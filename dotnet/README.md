# FCR .NET Migration — Solution

Centralized ASP.NET Core successor to the Python/FastAPI app, built per `masterplan.md` Phases 1–2.
The Python application at the repo root stays untouched and operational.

## Layout

- `src/FCR.Domain` — entities (`Fcr`, `ImportLog`, `Meta`, `User`, `Role`, `UserRole`), `RoleNames`, constants, FCR-code + value-cleaning rules, status-color logic. No DB/HTTP dependencies.
- `src/FCR.Application` — DTOs (`FcrRecordInput`, `FcrQuery`, bulk requests, auth DTOs), `IFcrRepository`, `IUserService`, `ISettingsStore`, `IStatsService`, `IExcelExportService`, `IFolderService`.
- `src/FCR.Infrastructure` — EF Core (`FcrDbContext`), migrations, `DatabaseMigrator`, `LegacySchemaUpgrader`, `FcrRepository`, `UserService`, `UserSeeder`, `PasswordHasher` (PBKDF2), `JsonSettingsStore`, `FcrBackupService`, `StatsService`, `ExcelExportService` (ClosedXML + OpenXML chart injection), `FolderService`, `AutoOpsService` (60 s auto-backup/auto-export loop).
- `src/FCR.Web` — ASP.NET Core host; runs `DatabaseMigrator` + `UserSeeder` at startup, maps the full `/api` surface, serves the existing SPA from `static/`, cookie auth + role authorization.
- `tests/FCR.Tests` — xUnit: schema-compatibility vs a copy of the real DB, repository parity, backup, code validation, HTTP contract parity (`ApiParityTests` via `WebApplicationFactory`), auth/RBAC (`AuthTests`).

## API parity

All 22 FastAPI routes are ported with identical paths, snake_case payloads, `{"detail": ...}` errors, and status codes (400 validation / 404 missing / 409 duplicate code / 201 create):

- `GET/POST/PUT/DELETE /api/fcr`, `POST /api/fcr/bulk-save`, `POST /api/fcr/bulk-delete`
- `GET /api/stats`, `/api/options`, `/api/notifications`
- `GET/PUT /api/settings`, `POST /api/settings/reset`
- `POST /api/backup`, `GET /api/backups` (restore stays a `FcrBackupService` API, matching Python which exposes no HTTP restore route)
- `GET /api/export`, `/api/export-with-charts`, `POST /api/auto-export/run`
- `GET /api/fcr/{id}/open-folder`, `POST /api/fcr/{id}/create-folder`
- `GET /api/health`, `/api/about`

Known deviation: `open-folder`/`create-folder` return `{ok, path}` instead of launching a file manager — a centralized server cannot open folders on the caller's machine.

## Authentication & roles (Phase 4)

Cookie auth (`fcr_auth`, HttpOnly, SameSite=Lax, 8 h sliding) over local `users`/`roles`/`user_roles`
tables. **Development mechanism for controlled testing only** — the production provider (AD/Entra) is
an open IT decision in the master plan; swap `AddAuthentication` in `Program.cs` when decided.

- `POST /api/auth/login` `{username, password}` → 200 + session cookie, 401 `{"detail": ...}` on bad credentials
- `POST /api/auth/logout`, `GET /api/auth/me`
- `GET /api/admin/roles`, `GET/POST/PUT /api/admin/users` — **System Administrator** role only
- All other `/api/*` routes require an authenticated user; only `/api/health` stays anonymous
- Roles: FCR User, Requester, FCR Admin, Reviewer, Approver, System Administrator (multi-role supported). ERMS external users are mapped by a stable subject claim to an FCR internal user record; FCR roles are loaded from the FCR database per request.
- First-run admin: `admin` / `Fcr:SeedAdminPassword` (default `admin`) — created only when `users` is empty
- `OnValidatePrincipal` re-checks `is_active` and refreshes role claims per request — deactivation and
  role changes apply immediately to existing sessions
- PBKDF2-SHA256 password hashes (`pbkdf2$iters$salt$hash`); never store plaintext

## ERMS module integration (Phase 1–2)

The `FCR.Module` project is the in-process integration target for the ERMS ASP.NET Core 8 application. ERMS owns authentication and its main UI shell; FCR does not add a login scheme when registered as a module.

```csharp
builder.Services.AddFcrModule(builder.Configuration);

app.UseAuthentication();
app.UseFcrModule();       // identity mapping + audit middleware
app.UseAuthorization();
app.MapFcrModule();
```

Configure the stable ERMS subject claim (default `ClaimTypes.NameIdentifier`):

```json
{
  "Fcr": {
    "Identity": {
      "ExternalIdClaim": "sub"
    }
  }
}
```

The claim value is matched against `users.external_id`. FCR Admin/System Administrator maps users and assigns FCR roles through:

- `GET /api/admin/fcr-users`
- `POST /api/admin/fcr-users` — create external identity mapping + roles
- `PUT /api/admin/fcr-users/{id}` — update active state/profile/roles

The module refreshes FCR role claims from the FCR DB on each request and does not trust incoming ERMS role claims as FCR roles. Unmapped or inactive users are denied FCR access. ERMS must separately enforce its FCR module access gate.

`InitializeFcrModuleAsync()` runs EF migrations and seeds FCR role definitions only; it does not create the standalone development admin. Back up and rehearse migrations on a database copy before production.

## Build & test

```bash
cd dotnet
dotnet restore && dotnet build
dotnet test
```

## Hosting (Phase 3)

Kestrel binds `http://0.0.0.0:8080` (config section `Kestrel:Endpoints`, overridable by env `Kestrel__Endpoints__Http__Url`). HTTPS is opt-in: set `Fcr:HttpsCertPath` (PFX) + `Fcr:HttpsCertPassword` and the app also listens on `Fcr:HttpsPort` (8443). `Fcr:BehindProxy=true` enables `X-Forwarded-*` handling.

Probes: `GET /health/live` and `GET /health/ready` (executes `SELECT 1` on SQLite). Request logging via `Microsoft.AspNetCore.HttpLogging`. Deployment, HTTPS setup, firewall/multi-workstation checks and rollback: `../docs/deployment.md`; unit + script in `deploy/`.

## Database procedures

**Migration** — `DatabaseMigrator.MigrateAsync()` runs at Web startup and is safe on any DB:

- Fresh database → EF migrations create the schema (DDL is verbatim from the existing DB, verified byte-for-byte by `FreshMigration_ReproducesExistingSchema`).
- Existing schema-v2 database → baselines `__EFMigrationsHistory` at the initial migration, then `MigrateAsync` applies later ones incrementally; data untouched.
- Pre-v2 database → `LegacySchemaUpgrader` adds missing columns first (port of `migrate_db.py`), then baselines.
- Always stamps `meta.schema_version = 2`.

To add future migrations:

```bash
cd src/FCR.Infrastructure
dotnet ef migrations add <Name> --context FcrDbContext
```

**Backup** — `FcrBackupService` mirrors the Python backup service: timestamped `fcr_database_backup_*.db` copies via the SQLite backup API (WAL-checkpointed), newest-first listing, pruning to 20, restore by filename with path-traversal protection.

**Safety** — never run against the live `fcr_database.db` during development; `FCR_DATA_DIR` / `Fcr:DataDir` points any run at a copy (tests do this automatically).
