# ERMS Integration Findings — Phase 0/1

**Updated:** 2026-11-03
**Host decision:** FCR runs in-process inside the existing ASP.NET Core 8 ERMS application.

## Repository facts

- The FCR .NET solution targets `net8.0`; this compatibility/module foundation is merged to main (PR #5), and .NET 8 CI passed.
- `FCR.Module` now exposes `AddFcrModule`, `UseFcrModule`, `MapFcrModule`, and `InitializeFcrModuleAsync`.
- `FCR.Web` is retained as a standalone development/parity-test host. It owns local cookie login and admin seeding; ERMS should not map `MapAuthApi()`.
- FCR APIs and workflow records use a local numeric FCR user ID for owner/reviewer/comment/attachment foreign keys.
- FCR's current EF provider is SQLite. No ERMS database provider configuration has been supplied or inspected; production database compatibility remains an explicit discovery item.
- The ERMS host source/project is not present in this repository, so host claims, authorization policies, UI routing, DI conventions, and DB provider cannot be verified from source here.

## Module lifecycle contract

```csharp
builder.Services.AddFcrModule(builder.Configuration);

app.UseAuthentication();
app.UseFcrModule();
app.UseAuthorization();
app.MapFcrModule();
```

- `AddFcrModule` registers FCR services, health check, and FCR authorization policies. It does not register authentication.
- `UseFcrModule` must be placed after host authentication. It resolves FCR identity and runs audit middleware.
- `MapFcrModule` maps FCR APIs, reports, and health endpoints; it does not map standalone login endpoints.
- `InitializeFcrModuleAsync` explicitly runs EF migrations and seeds role definitions only. It does not create a local development admin.
- Database migration must be rehearsed on a backup/copy before use with production data.

## Identity/authorization contract

- ERMS authenticates users and enforces its own FCR module permission gate.
- The FCR module maps the configured external subject claim (`Fcr:Identity:ExternalIdClaim`, default `ClaimTypes.NameIdentifier`) to `users.external_id`.
- FCR creates a numeric internal user ID claim and FCR role claims from the mapped database row; it does not trust upstream role claims as FCR roles.
- FCR roles are assigned through FCR Admin/System Administrator management endpoints. Roles include `FCR User`, `FCR Admin`, `Requester`, `Reviewer`, `Approver`, and `System Administrator`.
- The module has a one-time explicit bootstrap option for the first ERMS-mapped FCR Admin when the FCR user table is empty; it never creates the standalone `admin/admin` account.
- Unmapped/inactive ERMS users remain authenticated in ERMS but receive no FCR roles/internal ID, so protected FCR routes deny access.
- ERMS identity mapping is not automatic; administrators must map each ERMS subject to an FCR user and roles.
- FCR Admin processing assigns number/category/contractor/subcontractor; archive writes JSON + printable HTML request forms and copies attachments into the FCR-number folder. The linked FCR record receives the archive path.

## Items that require actual ERMS-side confirmation

1. Exact ERMS subject claim name and stability (set `Fcr:Identity:ExternalIdClaim`).
2. Whether ERMS permits FCR module route/API registration and middleware insertion in the stated order.
3. Database engine/provider and whether FCR should use ERMS's DB or a separate FCR database.
4. Server-side archive root and service-account permissions for FCR-number folders and attachments.
5. ERMS UI navigation/template and any reverse-proxy/base-path requirements.

These are not reasons to pause repository-side work; they are host integration/UAT checks before deployment.