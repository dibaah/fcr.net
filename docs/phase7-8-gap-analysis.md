# Phase 7–8 Gap Analysis (Repository Review)

Reviewed against `masterplan.md` on the `review-phase7-phase8-completion` branch.

## Current evidence

- Phase 6 workflow implementation exists in `RequestService`, `RequestEndpoints`, migration `AddPhase6ReviewWorkflow`, and `WorkflowTests`.
- Workflow decisions, comments, cycles, assignments, and notifications are persisted.
- Review history is available through `GET /api/requests/{id}/history`.
- Existing deployment assets include `dotnet/deploy/deploy.sh`, a systemd unit, and `docs/deployment.md`.
- Current GitHub Actions workflow runs Python tests and builds the Python EXE; it does not run `dotnet restore/build/test`.

## Phase 7 — implementation added; acceptance evidence still required

| Master Plan item | Evidence / gap |
|---|---|
| Audit logging | Added `fcr_audit_logs`, EF mapping/migration, and middleware that records mutating API metadata (user, action/path, status, request ID, UTC timestamp). Request bodies/query strings are intentionally excluded. |
| Workflow reporting | Added admin-only `/api/admin/reports/workflow-summary`, `/approval-statistics`, `/pending-reviews`, `/review-aging`, and `/user-activity` endpoints. |
| User activity history | Backed by audit log; recent activity can be filtered by user and limited (max 500). |
| Pending review reports | Added admin report endpoint and static admin report page `static/phase7-reports.html`. |
| Approval statistics | Added counts by review action and approved/rejected/returned workflow totals. |
| Dashboard integration | Existing frontend is served; requester/admin/reviewer frontend integration needs explicit end-to-end validation. |
| Status separation | Request/workflow/FCR technical status are separate in the data model; tests should validate transitions and exports together. |
| Report/export parity | Existing technical Excel exports exist; workflow reporting/export acceptance remains outstanding. |
| Security testing | Auth tests exist, but production security acceptance (CSRF, upload content validation, secrets, SSO, threat review) remains outstanding. |
| UAT | No business acceptance evidence is committed. |

## Phase 7 validation status\n\nImplementation is now present in the review branch, but Phase 7 is **not accepted as complete until the .NET CI run passes and business/security UAT validates report correctness, permissions, audit retention, and dashboard integration**. The report page is accessible at `/phase7-reports.html`; its APIs enforce the AdminConsole role server-side.\n\n## Phase 8 — production cutover is intentionally blocked

The repo has a Phase 3 deployment script and rollback notes, but that is not proof of production cutover. The Master Plan explicitly requires company IT/business approval and prohibits production deployment without explicit authorization.

Before cutover:
1. Confirm approved .NET runtime and host OS.
2. Confirm production database provider (SQLite is currently configured).
3. Confirm production identity provider (local cookie login is development-only).
4. Configure HTTPS/TLS and secrets outside source control.
5. Confirm central attachment storage and backup retention.
6. Run .NET build/test and security test suite in CI.
7. Perform database + attachment backup and restore rehearsal.
8. Test multi-user concurrency on the approved database.
9. Verify data/attachment counts and workflow records.
10. Obtain IT + business UAT approval.
11. Schedule cutover and retain rollback assets.

## Recommended next technical tasks

1. Add general audit entity, migration, service, and backend write points (request submission/admin edits, attachment actions, workflow config/submit/resubmit/decision, user/role changes, FCR CRUD).
2. Add admin report endpoints: pending reviews, approval/rejection/return counts, review aging, and user activity.
3. Add report tests and authorization tests.
4. Add .NET restore/build/test to CI.
5. Update deployment docs with the final production provider and identity configuration after IT decisions.

## Validation limitation

This repository review was performed against GitHub source. The current environment did not have the .NET SDK, so no local .NET build or test execution was possible. Do not treat source inspection alone as passing test evidence.
