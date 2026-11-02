#!/usr/bin/env bash
# ============================================================================
#  FCR Dashboard — server launcher (Linux)
#
#  Usage:
#      ./run_server.sh                 # local only  → http://<server-ip>:8000
#      ./run_server.sh --public        # accessible from your phone on the LAN
#      ./run_server.sh --public --port 8080
#
#  What it does:
#    1. Finds the project folder (default: /root/FCR dashboard)
#    2. Creates a virtualenv if missing and installs requirements.txt
#    3. Starts uvicorn in the foreground (Ctrl+C stops it cleanly)
#
#  Requirements on the server: python3 (>=3.10), python3-venv, unzip
#      Debian/Ubuntu:  apt install -y python3 python3-venv python3-pip unzip
# ============================================================================
set -euo pipefail

# ── Defaults (override with environment variables if you like) ──────────────
APP_DIR="${FCR_APP_DIR:-/root/FCR dashboard}"
BIND_HOST="127.0.0.1"
PORT="${FCR_PORT:-8000}"
WORKERS="${FCR_WORKERS:-1}"

# ── Parse arguments ────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --public|-p)  BIND_HOST="0.0.0.0"; shift ;;
    --port)       PORT="${2:?--port needs a number}"; shift 2 ;;
    --dir)        APP_DIR="${2:?--dir needs a path}"; shift 2 ;;
    -h|--help)
      sed -n '3,17p' "$0" | sed 's/^# \{0,1\}//' | sed '/^=\{10,\}$/d'
      exit 0 ;;
    *) echo "Unknown option: $1 (try --help)" >&2; exit 2 ;;
  esac
done

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
ok()   { printf '\033[32m✔\033[0m %s\n' "$*"; }
warn() { printf '\033[33m!\033[0m %s\n' "$*"; }
die()  { printf '\033[31m✘ %s\033[0m\n' "$*" >&2; exit 1; }

# ── 1. Sanity: project folder ──────────────────────────────────────────────
bold "== FCR Dashboard =="
[[ -d "$APP_DIR" ]] || die "Project folder not found: $APP_DIR
   Put the extracted repo there, or run:  ./run_server.sh --dir /path/to/repo"

cd "$APP_DIR"
[[ -f main.py ]] || die "main.py not found in $APP_DIR
   If you extracted a ZIP that contains an extra top-level folder, use:
     ./run_server.sh --dir '$APP_DIR/<inner-folder>'"
ok "Project: $APP_DIR"

# ── 2. Python & venv ───────────────────────────────────────────────────────
command -v python3 >/dev/null || die "python3 not installed (apt install python3)"
PY_VER="$(python3 -c 'import sys; print("%d.%d" % sys.version_info[:2])')"
ok "Python $PY_VER"

if [[ ! -d .venv ]]; then
  bold "Creating virtual environment (.venv)..."
  python3 -m venv .venv || die "Could not create venv. On Debian/Ubuntu run:
     apt install -y python3-venv"
fi

# Linux/macOS put executables in .venv/bin, Windows in .venv/Scripts.
if [[ -f .venv/bin/activate ]]; then
  # shellcheck disable=SC1091
  source .venv/bin/activate
  VENV_PY="python"
elif [[ -f .venv/Scripts/activate ]]; then
  # shellcheck disable=SC1091
  source .venv/Scripts/activate
  VENV_PY="python"
else
  die "Virtualenv was created but is incomplete. Delete .venv and run again:
     rm -rf .venv"
fi
ok "Virtualenv active"

# ── 3. Dependencies ────────────────────────────────────────────────────────
# Install only when requirements.txt is newer than the last install stamp.
STAMP=".venv/.requirements.sha256"
REQ_HASH="$(sha256sum requirements.txt | awk '{print $1}')"
if [[ ! -f "$STAMP" || "$(cat "$STAMP")" != "$REQ_HASH" ]]; then
  bold "Installing dependencies..."
  python -m pip install --upgrade pip -q
  python -m pip install -r requirements.txt
  echo "$REQ_HASH" > "$STAMP"
fi
ok "Dependencies ready"

# ── 4. Runtime folders (the app writes here) ───────────────────────────────
mkdir -p logs backups exports

# ── 5. Show how to reach it ────────────────────────────────────────────────
# `hostname -I` is Linux-only and is not present on every distro, so probe
# several ways and fall back to a placeholder instead of aborting the script.
SERVER_IP=""
if command -v hostname >/dev/null 2>&1; then
  SERVER_IP="$(hostname -I 2>/dev/null | awk '{print $1}')" || true
fi
if [[ -z "$SERVER_IP" ]] && command -v ip >/dev/null 2>&1; then
  SERVER_IP="$(ip -4 route get 1.1.1.1 2>/dev/null | awk '{for(i=1;i<=NF;i++) if($i=="src"){print $(i+1); exit}}')" || true
fi
[[ -z "$SERVER_IP" ]] && SERVER_IP="<server-ip>"

if [[ "$BIND_HOST" == "0.0.0.0" ]]; then
  warn "PUBLIC MODE — anyone who can reach this port gets full access."
  warn "The sign-in screen is visual only (demo/demo); it is NOT real security."
  echo
  echo "   Open from your phone:  http://${SERVER_IP}:${PORT}/"
  echo "   API docs:              http://${SERVER_IP}:${PORT}/api/docs"
  echo
  echo "   If it does not load, open the firewall:"
  echo "     ufw allow ${PORT}/tcp        (Ubuntu/Debian)"
  echo "     firewall-cmd --add-port=${PORT}/tcp --permanent && firewall-cmd --reload"
else
  echo "   Local only:  http://127.0.0.1:${PORT}/"
  echo "   For phone access run:  ./run_server.sh --public"
fi
echo

bold "Starting uvicorn on ${BIND_HOST}:${PORT}  (Ctrl+C to stop)"

# ── 6. Run ─────────────────────────────────────────────────────────────────
# A single worker is intentional: the app keeps a SQLite database (WAL) and an
# in-process auto-backup/auto-export scheduler. Running several uvicorn workers
# would start several schedulers writing to the same files and multiply the
# SQLite write contention. Use a reverse proxy (nginx) if you need more
# concurrency; do not raise FCR_WORKERS for this application.
if [[ "$WORKERS" != "1" ]]; then
  warn "Ignoring FCR_WORKERS=$WORKERS — this app must run with a single worker."
  WORKERS=1
fi

exec python -m uvicorn main:app \
  --host "$BIND_HOST" \
  --port "$PORT" \
  --workers 1 \
  --log-level info
