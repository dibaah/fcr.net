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

`AddFcrModule` does not register a login/authentication scheme, seed a local admin account, or automatically apply migrations. This avoids the module replacing ERMS auth or modifying the DB during an uncontrolled host startup.

## Database initialization

During a controlled deployment/startup migration step, ERMS can explicitly run:

```csharp
await app.Services.InitializeFcrModuleAsync();
```

This invokes FCR's EF migration path. Back up the target DB and validate migrations on a copy first. Do not point this at production until the migration/rollback plan is approved.

## Identity contract (current implementation)

Current workflow endpoints use FCR's internal numeric user IDs for ownership, reviewer assignments, comments, and attachment metadata. The current API reads `ClaimTypes.NameIdentifier` as a `long` and reads `ClaimTypes.Name` for the username. Role checks use `ClaimTypes.Role` values defined in `FCR.Domain.RoleNames`.

**ERMS integration must map its authenticated identity to the FCR user ID and FCR role claims before the FCR endpoints are used.** Do not pass arbitrary ERMS identity IDs directly if they are not numeric FCR user IDs. The stable ERMS-to-FCR user mapping is a Phase 2 integration item; this module scaffold does not silently provision users or infer roles.

The standalone `FCR.Web` project keeps its local cookie login only for development and parity tests. ERMS should reference `FCR.Module` and must not map `MapAuthApi()`.