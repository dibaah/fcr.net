# Phase 7–8 Gap Analysis

Reviewed against docs/MASTER_PLAN.md and the current .NET module implementation.

## Current evidence

- Phase 6 workflow implementation exists in RequestService, RequestEndpoints, migration AddPhase6ReviewWorkflow, and WorkflowTests.
- Workflow decisions, comments, cycles, assignments, and notifications are persisted.
- Review history is available through GET /api/requests/{id}/history.
- .NET CI is active in .github/workflows/dotnet.yml. It restores, builds, and tests the .NET 8 solution; CI materializes the Python legacy database and static assets as pinned fixtures only.
- Main commit 0c4f1f0d passed CI with 76/76 tests. Subsequent security/authorization changes are in PR #27 and require their own green CI before acceptance.

## Phase 7 — implementation present; acceptance evidence still required

| Master Plan item | Evidence / gap |
|---|---|
| Audit logging | fcr_audit_logs migration and middleware record mutating API metadata (user, action/path, status, request ID, UTC timestamp). Request bodies/query strings are excluded. |
| Workflow reporting | Admin-only workflow-summary, approval-statistics, pending-reviews, review-aging, and user-activity endpoints exist. |
| User activity history | Backed by audit log; recent activity can be filtered by user and limited to 500. |
| Pending review reports | Admin report endpoint and static report page exist. Actual ERMS UI wiring is external. |
| Approval statistics | Counts by review action and approved/rejected/returned workflow totals exist. |
| Dashboard integration | Standalone static frontend is a legacy/test fixture; requester/admin/reviewer screens need end-to-end validation in the actual ERMS UI. |
| Status separation | Request/workflow/FCR technical statuses are separate; transitions and report/export consistency still need business validation. |
| Report/export parity | Legacy Excel export exists; workflow reporting/export acceptance remains outstanding. |
| Security testing | Automated tests cover authentication, authorization, identity mapping, role separation, and audit routes. CSRF/anti-forgery, upload content validation/scanning, production secrets/SSO, and threat review remain to be accepted by IT/security. |
| UAT | No business acceptance evidence is committed. |

## Phase 7 validation status

Phase 7 is **not accepted as complete** until current .NET CI is green and business/security UAT validates report correctness, permissions, audit retention, attachment handling, and ERMS dashboard integration.

## Phase 8 — production cutover is intentionally blocked

The deployment script and rollback guide are operational aids, not proof of production readiness. The Master Plan explicitly requires company IT/business approval and prohibits production deployment without explicit authorization.

Before cutover:

1. Confirm approved .NET runtime and host OS.
2. Confirm production database provider and multi-user concurrency strategy (SQLite is currently configured).
3. Confirm production identity provider (AD/Entra) and ERMS stable claim.
4. Configure HTTPS/TLS, proxy trust, and secrets outside source control.
5. Confirm archive storage, service-account permissions, and backup retention.
6. Run .NET build/test and security tests in CI.
7. Rehearse database + attachment backup and restore.
8. Test concurrency on the approved database.
9. Reconcile data/attachment counts and workflow records.
10. Obtain IT/security and business UAT approval.
11. Schedule cutover and retain rollback assets.

## Recommended remaining technical work

1. Validate CSRF/anti-forgery behavior for cookie-authenticated mutating routes in the actual ERMS host.
2. Validate file upload content type, malware scanning, and archive/download policies with IT/security.
3. Confirm audit failure behavior and retention requirements.
4. Complete end-to-end ERMS identity, UI, workflow, and archive-share tests.
5. Revisit production database provider after IT concurrency/storage decision.

## Validation limitation

Repository source was reviewed through GitHub. The current execution environment does not provide a local .NET SDK; CI is the authoritative build/test evidence. Do not treat source inspection alone as passing evidence.
