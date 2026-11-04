# FCR .NET deployment and operations

## Hosting model

**Production target is FCR.Module inside ERMS**, not the standalone FCR.Web development host. ERMS must supply authentication, UI navigation, and the module access gate. The standalone host's local login and default development admin are disabled outside the Development environment.

The Linux systemd files in dotnet/deploy/ are retained for controlled standalone/test deployments. They are not approval to deploy to production.

## Build and test

From the repository root:

\`\`\`bash
cd dotnet
dotnet restore FCR.slnx
dotnet build FCR.slnx --configuration Release --no-restore
dotnet test FCR.slnx --configuration Release
dotnet publish src/FCR.Web -c Release -o out/fcr-web
\`\`\`

.NET SDK 8 is required. CI materializes the legacy SQLite/static fixtures from the Python repository; those fixtures are not committed to this repository.

## Standalone Linux service (non-production unless explicitly approved)

Prerequisites: Linux, .NET 8 runtime, systemd, curl, sudo, and a local filesystem for the SQLite database.

The deployment script:

1. Publishes FCR.Web.
2. Stops the service before copying the SQLite database backup.
3. Keeps the previous application directory as /opt/fcr-web.prev.
4. Creates a dedicated unprivileged fcr service account.
5. Sets ownership on /var/lib/fcr data, backup, and attachment directories.
6. Installs the systemd unit and restarts the service.
7. Checks /health/ready.

Run from the repo root:

\`\`\`bash
sudo ./dotnet/deploy/deploy.sh
\`\`\`

The script is not an automated rollback. Inspect its output and verify the health check and logs after each deployment.

### Configuration

Use /etc/fcr-web/fcr-web.env for systemd environment overrides. It is created with mode 0600; edit it with sudo. ASP.NET Core environment variables use double underscores for nested keys:

\`\`\`ini
ASPNETCORE_ENVIRONMENT=Production
Fcr__DataDir=/var/lib/fcr/data
Fcr__BackupLocation=/var/lib/fcr/backups
Fcr__AttachmentRoot=/var/lib/fcr/attachments
\`\`\`

The service runs as fcr:fcr. Give this account only the filesystem permissions it needs. If the archive root is an approved network share, configure its mount and permissions before starting the service.

Production Kestrel config binds HTTP to 127.0.0.1:8080. Put a trusted reverse proxy in front and terminate TLS there, or configure a certificate using Fcr:HttpsCertPath, Fcr:HttpsCertPassword, and Fcr:HttpsPort. Set Fcr:BehindProxy=true only when behind a trusted proxy. Do not expose plain HTTP directly to an untrusted network.

## Database and archive safety

- Keep the SQLite database on a supported **local filesystem**, not an SMB/UNC/network share. The archive/attachment directory may be a share, but the database should not.
- Before migrations or release changes, back up both the database and archive attachments.
- Rehearse migration and restore on a copy. Verify record counts, IDs, request/workflow rows, attachment counts, and archive paths.
- Database migration in standalone FCR.Web occurs at startup. ERMS module migration is explicit via InitializeFcrModuleAsync() and should run during a controlled maintenance window.
- Confirm service-account access to the archive, antivirus scanning, backup coverage, retention, and restore permissions.
- Production identity provider (AD/Entra), database provider/concurrency strategy, and security/UAT sign-off are still required decisions.

## Health and logs

- Liveness: GET /health/live
- Readiness: GET /health/ready (checks SQLite connectivity)
- Logs: sudo journalctl -u fcr-web -n 100 --no-pager
- Service: sudo systemctl status fcr-web

## Manual rollback

1. Stop the service: sudo systemctl stop fcr-web.
2. Preserve the failed deployment and current database for diagnosis.
3. Restore the prior app directory from /opt/fcr-web.prev (do not delete the failed build until evidence is collected).
4. Restore the pre-deployment database backup **only if** data created/changed after deployment can be safely discarded or reconciled. Blind DB rollback can lose user submissions.
5. Restore attachments/archive from the matching backup if necessary.
6. Start the service and verify /health/ready, logs, and a non-destructive smoke test.

## Production release gate

Production deployment/cutover requires explicit IT and business approval, ERMS integration validation, security review, UAT sign-off, and a tested backup/restore/rollback rehearsal. This document does not authorize production deployment.
