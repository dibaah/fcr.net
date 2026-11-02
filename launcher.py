"""
FCR Dashboard — Desktop Launcher
Launches Microsoft Edge in App Mode to display the FastAPI application.
Author: Farzad Meshkipour
"""
from __future__ import annotations
import os
import sys
import threading
import time
import socket
import subprocess
import ctypes
import traceback
from datetime import datetime
from pathlib import Path

# A windowed PyInstaller build has no console, so sys.stdout / sys.stderr are
# None. Several libraries call .isatty() or .write() on them (uvicorn's log
# formatter does, and it crashed the frozen app), so give them a harmless sink
# before anything else imports/logs.
if sys.stdout is None or sys.stderr is None:
    _null_stream = open(os.devnull, "w", encoding="utf-8")
    if sys.stdout is None:
        sys.stdout = _null_stream
    if sys.stderr is None:
        sys.stderr = _null_stream

if getattr(sys, "frozen", False):
    BASE_DIR = Path(sys.executable).parent
    sys.path.insert(0, str(sys._MEIPASS))
else:
    BASE_DIR = Path(__file__).parent

sys.path.insert(0, str(BASE_DIR))
os.chdir(str(BASE_DIR))

HOST = "127.0.0.1"
_srv = None
_edge_process = None
_server_error: list[str] = []  # traceback captured from the server thread


def log_fatal(message: str) -> Path:
    """Write a startup failure to a file the user can send us.

    A windowed PyInstaller build has no console, so anything printed to
    stderr is lost. Keeping the traceback on disk makes support possible.
    """
    log_dir = BASE_DIR / "logs"
    try:
        log_dir.mkdir(parents=True, exist_ok=True)
        path = log_dir / "launcher-error.log"
        with open(path, "a", encoding="utf-8") as fh:
            fh.write(f"\n{'=' * 70}\n{datetime.now().isoformat(timespec='seconds')}\n")
            fh.write(message.rstrip() + "\n")
        return path
    except Exception:
        return BASE_DIR / "launcher-error.log"


# CREATE_NO_WINDOW keeps every helper process silent. A windowed build (no
# console of its own) makes Windows open a NEW console window for each child
# process launched without this flag — that was the console flashing on every
# start.
_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0) if os.name == "nt" else 0


def _run_hidden(args, timeout: float = 20.0) -> subprocess.CompletedProcess | None:
    """Run a helper command with no console window and no stdio handles."""
    try:
        return subprocess.run(
            args,
            capture_output=True,
            text=True,
            timeout=timeout,
            creationflags=_NO_WINDOW,
        )
    except Exception:
        return None


def find_port(start=8000):
    """Find an available port starting from the given port."""
    for p in range(start, start + 20):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try:
                s.bind((HOST, p))
                return p
            except OSError:
                continue
    return start


def wait_ready(port, timeout=45.0):
    """Wait until the server is reachable on the given port."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with socket.create_connection((HOST, port), timeout=0.5):
                return True
        except OSError:
            time.sleep(0.15)
    return False


def start_server(port):
    """Start the FastAPI server using uvicorn."""
    global _srv
    try:
        import uvicorn
        from main import app
        cfg = uvicorn.Config(
            app=app,
            host=HOST,
            port=port,
            log_level="warning",
            access_log=False,
            loop="asyncio",
            # Keep main.py's file logging instead of letting uvicorn replace the
            # root handlers with console-only ones (which cannot work in a
            # windowed build anyway).
            log_config=None,
        )
        _srv = uvicorn.Server(cfg)
        _srv.run()
    except BaseException:  # noqa: BLE001 - must never die silently in a GUI build
        detail = traceback.format_exc()
        _server_error.append(detail)
        log_fatal(f"Server thread crashed while starting on port {port}:\n{detail}")
        raise


def log_note(message: str) -> None:
    """Append a timestamped note to logs/launcher.log (best effort)."""
    try:
        log_dir = BASE_DIR / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        with open(log_dir / "launcher.log", "a", encoding="utf-8") as fh:
            fh.write(f"{datetime.now().isoformat(timespec='seconds')} {message}\n")
    except Exception:
        pass


def edge_profile_alive(profile: Path) -> bool:
    """True while an msedge.exe process is still using our private profile.

    Implemented with ctypes instead of a PowerShell subprocess: a child
    process would pop its own console window in a windowed build, and polling
    it every 2 seconds made the window flash on the first launch.
    """
    count = _count_processes(str(profile))
    if count is not None:
        return count > 0
    return _edge_profile_alive_via_cim(profile)


def _count_processes(needle: str) -> int | None:
    """Count msedge.exe processes whose command line contains `needle`."""
    if os.name != "nt":
        return None
    try:
        from ctypes import wintypes

        class PROCESSENTRY32W(ctypes.Structure):
            _fields_ = [
                ("dwSize", wintypes.DWORD),
                ("cntUsage", wintypes.DWORD),
                ("th32ProcessID", wintypes.DWORD),
                ("th32DefaultHeapID", ctypes.c_size_t),
                ("th32ModuleID", wintypes.DWORD),
                ("cntThreads", wintypes.DWORD),
                ("th32ParentProcessID", wintypes.DWORD),
                ("pcPriClassBase", wintypes.LONG),
                ("dwFlags", wintypes.DWORD),
                ("szExeFile", wintypes.WCHAR * 260),
            ]

        kernel32 = ctypes.windll.kernel32
        snapshot = kernel32.CreateToolhelp32Snapshot(0x00000002, 0)  # TH32CS_SNAPPROCESS
        if snapshot == -1 or snapshot is None:
            return None
        found = 0
        try:
            entry = PROCESSENTRY32W()
            entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
            ok = kernel32.Process32FirstW(snapshot, ctypes.byref(entry))
            while ok:
                if entry.szExeFile.lower() == "msedge.exe":
                    cmd = _read_command_line(entry.th32ProcessID)
                    if cmd and needle.lower() in cmd.lower():
                        found += 1
                ok = kernel32.Process32NextW(snapshot, ctypes.byref(entry))
        finally:
            kernel32.CloseHandle(snapshot)
        return found
    except Exception:
        return None


def _read_command_line(pid: int) -> str | None:
    """Read a process command line via NtQueryInformationProcess + PEB."""
    try:
        from ctypes import wintypes

        PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
        ProcessCommandLineInformation = 60
        kernel32 = ctypes.windll.kernel32
        ntdll = ctypes.windll.ntdll

        handle = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        if not handle:
            return None
        try:
            size = wintypes.ULONG(0)
            ntdll.NtQueryInformationProcess(
                handle, ProcessCommandLineInformation, None, 0, ctypes.byref(size)
            )
            if not size.value:
                return None
            buf = ctypes.create_string_buffer(size.value)
            status = ntdll.NtQueryInformationProcess(
                handle, ProcessCommandLineInformation, buf, size.value, ctypes.byref(size)
            )
            if status != 0:
                return None
            # UNICODE_STRING at the start of the buffer: Length, MaxLength, Buffer
            length = ctypes.c_ushort.from_buffer(buf).value
            text = buf.raw[16:16 + length].decode("utf-16-le", "ignore")
            return text
        finally:
            kernel32.CloseHandle(handle)
    except Exception:
        return None


def _edge_profile_alive_via_cim(profile: Path) -> bool:
    """Last-resort fallback — still hidden, but slower."""
    script = (
        "(Get-CimInstance Win32_Process -Filter \"Name='msedge.exe'\" | "
        "Where-Object { $_.CommandLine -like '*" + str(profile) + "*' }).Count"
    )
    result = _run_hidden(["powershell", "-NoProfile", "-NonInteractive", "-Command", script])
    if result is None:
        return False
    try:
        return int((result.stdout or "0").strip() or "0") > 0
    except ValueError:
        return False


def stop_server():
    """Signal the server to stop."""
    global _srv
    if _srv:
        _srv.should_exit = True


def show_messagebox(title, message, icon_type=0):
    """Display a native Windows MessageBox."""
    if sys.platform == "win32":
        ctypes.windll.user32.MessageBoxW(0, message, title, icon_type)
    else:
        print(f"[{title}] {message}")


def find_edge_path():
    """Find the Microsoft Edge executable path."""
    if sys.platform != "win32":
        return None
    
    # Common Edge installation paths
    edge_paths = [
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\Edge\Application\msedge.exe"),
        os.path.expandvars(r"%PROGRAMFILES%\Microsoft\Edge\Application\msedge.exe"),
        os.path.expandvars(r"%PROGRAMFILES(X86)%\Microsoft\Edge\Application\msedge.exe"),
    ]
    
    for path in edge_paths:
        if os.path.exists(path):
            return path
    
    # Try to find via registry
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, 
                           r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe") as key:
            path = winreg.QueryValue(key, None)
            if os.path.exists(path):
                return path
    except (ImportError, FileNotFoundError, OSError):
        pass
    
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                           r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe") as key:
            path = winreg.QueryValue(key, None)
            if os.path.exists(path):
                return path
    except (ImportError, FileNotFoundError, OSError):
        pass
    
    return None


def edge_profile_dir() -> Path:
    """Dedicated Edge profile so the app window owns its own browser process.

    Without a private ``--user-data-dir``, ``msedge.exe --app=…`` hands the URL
    to an already running Edge instance and exits immediately; the launcher
    would then think the window was closed and shut the server down. A private
    profile also keeps the app isolated from the user's browsing session.
    """
    root = os.environ.get("LOCALAPPDATA") or str(Path.home())
    path = Path(root) / "FCR_Dashboard" / "edge-profile"
    try:
        path.mkdir(parents=True, exist_ok=True)
    except OSError:
        path = BASE_DIR / "edge-profile"
        path.mkdir(parents=True, exist_ok=True)
    return path


def launch_edge_app_mode(url):
    """Launch Microsoft Edge in App Mode."""
    edge_path = find_edge_path()

    if not edge_path:
        show_messagebox(
            "Microsoft Edge Not Found",
            "Microsoft Edge is required to run this application.\n\n"
            "Please install Microsoft Edge and try again.\n\n"
            "Download from: https://www.microsoft.com/edge",
            16  # Error icon
        )
        return None

    # Build command for Edge App Mode
    cmd = [
        edge_path,
        "--app=" + url,
        "--user-data-dir=" + str(edge_profile_dir()),
        "--no-first-run",
        "--disable-features=TranslateUI",
        "--disable-component-extensions-with-background-pages",
        "--disable-background-networking",
        "--disable-sync",
        "--metrics-recording-only",
        "--no-default-browser-check",
    ]

    try:
        # Edge is a GUI app; hide its handles and make sure no console window
        # is attached to the child process either.
        process = subprocess.Popen(
            cmd,
            creationflags=_NO_WINDOW,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        return process
    except Exception as e:
        show_messagebox(
            "Failed to Launch Edge",
            f"Could not start Microsoft Edge:\n{str(e)}",
            16  # Error icon
        )
        return None


def main():
    """Main entry point for the launcher."""
    global _edge_process
    
    # Find an available port
    port = find_port(8000)
    url = f"http://{HOST}:{port}"
    
    # Start server in background thread
    server_thread = threading.Thread(
        target=start_server,
        args=(port,),
        daemon=False,
        name="uvicorn"
    )
    server_thread.start()
    
    # Wait for server to be ready
    if not wait_ready(port, timeout=45.0):
        stop_server()
        detail = (_server_error[0] if _server_error else "The server did not become reachable.")
        err_path = log_fatal(
            f"Startup failed: server not reachable on {url}\n\n{detail}"
        )
        tail = detail.strip().splitlines()[-1] if detail.strip() else "unknown error"
        show_messagebox(
            "Server Startup Failed",
            "The application server failed to start.\n\n"
            f"Last error: {tail}\n\n"
            f"Full details were written to:\n{err_path}",
            16  # Error icon
        )
        sys.exit(1)
    
    # Launch Edge in App Mode
    _edge_process = launch_edge_app_mode(url)
    
    if _edge_process is None:
        # Edge not found or failed to launch - already showed messagebox
        stop_server()
        sys.exit(1)
    
    # Wait for Edge to exit
    try:
        _edge_process.wait(timeout=3.0)
        # The launcher stub exited almost immediately. That happens when Edge
        # hands the window to a process we do not own; keep serving while a
        # browser process still runs against our private profile.
        profile = edge_profile_dir()
        if edge_profile_alive(profile):
            log_note("Edge stub exited immediately; monitoring the app profile instead.")
            while edge_profile_alive(profile):
                time.sleep(2.0)
        else:
            log_note("Edge process exited immediately and no app window was found.")
    except subprocess.TimeoutExpired:
        _edge_process.wait()
    except KeyboardInterrupt:
        pass
    
    # Gracefully shutdown server
    stop_server()
    
    # Wait for server thread to finish
    server_thread.join(timeout=5.0)
    
    # Exit cleanly
    sys.exit(0)


if __name__ == "__main__":
    main()
