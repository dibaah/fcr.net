# Phase 7 Security Review and Business UAT

## ERMS module identity and role checks (Phase 2)

- [ ] ERMS host uses the correct stable subject claim (`Fcr:Identity:ExternalIdClaim`).
- [ ] An ERMS user mapped to `FCR User` can access permitted FCR module routes.
- [ ] An ERMS user mapped to `FCR Admin` can manage FCR identity mappings and roles.
- [ ] An authenticated ERMS user with no FCR mapping/role receives 403 on protected FCR routes.
- [ ] Unmapped and inactive users cannot reuse upstream role claims (including `FCR Admin` / `System Administrator`) to bypass FCR authorization.
- [ ] FCR role changes and user deactivation take effect on the next request.
- [ ] ERMS FCR access permission gate denies module/API access before the user is permitted into FCR.
- [ ] Module startup/migration path does not create the standalone local development admin account.


This checklist is an acceptance aid, not evidence that production UAT has already passed.

## Security review

- [x] Admin report routes are grouped under the `AdminConsole` authorization policy.
- [x] Automated regression test: anonymous access to admin reports returns HTTP 401.
- [ ] Automated test: authenticated requester/reviewer without an admin role receives HTTP 403.
- [ ] Confirm production identity provider and role mapping (AD/Entra decision remains open).
- [ ] Confirm cookie settings behind production HTTPS: Secure, HttpOnly, SameSite, expiration, and proxy configuration.
- [ ] Confirm audit log table permissions prevent application users from altering/deleting audit records outside approved retention/admin procedures.
- [ ] Define audit retention, archival, access review, and deletion policy with IT/security.
- [ ] Review log access and export process with IT/security.
- [ ] Verify audit logs do not capture credentials, request bodies, or query-string values.
- [ ] Verify audit logging under DB failure/concurrency and agree whether fail-open behavior is acceptable.

## Business UAT

Run using a non-production environment and approved test accounts. Record tester, date, environment, test data/reference, expected result, actual result, and evidence.

- [ ] Requester submits a new FCR and confirms request number/status.
- [ ] Reviewer sees the assigned item in pending review and confirms stage/reviewer accuracy.
- [ ] Reviewer approves a request; approval statistics and workflow summary update correctly.
- [ ] Reviewer rejects a request; status and report counts update correctly.
- [ ] Reviewer returns a request for correction; requester can identify the required correction.
- [ ] Pending-review report excludes completed/cancelled assignments (confirm intended workflow semantics).
- [ ] Review-aging report uses the agreed aging basis/time zone and threshold.
- [ ] User-activity report shows expected user/action/time/status and is limited to authorized admins.
- [ ] Compare report counts against source records for a known test set.
- [ ] Confirm report page usability, labels, filters, and refresh behavior with business users.
- [ ] Confirm export requirements and validate exported data, if export is in scope.

## Acceptance / sign-off

- Business owner: __________________
- IT/security reviewer: __________________
- UAT date/environment: __________________
- Open defects / decisions: __________________
- Outcome: [ ] Accepted [ ] Accepted with actions [ ] Rejected

**Phase 7 is not fully accepted until required security items and business UAT are signed off.**
