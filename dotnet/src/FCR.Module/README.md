# FCR Module — ERMS Integration

```csharp
// ERMS Program.cs — after the host's authentication configuration
builder.Services.AddFcrModule(builder.Configuration);

// ...
app.UseAuthentication();
app.UseFcrModule();       // FCR audit middleware; after authentication
app.UseAuthorization();
app.MapFcrModule();       // FCR APIs, reports, and health probes
```

## What the module owns

- FCR domain/application/infrastructure services and API routes.
- FCR workflow, reporting, audit, attachment, and archive operations.
- FCR authorization policies (`AdminConsole`, `RequesterAccess`, `ReviewerAccess`).

## What ERMS owns

- Authentication scheme and login/logout.
- Main UI shell and styling.
- The ERMS-level permission gate for opening FCR.
- Identity/role claim mapping and deployment lifecycle.

`AddFcrModule` does not register a login/authentication scheme or automatically apply migrations. `InitializeFcrModuleAsync` seeds FCR roles and, only when explicitly configured and the FCR user table is empty, creates the first ERMS-mapped FCR Admin (`FCR Admin` + `FCR User`). It never creates the standalone `admin/admin` demo account.

## First FCR Admin bootstrap

For the first controlled module initialization only, configure the stable ERMS subject and username:

```json
{
  "Fcr": {
    "BootstrapAdmin": {
      "ExternalId": "stable-ERMS-subject",
      "Username": "initial-fcr-admin",
      "DisplayName": "Initial FCR Admin",
      "Email": "admin@example.com"
    }
  }
}
```

If the FCR user table is empty, `InitializeFcrModuleAsync()` creates this mapped account with `FCR Admin` and `FCR User` roles. If any FCR user already exists, bootstrap does nothing and does not change existing roles. Without this configuration, no admin user is created. Remove the bootstrap setting after initialization if your deployment policy requires it.

## Database initialization

During a controlled deployment/startup migration step, ERMS can explicitly run:

```csharp
await app.Services.InitializeFcrModuleAsync();
```

This invokes FCR's EF migration path. Back up the target DB and validate migrations on a copy first. Do not point this at production until the migration/rollback plan is approved.

## Identity contract (current implementation)

Current workflow endpoints use FCR's internal numeric user IDs for ownership, reviewer assignments, comments, and attachment metadata. The module reads the upstream subject claim configured by `Fcr:Identity:ExternalIdClaim` (default: `ClaimTypes.NameIdentifier`) and resolves it against `users.external_id`. After mapping, the module replaces the internal `ClaimTypes.NameIdentifier` with the FCR numeric user ID and refreshes `ClaimTypes.Name` and `ClaimTypes.Role` from the FCR database. This prevents upstream role claims from bypassing FCR role assignment. The standalone local-cookie host continues to resolve its numeric ID as a compatibility fallback.

**ERMS integration must map its authenticated identity to the FCR user ID and FCR role claims before the FCR endpoints are used.** Do not pass arbitrary ERMS identity IDs directly if they are not numeric FCR user IDs. Create and assign mappings through the FCR Admin API (`POST /api/admin/fcr-users`, `PUT /api/admin/fcr-users/{id}`). Users need an FCR role (`FCR User`, `FCR Admin`, or one of the workflow roles) to access protected FCR routes. Unmapped ERMS users remain signed into ERMS but are denied FCR access. The ERMS application must separately enforce its own FCR access permission gate.

The standalone `FCR.Web` project keeps its local cookie login only for development and parity tests. ERMS should reference `FCR.Module` and must not map `MapAuthApi()`.