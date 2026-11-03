# FCR inside ERMS — .NET Master Plan

**Status:** .NET implementation baseline; Python edition split into its own repository  
**Target host:** Existing ERMS ASP.NET Core 8 application  
**Integration decision:** FCR .NET runs inside ERMS as a module/library, not as a separate service.

## 1. Product goal

Deliver FCR as an integrated ERMS module. ERMS owns the shell, authentication, navigation and primary module-access gate. FCR owns domain workflows, FCR records, audit history and FCR-specific authorization.

## 2. Repository boundaries

- **This repository — [fcr.net](https://github.com/dibaah/fcr.net):** .NET 8 module, tests, .NET integration/deployment documentation.
- **Python repository — [fcr-python](https://github.com/dibaah/fcr-python):** Python/FastAPI standalone dashboard, frontend, tests, Python requirements and Python-specific plan.
- Python source and Python runtime artifacts should not be reintroduced here.
- Do not move or delete production data as part of repository separation. Runtime databases, secrets, settings, backups and user uploads are not source-control content.

## 3. Architecture baseline

- **Host:** ERMS ASP.NET Core 8 application.
- **Module shape:** FCR module/class library hosted in-process by ERMS.
- **Authentication:** Reuse authenticated ERMS identity; no separate FCR login.
- **Authorization:** ERMS controls module access; FCR enforces FCR roles server-side.
- **Roles:** FCR Admin and FCR User.
- **UI:** ERMS Admin integrates module screens/navigation into ERMS UI.
- **Data:** Preserve existing FCR records. Migrations must be backed up, tested and reversible.
- **Archive:** Numbered FCR folders and attachment storage use configured ERMS/server storage with path validation and access controls.

## 4. Functional requirements

### Submitter
- Submit Title, Justification, Description and allowed attachments.
- Track own requests and status.

### Workflow
- Configurable sequential/parallel reviewer and approver assignment.
- Record decisions, timestamps, history and audit metadata.
- Business rules for rejection, revision, reassignment and cancellation require UAT confirmation.

### FCR Admin
- Assign official FCR number, category, contractor/subcontractor and required metadata.
- Archive request form and attachments under the FCR-number folder.
- Support retry/recovery and auditable folder/file operations.

### Audit and reporting
- Keep server-side authorization, audit trail and reports.
- Validate report accuracy and retention policy during UAT.

## 5. Delivery phases and status

| Phase | Scope | Current status |
|---|---|---|
| 0 | ERMS integration discovery | Findings documented; actual ERMS host conventions still need validation |
| 1 | .NET 8 module foundation | Implemented and merged; build/tests pass |
| 2 | ERMS identity and authorization | FCR-side mapping/gates/tests implemented; actual ERMS claim and host access gate remain external validation |
| 3 | Submitter form and tracking | Implemented/tested |
| 4 | Review/approval workflow | Implemented/tested; business behavior needs UAT sign-off |
| 5 | Admin metadata and archive | Implemented/tested; server storage/service account requires ERMS-side validation |
| 6 | ERMS UI integration | External ERMS Admin work; ERMS source/UI is not in this repository |
| 7 | Security, audit, reports and UAT | Code/tests exist; business UAT, report accuracy and retention signoff remain |
| 8 | Data migration/production readiness | Backup, migration and rollback rehearsal pending; production cutover requires explicit approval |

## 6. Completed .NET work

- Hostable .NET 8 module/library and ERMS integration contract.
- External ERMS identity mapping and FCR role authorization.
- Submitter Title/Justification/Description, attachment handling and tracking.
- Workflow definitions, reviewer assignment, decisions and history.
- Admin FCR metadata and numbered-folder archive with request JSON/HTML and attachment copies.
- Archive retry/immutability behavior and attachment path safety.
- Audit/report endpoints and authorization regression tests.
- Fake ERMS-host integration tests (mapped identity, unauthorized access, anonymous 401).
- ERMS module UAT checklist.

## 7. External UAT / production gates

- ERMS Admin wires FCR navigation and screens into actual ERMS UI.
- Configure/verify actual stable ERMS identity claim and module access gate.
- Verify archive share permissions using ERMS service account.
- Confirm database storage, backup and concurrency strategy. Do not use SQLite on unsupported shared/network storage.
- Validate workflow routing, report accuracy, audit retention and security.
- Rehearse backup/migration/rollback on a copy.
- **No production deployment/cutover is authorized by this plan.**

## 8. Working protocol

- Keep .NET work in this repository and Python work in dibaah/fcr-python.
- Preserve existing data and keep runtime/local config out of Git.
- Use reviewable PRs, tests and small changes.
- Do not ask the user to restate confirmed requirements.
- Production deployment/cutover remains a separate explicit approval gate.
