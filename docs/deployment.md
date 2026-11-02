# Deployment & Rollback — FCR .NET (Phase 3)

The .NET `FCR.Web` host is the production hosting model. The Python desktop
launcher (`launcher.py`, `START_SERVER.bat`) is **not** a hosting model for the
centralized app.

## Topology

```
workstations ──http(s)──> FCR.Web (Kestrel, 0.0.0.0:8080 / :8443)
                              ├── SQLite  /var/lib/fcr/data/fcr_database.db (WAL)
                              ├── backups /var/lib/fcr/backups
                              └── attachments /var/lib/fcr/attachments
                                  (or a mounted network share)
```

## Configuration

All settings are environment-overridable (`Fcr__DataDir=...` env vars map to
`Fcr:DataDir`). Precedence: env vars > `appsettings.{Environment}.json` >
`appsettings.json` > code defaults > `settings.json` (runtime UI settings).

| Key | Default | Purpose |
|---|---|---|
| `Kestrel:Endpoints:Http:Url` | `http://0.0.0.0:8080` | HTTP bind (all interfaces for workstation access) |
| `Fcr:HttpsCertPath` | _empty_ | PFX path; when set, HTTPS listens on `Fcr:HttpsPort` (8443) |
| `Fcr:HttpsCertPassword` | _empty_ | PFX password |
| `Fcr:DataDir` | `data` | Dir holding `fcr_database.db` + `settings.json` |
| `Fcr:BackupLocation` | `backups` | Backup dir (relative → under DataDir) |
| `Fcr:AttachmentRoot` | `X:\1-DC` | Shared attachment root; seeds `attachment_root` when `settings.json` doesn't exist — a stored value always wins |
| `Fcr:StaticDir` | _auto_ | SPA dir; auto-probes `static/` beside the binary or the repo `static/` |
| `Fcr:BehindProxy` | `false` | Honor `X-Forwarded-For/Proto` when behind a reverse proxy |
| `Fcr:SeedAdminPassword` | `admin` | Initial admin password — only used when the `users` table is empty (dev auth) |

## HTTPS

HTTPS is opt-in: set `Fcr:HttpsCertPath` (`.pfx`) and the app adds an HTTPS
listener on `Fcr:HttpsPort`.

```bash
# development cert (testing only — browsers will warn)
dotnet dev-certs https --export-path fcr-web-dev.pfx --password <pw>
# production: obtain the org's cert, export as PFX to /etc/fcr-web/fcr-web.pfx
```

Alternatively terminate TLS at a reverse proxy (nginx/IIS) and set
`Fcr:BehindProxy=true`.

## Deploy

```bash
dotnet/deploy/deploy.sh            # publishes, backs up DB, installs, restarts, health-checks
```

Equivalent manual steps:

```bash
cd dotnet && dotnet publish src/FCR.Web -c Release -o out/fcr-web
sudo mkdir -p /opt/fcr-web /var/lib/fcr/{data,backups,attachments}
sudo cp -a out/fcr-web/. /opt/fcr-web/
sudo cp dotnet/deploy/fcr-web.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now fcr-web
curl http://localhost:8080/health/ready    # {"status":"Healthy"...}
```

## Rollback

`deploy.sh` keeps the previous build at `/opt/fcr-web.prev` and takes a
pre-deploy DB copy under `$DATA_DIR/backups/fcr_database_predeploy_*.db`.

```bash
sudo systemctl stop fcr-web
sudo cp -a /opt/fcr-web.prev/. /opt/fcr-web/
# only if data was written since deploy:
# cp /var/lib/fcr/data/backups/fcr_database_predeploy_<ts>.db /var/lib/fcr/data/fcr_database.db
sudo systemctl start fcr-web
curl http://localhost:8080/health/ready
```

The DB schema is forward-compatible for rollbacks inside a phase (Phase 2 added
no schema changes); schema-changing phases will ship a migration + a paired
rollback script.

## Monitoring

- `GET /health/live` — process alive (liveness probe)
- `GET /health/ready` — executes `SELECT 1` against SQLite (readiness probe)
- `GET /api/health` / `GET /api/about` — app-level parity endpoints (version, DB path, record count)
- Request logging is enabled (`Microsoft.AspNetCore.HttpLogging`); logs go to journald: `journalctl -u fcr-web -f`

## Multi-workstation access

Kestrel binds `0.0.0.0`, so any workstation that reaches the host hits the app:

1. `hostname -I` on the server → note the LAN IP.
2. Open the firewall: `sudo ufw allow 8080/tcp` (and 8443 if HTTPS).
3. From a workstation: `http://<server-ip>:8080/` → dashboard loads; login unchanged.
4. Point every workstation's `attachment_root` (PUT `/api/settings`) at the shared store.

Verified in-repo: `ss -tln` shows `0.0.0.0:8080`; API + SPA respond on the LAN address.

## Concurrency

SQLite runs WAL mode; the app is safe for concurrent read/write from multiple
users — short write transactions, one writer at a time (SQLite semantics).
`ApiParityTests.Concurrency_ParallelCreatesAndReads` fires parallel
create/read/delete traffic through the full HTTP stack.
