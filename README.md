# FCR — .NET ERMS Module

FCR (Field Change Request) is implemented as a hostable **ASP.NET Core 8 module/library** for integration into the existing ERMS application.

## Repository scope

This repository contains the .NET implementation only. The Python standalone/dashboard edition is maintained separately at [dibaah/fcr-python](https://github.com/dibaah/fcr-python).

## Build and test

From the repository root:

```bash
dotnet build dotnet/FCR.slnx
dotnet test dotnet/FCR.slnx
```

## Integration

FCR is intended to run inside ERMS in-process. ERMS owns authentication, primary access to the module, UI shell and navigation. FCR maps the ERMS identity and applies FCR-specific authorization server-side.

See:
- [Master Plan](docs/MASTER_PLAN.md)
- [ERMS module integration UAT checklist](docs/erms-module-integration-uat-checklist.md)
- [.NET module README](dotnet/README.md)

## Production

Actual ERMS UI wiring, identity claim configuration, archive storage permissions, business UAT, migration/rollback rehearsal and production cutover remain gated. No production cutover is implied by repository changes.
