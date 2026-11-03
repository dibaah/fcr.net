# Phase 7–8 Gap Analysis (Repository Review)

Reviewed against `masterplan.md` on the `review-phase7-phase8-completion` branch.

## Current evidence

- Phase 6 workflow implementation exists in `RequestService`, `RequestEndpoints`, migration `AddPhase6ReviewWorkflow`, and `WorkflowTests`.
- Workflow decisions, comments, cycles, assignments, and notifications are persisted.
- Review history is available through `GET /api/requests/{id}/history`.
- Existing deployment assets include `dotnet/deploy/deploy.sh`, a systemd unit, and `docs/deployment.md`.
- Current GitHub Actions workflow runs Python tests and builds the Python EXE; it does not run `dotnet restore/build/test`.

## Phase 7 — not yet accepted as complete

| Master Plan item | Evidence / gap |
|---|---|
| Audit logging | Review history is persisted in `fcr_reviews`, but a general `FCRAuditLogs` store for admin/user/system actions is not present. |
| Workflow reporting | Request status/history APIs exist; dedicated reporting endpoints are not present. |
| User activity history | No general user activity audit log found. |
| Pending review reports | Reviewer queue exists at `GET /api/reviews/mine`; admin-wide pending-review reporting remains to be added. |
| Approval statistics | Existing FCR technical statistics are available; workflow approval metrics are not implemented as dedicated report APIs. |
| Dashboard integration | Existing frontend is served; requester/admin/reviewer frontend integration needs explicit end-to-end validation. |
| Status separation | Request/workflow/FCR technical status are separate in the data model; tests should validate transitions and exports together. |
| Report/export parity | Existing technical Excel exports exist; workflow reporting/export acceptance remains outstanding. |
| Security testing | Auth tests exist, but production security acceptance (CSRF, upload content validation, secrets, SSO, threat review) remains outstanding. |
| UAT | No business acceptance evidence is committed. |

## Phase 8 — production cutover is intentionally blocked

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
