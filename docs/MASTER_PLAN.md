# FCR inside ERMS — Master Plan

**Status:** Planning baseline updated  
**Target host:** Existing ERMS ASP.NET Core 8 application  
**Integration decision:** FCR will run inside the same ASP.NET Core application as a module/library. It will not be deployed as a separate FCR web application with SSO.

## 1. Product goal

Deliver FCR as an integrated ERMS module. ERMS remains the application shell and owns the user experience template, authentication, and primary access gate. FCR owns its domain workflows, records, audit trail, and FCR-specific permissions.

Users enter ERMS, select FCR from the dashboard/navigation, and use FCR without a separate login.

## 2. Architecture baseline

- **Host:** ERMS ASP.NET Core 8 application.
- **Module shape:** FCR module/class library hosted in-process by ERMS (modular-monolith approach).
- **Authentication:** Reuse the authenticated ERMS identity; no standalone FCR login.
- **Authorization:** ERMS Admin controls whether a user may access the FCR module. FCR additionally enforces FCR-specific roles and permissions server-side.
- **Roles:** FCR Admin and FCR User, with the FCR Admin role including the project owner/admin as configured. Do not rely on UI hiding as authorization.
- **UI:** ERMS Admin integrates FCR screens into the ERMS UI template. FCR should provide module functionality/API and integration guidance; standalone UI is not the primary target.
- **Framework compatibility:** Target .NET 8 compatibility for integration. The existing repository may currently target a newer framework; verify and align before host integration.
- **Data:** Preserve existing FCR records and database history. Any schema conversion/migration must be backed up, tested, and reversible.
- **Files:** FCR-number-based archive folders and attachment storage must be implemented against the actual ERMS/server storage configuration, with path validation and access controls.

## 3. Functional requirements

### 3.1 FCR access and roles
- ERMS Admin grants/revokes access to the FCR module.
- FCR Admin manages FCR-specific permissions/roles where authorized.
- FCR User can perform only permitted FCR actions.
- All protected operations are authorized on the server.

### 3.2 Submitter
- Create an FCR using the initial required fields:
  - Title
  - Justification
  - Description
- Submit the FCR into the configured workflow.
- Upload attachments where allowed.

### 3.3 Workflow and approvals
- ERMS Admin/configured workflow administrator defines routing after submission.
- Workflow supports configurable reviewers/approvers and sequential signing.
- Each step records assigned reviewer, decision, timestamp, and relevant audit metadata.
- Pending work remains pending until the appropriate step is completed.
- Define behavior for rejection, return-for-revision, reassignment, and cancellation before implementation.

### 3.4 FCR Admin processing
- Submitted FCR reaches the designated FCR Admin/processing queue.
- FCR Admin assigns the official FCR number, category, contractor/subcontractor, and other required administrative metadata.
- FCR number drives creation/resolution of the archive folder.
- Save the FCR form and its attachments in the appropriate folder.
- Folder/file actions must be safe, permission-controlled, and auditable.

### 3.5 Audit and reports
- Retain audit logging and reporting work already implemented, subject to review against ERMS identity and authorization.
- Reports must use accurate workflow/business data and be validated during UAT.
- Retention policy and report sign-off remain explicit acceptance items.

## 4. Delivery phases

### Phase 0 — Integration discovery and compatibility
- Inspect ERMS hosting conventions, module registration, identity model, role/claim source, DI, routing, and deployment constraints.
- Inspect FCR repository structure, current .NET target, database provider/schema, migrations, workflow implementation, and tests.
- Confirm the supported .NET 8 dependency set and integration contract.
- Produce a concise integration design before implementation.

**Exit gate:** Host/module contract, identity mapping, data strategy, and storage strategy are documented and agreed.

### Phase 1 — Module foundation
- Refactor FCR into a hostable module/library without a separate application startup/login.
- Provide clear registration/DI and endpoint integration for ERMS.
- Keep standalone/demo hosting only if useful for development and tests; it must not become the production integration model.
- Establish .NET 8 build and automated tests.

**Exit gate:** Module builds and runs within an ERMS-like .NET 8 host.

### Phase 2 — ERMS identity and authorization
- Map ERMS identity into FCR.
- Implement ERMS-level FCR access gate and FCR Admin/User authorization.
- Add authorization tests for anonymous, ERMS-ineligible, FCR User, and FCR Admin scenarios.

**Exit gate:** Access is enforced server-side and role tests pass.

### Phase 3 — Core submitter workflow
- Implement/verify Title, Justification, Description submission.
- Persist submissions and attachments metadata.
- Add submitter view/status and tests.

**Exit gate:** A permitted user can submit and track an FCR end-to-end.

### Phase 4 — Configurable review and sequential approval
- Implement workflow configuration and reviewer assignment.
- Support sequential approvals, pending queues, decisions, and workflow history.
- Specify and test rejection/return/reassignment/cancellation behavior.

**Exit gate:** Workflow scenarios pass automated tests and business review.

### Phase 5 — FCR Admin processing and archive
- Implement FCR Admin processing queue.
- Capture official number, category, contractor/subcontractor, and required metadata.
- Create/resolve the numbered folder and save form plus attachments.
- Validate storage permissions, filename/path safety, and failure recovery.

**Exit gate:** A processed FCR and its attachments are correctly archived and retrievable.

### Phase 6 — ERMS UI integration
- Integrate module routes/API and screens into ERMS-owned UI template.
- Confirm navigation, layout, and interaction with ERMS conventions.
- UI styling is owned by ERMS integration, not a standalone FCR redesign.

**Exit gate:** ERMS users can navigate and complete FCR tasks inside the host app.

### Phase 7 — Security, audit, reports, and UAT
- Complete authorization regression tests, audit verification, report validation, and security review.
- Confirm audit retention policy and report accuracy sign-off.
- Complete business UAT and document outstanding risks.

**Exit gate:** Security/UAT approvals recorded. Existing audit/report work alone does not mean Phase 7 is accepted.

### Phase 8 — Data migration and production readiness
- Back up existing data and validate migration on a copy.
- Reconcile record counts, identifiers, relationships, attachments, and folder links.
- Prepare rollback, deployment, monitoring, and operational support steps.
- Production cutover requires explicit approval; it is not authorized by this plan.

**Exit gate:** Migration/UAT/rollback checklist approved and cutover explicitly authorized.

## 5. Current known status (based on prior repository work; verify against current branch/CI before relying on it)

- Phase 7 audit/reporting implementation and related migration/tests were previously added.
- A security/UAT checklist and anonymous-access regression test were previously proposed in an open PR.
- Some security, UAT, report accuracy, and audit-retention acceptance items remain unconfirmed.
- Phase 1 has retargeted the .NET solution and CI to .NET 8 and added the hostable `FCR.Module` entry points. PR #5 CI (build + tests) passed; it is ready for review but not yet merged.
- Phase 2 implementation is in PR #7: external ERMS subject mapping, FCR User role, and server-side module access policy; .NET 8 CI passed. Actual ERMS host claim configuration and end-to-end integration still require ERMS-side verification.
- FCR module registration/endpoint mapping exists, but actual ERMS host integration and ERMS-side access gate/UI template are not verified.
- ERMS identity/role mapping is being implemented in Phase 2; end-to-end host verification remains required.
- Configurable sequential workflow and server archive integration are not considered complete until demonstrated in the actual host.

## 6. Decisions and assumptions

**Confirmed**
- FCR is hosted in the same ASP.NET Core application as ERMS via module/library.
- ERMS owns the shell/UI template and primary authentication/access gate.
- FCR-specific Admin/User roles and server-side checks are still required.
- Existing FCR data must be preserved.

**To verify during Phase 0 (do not repeatedly ask unless a real blocker emerges)**
- ERMS identity key and role/claim conventions.
- Database engine/provider and preferred module data-access boundary.
- Whether workflow configuration is managed by ERMS or by FCR Admin UI/API.
- Archive root, server service account permissions, and attachment size/type policy.
- Exact rejection/return/reassignment/cancellation rules.
- Required fields beyond the initial submitter fields and admin metadata.

## 7. Working protocol

- Use this plan as the shared project baseline in subsequent work.
- Do not ask the user to restate confirmed requirements.
- Ask only one focused question when an unresolved decision blocks the next concrete step; otherwise inspect the repository/ERMS integration materials and make progress.
- Work in small, testable phases and report what changed, tests, remaining blockers, and next step.
- Preserve backups and use PRs for reviewable changes.
- Do not merge/deploy/cut over production without explicit approval.
