# ERMS-hosted FCR Module Integration Checklist

This checklist is for the actual ERMS ASP.NET Core 8 host. The FCR repository contains a fake-host integration test, but it does not contain the ERMS application itself.

## Host wiring

Register and map FCR in the existing ERMS app (no separate FCR login):

```csharp
builder.Services.AddFcrModule(builder.Configuration);

app.UseAuthentication();  // ERMS authentication
app.UseFcrModule();       // FCR subject mapping + audit
app.UseAuthorization();   // ERMS/FCR policies
app.MapFcrModule();
```

Run `InitializeFcrModuleAsync()` during a controlled startup/migration window after backing up the FCR database. Do not run unreviewed migrations against production.

## Required configuration

- `Fcr:Identity:ExternalIdClaim`: stable ERMS subject claim (for example `sub`).
- `Fcr:DataDir` or `FCR_DATA_DIR`: server-local directory containing the FCR SQLite database and request uploads.
- `Fcr:AttachmentRoot`: official archive root (prefer an approved UNC/server share).
- ERMS application/service identity must have read/write/create-directory permissions on the archive root.
- Ensure ERMS itself enforces its FCR module access gate; the FCR role policy is a second server-side gate.

## Identity and permissions smoke tests

- [ ] ERMS user without FCR module permission cannot navigate to or call FCR.
- [ ] ERMS user with FCR module permission but no FCR identity mapping gets 403 from protected FCR routes.
- [ ] Mapped FCR User/Requester can create a request using Title, Justification, and Description.
- [ ] Mapped FCR Admin can manage FCR mappings and administer requests.
- [ ] Upstream ERMS role claims do not grant FCR roles by themselves.
- [ ] Role change/deactivation is reflected on the next request.
- [ ] No separate FCR login or standalone development admin is used in ERMS.

## Request, workflow, and archive smoke tests

- [ ] Submitter creates and tracks a request; another submitter cannot see it.
- [ ] Upload all request attachments before official FCR number assignment.
- [ ] FCR Admin assigns FCR number, category, contractor, and subcontractor.
- [ ] Numbered archive folder is created under the configured archive root.
- [ ] Archive contains the printable HTML form, JSON manifest, and attachment copies.
- [ ] Linked legacy FCR record has the correct archive location.
- [ ] Sequential reviewers receive assignments in order; only the active reviewer can decide.
- [ ] Approve/reject/return and resubmit paths produce correct status/history/notifications.
- [ ] Admin can retry archive synchronization after a transient share failure.
- [ ] FCR reports are available to admins and return 403 to authenticated non-admins.

## Data and operational checks

- [ ] Back up the existing FCR database and attachment archive.
- [ ] Rehearse module migration against a copy; reconcile legacy record count/IDs.
- [ ] Confirm database and request-upload paths are server-local (do not host a live SQLite DB on an unsupported shared filesystem).
- [ ] Confirm archive permissions, antivirus/backup coverage, and restore procedure.
- [ ] Record tester, environment, test request/FCR numbers, and evidence.
- [ ] Confirm rollback steps before production deployment.

**Production cutover is a separate gated decision; this checklist does not authorize deployment.**
