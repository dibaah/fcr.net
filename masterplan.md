# Master Implementation Plan
## FCR Dashboard Migration to .NET and Development of ERMS/FCM Workflow Module

**Document Status:** Proposed – Pending User Approval  
**Current Application:** FCR Dashboard v2.0.0  
**Target Platform:** ASP.NET Core Web Application  
**Target Framework:** .NET 10 LTS (subject to company IT approval)  
**Initial Database:** SQLite (subject to production deployment assessment)  
**Deployment Model:** Centralized Web Application

---

# 1. Project Overview

## 1.1 Current Situation

The existing FCR Dashboard is a locally operated desktop application developed using Python 3.11, FastAPI, SQLite, HTML, CSS, and JavaScript.

It provides FCR record management, status monitoring, reporting, Excel exports, charts, notifications, backup, and other administrative capabilities.

The application was initially developed to improve the user's FCR monitoring and management activities.

The next objective is to migrate the existing application to .NET and gradually transform it into an organizational FCR Management Module within a broader ERMS (Engineering Request Management System) / FCM (Facility Change Management System).

## 1.2 Target Objective

The target application must provide two integrated capabilities:

1. **FCR Technical Database and Monitoring:** Preserve and improve all existing FCR Dashboard functionality.
2. **FCR Request and Approval Workflow:** Introduce a multi-user organizational workflow that allows employees to submit FCR requests, FCR Admin to manage and complete them, and designated reviewers to review and approve or reject them.

The new application must be designed as a centralized web application accessible through a browser by authorized users.

## 1.3 Main Design Principles

- Preserve all verified existing application functionality.
- Do not lose or overwrite existing FCR records.
- Separate initial FCR request submission from the complete FCR administrative record.
- Implement role-based access control.
- Support both sequential and parallel review workflows.
- Maintain a complete history of submissions, reviews, decisions, comments, and administrative actions.
- Keep the architecture modular to support future ERMS integration.
- Do not assume the final company database, authentication provider, or infrastructure without IT approval.
- Do not begin implementation until the final plan has been approved.

---

# 2. Target Business Workflow

## 2.1 User Roles

The initial system should support the following roles.

| Role | Main Responsibilities |
|---|---|
| Requester | Submit a new FCR request with its subject, justification, description, and optional attachments. |
| FCR Admin | Receive submitted requests, review and complete administrative information, assign FCR numbers, manage the workflow, and coordinate returned or rejected requests. |
| Reviewer | Review assigned FCR requests, provide comments, approve, or reject. |
| Approver | Provide formal approval where the configured workflow requires a designated approval authority. |
| System Administrator | Manage users, roles, permissions, workflow configurations, and system settings. |

A user may have more than one role if authorized.

The system must not rely on frontend visibility alone for access control. All permissions must also be enforced by the backend.

## 2.2 Workflow – Initial Request Submission

A Requester logs into the centralized application and opens the FCR Request Submission page.

The initial form must be simple and must not require completion of the full existing FCR Dashboard form.

Required information:

- Subject
- Justification
- Description

Optional information:

- Attachments

After submission:

1. The system generates a unique Request ID.
2. The request is stored in the database.
3. The initial workflow status becomes **Submitted**.
4. The Requester receives a submission confirmation.
5. The request appears in the FCR Admin dashboard.

The Requester must not be required to enter an FCR Code, Category, PEDEC Status, Execution Status, or other administrative fields during initial submission.

## 2.3 Workflow – FCR Admin Processing

The FCR Admin receives the submitted request in a dedicated dashboard.

The Admin can:

- View the submitted subject, justification, and description.
- Open and download submitted attachments.
- Review the request.
- Add administrative comments.
- Complete the technical and administrative information.
- Assign the FCR Number / Code.
- Select the Category and other relevant fields.
- Configure the appropriate review workflow.
- Assign reviewers and approvers.
- Submit the request for review.
- Manage returned or rejected requests.

The Admin must be able to save a request as a draft before initiating the review process.

The existing FCR technical database fields must remain available to the Admin.

## 2.4 Workflow – Sequential and Parallel Review

The system must support two review modes.

### A. Sequential Review

In sequential mode, reviewers receive the request in a predefined order.

Example:

1. Reviewer A receives the request.
2. Reviewer A submits a decision.
3. If approved, the request proceeds to Reviewer B.
4. Reviewer B reviews and submits a decision.
5. The workflow continues according to the configured approval sequence.

The next reviewer must not receive the approval task before the preceding required review is completed.

### B. Parallel Review

In parallel mode, all assigned reviewers receive the request at the same workflow stage.

Each reviewer can independently:

- Approve.
- Comment.
- Reject.

The system must track the decision of every assigned reviewer separately.

The workflow must support configurable decision rules, such as:

- All assigned reviewers must approve.
- A specified number of approvals is sufficient.
- A designated approver provides the final decision.

The final approval rule must be configurable and must not be hardcoded before the company's approval requirements are confirmed.

### C. Workflow Configuration

The FCR Admin must be able to select the review mode for each FCR request.

The system should also support configurable reviewer groups, approval sequences, and decision rules.

Workflow configuration should be maintained separately from individual FCR records.

## 2.5 Workflow – Comments and Decisions

Comments and formal decisions must be stored as separate records.

Each review action must include:

- FCR Request ID.
- Reviewer User ID.
- Reviewer Name.
- Action Type.
- Comment.
- Decision Date and Time.
- Workflow Stage.
- Previous Status.
- Resulting Status.

A reviewer must be able to submit a comment without necessarily approving or rejecting the request.

The system must preserve previous comments and decisions. Users must not be able to silently overwrite historical review records.

## 2.6 Workflow – Rejection and Return for Correction

Based on the selected business process, rejected or returned requests must be routed back to the **FCR Admin dashboard**.

The workflow must not automatically transfer ownership back to the Requester.

When a reviewer rejects or requests correction:

1. The review decision and comment are recorded.
2. The workflow stage is updated.
3. The request is returned to the FCR Admin.
4. The Admin receives a notification.
5. The Admin reviews the comments and determines the necessary corrective action.
6. The Admin can update the administrative information and resubmit the request.
7. The new review cycle must retain the previous review history.

The system should distinguish between:

- **Returned for Correction:** The request requires changes before further review.
- **Rejected:** The reviewer does not approve the request.

Both actions return the request to the FCR Admin, but their business meaning and reporting must remain distinguishable.

If clarification from the original Requester is needed, the Admin may coordinate it through comments or a future clarification function. Direct automatic reassignment to the Requester is not part of the initial workflow.

## 2.7 Workflow – Completion

After all required reviews and approvals are completed:

1. The workflow is marked as approved.
2. The FCR Admin receives the final notification.
3. The FCR record is available for continued technical and administrative management.
4. The workflow history is retained.
5. The existing FCR lifecycle and execution tracking remain available.

Approval of the request must not automatically mean that the facility modification has been executed or closed.

Workflow approval, FCR status, and execution status must remain separate concepts.

---

# 3. Target Application Architecture

## 3.1 Deployment Architecture

The target system must be a centralized ASP.NET Core Web Application.

All authorized users access the application through a browser.

The system must consist of:

1. Central ASP.NET Core backend.
2. Central database.
3. Central attachment storage.
4. Web frontend.
5. Authentication and authorization layer.
6. Background services for notifications, scheduled backups, and exports.

The existing desktop launcher and local Edge application-mode architecture must not be the target architecture for the organizational ERMS module.

The system should be deployable on a company-approved Windows or Linux server environment.

## 3.2 Proposed Solution Structure

Create a modular Visual Studio solution.

### Project 1 – FCR.Domain

Responsibilities:

- FCR entities.
- FCR Request entities.
- User and Role entities.
- Workflow entities.
- Review and Comment entities.
- Attachment metadata.
- Audit log entities.
- Domain enums and value objects.
- Business validation rules.

No direct database or HTTP dependencies.

### Project 2 – FCR.Application

Responsibilities:

- Application services.
- Use cases.
- DTOs.
- Validation.
- Authorization policies.
- Workflow orchestration.
- FCR management operations.
- Review and approval operations.
- Notification coordination.

### Project 3 – FCR.Infrastructure

Responsibilities:

- Entity Framework Core.
- Database providers.
- Repository implementations where justified.
- File storage.
- Excel export.
- Backup implementation.
- Email/notification integrations.
- Authentication integrations where applicable.

### Project 4 – FCR.Web

Responsibilities:

- ASP.NET Core Web Application.
- REST API endpoints.
- User interface.
- Authentication.
- Authorization.
- Dashboard pages.
- Static frontend assets.
- OpenAPI/Swagger in approved environments.

### Project 5 – FCR.Tests

Responsibilities:

- Unit tests.
- Integration tests.
- Workflow tests.
- Database migration tests.
- API tests.
- Authorization tests.
- Data preservation tests.

### Frontend

The existing HTML, CSS, and JavaScript frontend can be retained as the starting point for the current FCR Dashboard.

However, new pages and components must be developed for:

- Requester Dashboard.
- FCR Submission Form.
- FCR Admin Dashboard.
- Reviewer Dashboard.
- Approval and Review Details.
- Workflow History.
- User and Role Administration.

A complete frontend framework rewrite is not mandatory at this stage.

The architecture must allow a future frontend modernization without requiring changes to the business rules and database model.

---

# 4. Database Architecture

## 4.1 Existing Database Preservation

The existing `fcr_database.db` must be preserved.

The existing FCR table contains the following 22 columns:

- id
- row_num
- fcr_subject
- category
- fcr_code
- date
- review_date
- originator
- action_party
- level
- session_code
- pedec_status
- fcr_status
- execution_status
- closed_date
- tr
- next_action_by
- arch_loc
- last_update
- remarks
- created_at
- updated_at

The existing `import_log` and `meta` tables must also be preserved.

The verified current schema version is 2.

No existing column may be removed or renamed during the initial migration without explicit approval.

The actual database schema must be inspected directly before implementation. Do not rely exclusively on documentation or assumptions.

## 4.2 Proposed New Tables

The following tables are proposed for the new workflow capabilities.

### Users

Stores user accounts and identity information.

Suggested fields:

- id
- username
- display_name
- email
- is_active
- created_at
- updated_at

### Roles

Stores system roles.

Examples:

- Requester
- FCR Admin
- Reviewer
- Approver
- System Administrator

### UserRoles

Maps users to roles.

### FCRRequests

Stores the initial submitted request.

Suggested fields:

- id (UUID)
- request_number
- requester_user_id
- subject
- justification
- description
- submission_status
- submitted_at
- created_at
- updated_at
- linked_fcr_id (nullable)

The Request ID must exist independently of the FCR Code.

### FCRWorkflowInstances

Stores the workflow instance associated with an FCR request.

Suggested fields:

- id
- fcr_request_id
- workflow_status
- review_mode
- current_stage
- started_at
- completed_at
- created_at
- updated_at

### FCRWorkflowStages

Stores the configured workflow stages and their order.

Suggested fields:

- id
- workflow_instance_id
- stage_name
- stage_order
- decision_rule
- stage_status

### FCRReviewAssignments

Stores reviewer assignments.

Suggested fields:

- id
- workflow_stage_id
- assigned_user_id
- assignment_status
- assigned_at
- completed_at

### FCRReviews

Stores individual review decisions.

Suggested fields:

- id
- review_assignment_id
- reviewer_user_id
- decision
- comment
- previous_status
- resulting_status
- created_at

### FCRComments

Stores comments independently from formal decisions.

Suggested fields:

- id
- fcr_request_id
- user_id
- comment_text
- comment_type
- created_at

### FCRAttachments

Stores attachment metadata.

Suggested fields:

- id
- fcr_request_id
- uploaded_by_user_id
- original_file_name
- stored_file_name
- storage_location
- content_type
- file_size
- uploaded_at

Attachments must be associated with the Request ID before an FCR Code is assigned.

### FCRAuditLogs

Stores a historical record of important system activities.

Suggested fields:

- id
- entity_type
- entity_id
- action
- old_values
- new_values
- performed_by_user_id
- performed_at

### WorkflowDefinitions

Stores configurable workflow templates.

Suggested fields:

- id
- workflow_name
- review_mode
- configuration
- is_active
- created_at
- updated_at

The exact schema and relationships must be finalized during database design.

## 4.3 Relationship Between FCR Request and FCR Record

The initial FCR Request and the existing FCR technical record must be logically separated.

A Request ID is generated immediately upon submission.

The FCR Code is assigned later by the FCR Admin.

When the Admin creates or links the formal FCR record:

- The Request ID remains unchanged.
- The formal FCR record is linked to the request.
- Existing attachments remain associated with the request.
- The complete request and review history remain accessible.
- No duplicate request or attachment records are created unnecessarily.

The implementation must define a consistent one-to-one or approved one-to-many relationship based on the final business rules.

## 4.4 Database Provider Strategy

SQLite is the existing database and can be used for initial development and migration testing.

However, the production database provider must be confirmed by company IT.

Potential providers include:

- Microsoft SQL Server.
- PostgreSQL.
- Azure SQL.
- SQLite, if approved for the production deployment model.

Do not assume that changing only the connection string will make the application fully compatible with another database provider.

Provider-specific migrations, indexes, concurrency behavior, data types, and queries must be tested.

The application should isolate database-specific implementation details through Infrastructure.

For a centralized multi-user production deployment, database concurrency and backup requirements must be evaluated before selecting SQLite as the permanent production database.

---

# 5. Migration of Existing FCR Dashboard Functionality

All verified existing functionality must be retained.

| Existing Feature | Target Implementation |
|---|---|
| Health / About | ASP.NET Core API |
| Options and dropdowns | API + application services |
| FCR listing | API + repository/data services |
| Search and filtering | Preserved |
| Sorting | Preserved |
| Single-record CRUD | Preserved |
| Bulk save | Preserved |
| Bulk delete | Preserved |
| Status groups | Preserved |
| Dashboard statistics | Preserved |
| Folder generation | Updated for centralized storage |
| Settings | Application settings service |
| Backup | Centralized backup service |
| Backup listing | Preserved |
| Excel export | Preserved |
| Export with charts | Preserved |
| Auto backup | Background hosted service |
| Auto export | Background hosted service |
| PEDEC notifications | Preserved |
| Dark/light theme | Preserved |
| English/Chinese i18n | Preserved |
| Column presets | Preserved |
| Undo/redo | Preserved in frontend |
| Charts | Preserved |
| Demo login | Replaced by real authentication for organizational use |
| Packaging | Centralized web deployment |

The existing API routes, response shapes, status codes, and business rules must be documented before migration.

Where existing API behavior is verified, the new implementation must maintain compatibility unless an approved design change is documented.

---

# 6. FCR Status and Workflow Status Separation

The system must maintain separate status fields for different business purposes.

### FCR Technical Status

The existing fields remain:

- PEDEC Status
- FCR Status
- Execution Status

These represent the technical and administrative lifecycle of the FCR.

### Request Status

Represents the status of the initial submitted request.

Examples:

- Draft
- Submitted
- Under Admin Review
- Returned to Admin
- Under Review
- Approved
- Rejected
- Completed

### Workflow Status

Represents the state of the active review process.

Examples:

- Not Started
- In Progress
- Awaiting Review
- Awaiting Approval
- Returned for Correction
- Rejected
- Approved
- Completed
- Cancelled

These statuses must not be mixed with the existing FCR technical statuses.

The exact status transitions must be documented and tested.

---

# 7. Security and Access Control

The organizational application must implement real authentication and authorization.

The existing `demo/demo` login is only a visual demonstration gate and must not be retained as the production authentication mechanism.

Requirements:

- Centralized authentication.
- Role-Based Access Control (RBAC).
- Server-side authorization checks.
- Secure session or token management.
- Password and identity management according to company IT policy.
- Secure file upload and download.
- File type and size validation.
- Path traversal protection.
- Audit logging.
- Protection against unauthorized modification or deletion.
- HTTPS in production.
- Secure configuration and secrets management.

Potential authentication methods:

- OpenID Connect / OAuth 2.0.
- Microsoft Entra ID.
- Windows Integrated Authentication.
- Another company-approved identity provider.

The final method must be agreed with IT.

## 7.1 Role Permission Matrix

| Function | Requester | FCR Admin | Reviewer | Approver | System Admin |
|---|---|---|---|---|---|
| Submit request | Yes | Yes, if authorized | If authorized | If authorized | Yes |
| View own request | Yes | Yes | Assigned requests | Assigned requests | Yes |
| View all requests | No | Yes | No | No | Yes |
| Edit initial submission | Policy-dependent | Yes | No | No | Yes |
| Assign FCR Code | No | Yes | No | No | Yes |
| Complete technical fields | No | Yes | No | No | Yes |
| Assign reviewers | No | Yes | No | No | Yes |
| Add review comments | No | Yes | Yes | Yes | Yes |
| Approve | No | No, unless separately authorized | If assigned | If assigned | No, unless separately authorized |
| Reject | No | No, unless separately authorized | If assigned | If assigned | No, unless separately authorized |
| Manage users and roles | No | No | No | No | Yes |

The final permission matrix must be approved by the organization.

---

# 8. Attachment Management

The existing local folder structure must be reviewed and adapted for centralized storage.

Requirements:

- Support multiple attachments per request.
- Generate unique stored file names.
- Preserve original file names as metadata.
- Associate files with Request ID.
- Allow authorized users to download attachments.
- Validate file extensions and content types.
- Enforce file-size limits.
- Prevent unauthorized access.
- Maintain attachment history where files are replaced or removed.
- Avoid exposing physical server paths to users.

After an FCR Code is assigned, the system may organize attachments into a logical FCR folder structure, but the Request ID must remain the permanent reference.

The storage implementation must be independent of the database provider.

---

# 9. Notifications

The notification module must support both existing and new requirements.

Existing notifications:

- PEDEC overdue notifications.

New workflow notifications:

- New request submitted.
- Request received by FCR Admin.
- Request assigned to reviewer.
- Review pending.
- Comment added.
- Request returned for correction.
- Request rejected.
- Request approved.
- Workflow completed.

The initial implementation may use in-application notifications.

Email notifications can be added through a company-approved SMTP or notification service.

Notifications must be generated based on actual workflow events and user assignments.

---

# 10. Phased Implementation Roadmap

Implementation must be carried out in controlled stages.

## Phase 0 – Repository Inspection and Technical Baseline

Before modifying any code:

1. Inspect the complete existing Python application.
2. Inspect the actual SQLite database schema.
3. Inspect all API routes.
4. Inspect the frontend functionality.
5. Inspect settings and configuration files.
6. Inspect backup and export behavior.
7. Inspect the existing test suite.
8. Identify all dependencies and external integrations.
9. Identify any discrepancies between Python, Node.js, and other implementations if present.
10. Produce a verified technical inventory.

Deliverables:

- Existing application architecture report.
- Verified database schema.
- API inventory.
- Feature inventory.
- Migration risk register.
- Baseline test results.

No existing files or databases may be modified during this phase.

## Phase 1 – .NET Solution and Database Foundation

1. Create the Visual Studio solution.
2. Create Domain, Application, Infrastructure, Web, and Tests projects.
3. Configure the selected .NET version.
4. Configure EF Core.
5. Implement the existing FCR entities.
6. Implement the existing `import_log` and `meta` entities.
7. Create database migrations.
8. Implement repository/data access services where appropriate.
9. Prepare database migration and backup procedures.
10. Verify schema compatibility.

Use a copy of the existing database for all migration testing.

## Phase 2 – Existing FCR Dashboard Migration

Implement the current functionality in .NET.

Priorities:

1. FCR CRUD.
2. Bulk save and delete.
3. Search, filtering, and sorting.
4. Status groups and statistics.
5. Settings.
6. Folder handling.
7. Excel exports and charts.
8. Backup and restore.
9. Auto backup and auto export.
10. Notifications.
11. Frontend compatibility.

At the end of this phase, the new application must be able to perform the existing FCR Dashboard functions without losing records or functionality.

## Phase 3 – Centralized Web Deployment Foundation

1. Configure ASP.NET Core web hosting.
2. Configure centralized database connectivity.
3. Configure centralized attachment storage.
4. Configure HTTPS.
5. Configure environment-specific settings.
6. Configure logging and monitoring.
7. Implement health checks.
8. Prepare deployment and rollback procedures.
9. Test access from multiple workstations.
10. Test concurrent user operations.

The desktop launcher must not be used as the main hosting model.

## Phase 4 – Authentication and Role-Based Access Control

1. Implement authentication integration.
2. Implement user and role management.
3. Implement permission policies.
4. Implement server-side authorization.
5. Implement user session management.
6. Implement administrative access control.
7. Test unauthorized access and privilege escalation scenarios.

Use a development authentication mechanism only for controlled testing. Do not treat it as production authentication.

## Phase 5 – FCR Request Submission and Administration

Develop the initial workflow foundation.

### Requester Module

- New FCR Request form.
- Subject, Justification, and Description.
- Optional attachments.
- Submit request.
- Submission confirmation.
- Request history and status viewing.

### FCR Admin Module

- Incoming requests dashboard.
- Request details.
- Attachment management.
- Administrative comments.
- FCR Code assignment.
- Category assignment.
- Completion of existing FCR fields.
- Draft saving.
- Workflow preparation and reviewer assignment.

Test the complete submission-to-admin process before implementing approvals.

## Phase 6 – Review and Approval Workflow

1. Implement workflow definitions.
2. Implement sequential review.
3. Implement parallel review.
4. Implement reviewer assignments.
5. Implement approval rules.
6. Implement individual review decisions.
7. Implement comments.
8. Implement rejection.
9. Implement return-for-correction.
10. Route returned and rejected requests to FCR Admin.
11. Implement resubmission and subsequent review cycles.
12. Implement final approval.
13. Implement workflow history.
14. Implement notifications.

All workflow transitions must be validated on the backend.

The workflow engine must be configurable and should not depend on hardcoded reviewer names or fixed approval sequences.

## Phase 7 – Audit Trail, Reporting and Final Integration

1. Implement audit logging.
2. Implement workflow reporting.
3. Implement user activity history.
4. Implement pending review reports.
5. Implement approval statistics.
6. Integrate workflow information with the existing FCR dashboard.
7. Verify all technical and workflow statuses.
8. Verify all reports and exports.
9. Complete security testing.
10. Complete user acceptance testing.

## Phase 8 – Production Cutover

1. Take a complete backup of the existing database.
2. Perform the approved database migration.
3. Verify all existing FCR records.
4. Verify attachments and folder references.
5. Verify user accounts and permissions.
6. Verify workflow functionality.
7. Perform multi-user acceptance testing.
8. Obtain IT and business approval.
9. Deploy to production.
10. Retain the previous application and database as a rollback option until the new system is accepted.

---

# 11. Testing Strategy

Testing must cover both existing functionality and new organizational workflow capabilities.

## 11.1 Existing Application Tests

- Database schema verification.
- Existing record preservation.
- CRUD lifecycle.
- Bulk save and delete.
- FCR code validation.
- Duplicate code rejection.
- Code normalization.
- Status group consistency.
- Statistics and filtering parity.
- Settings roundtrip.
- Derived settings.
- Backup integrity.
- Excel export.
- Export with charts.
- Auto backup/export.
- Folder path generation.
- API response compatibility.

## 11.2 Workflow Tests

- Request submission.
- Required-field validation.
- Attachment upload.
- Request ID generation.
- Admin dashboard visibility.
- FCR Code assignment.
- Reviewer assignment.
- Sequential workflow.
- Parallel workflow.
- Independent reviewer decisions.
- Approval rule evaluation.
- Comment submission.
- Rejection routing to Admin.
- Return-for-correction routing to Admin.
- Admin resubmission.
- Repeated review cycles.
- Final approval.
- Workflow history.
- Notification delivery.

## 11.3 Security Tests

- Unauthorized access.
- Role permission enforcement.
- Unauthorized record modification.
- Unauthorized attachment access.
- File upload validation.
- Session handling.
- Audit log integrity.
- Concurrent update conflicts.

All tests must use temporary databases or isolated test environments. The original working database must never be used as a test database.

---

# 12. Backup, Recovery and Data Integrity

The application must provide:

- Scheduled database backups.
- Manual backups.
- Attachment backup.
- Backup integrity verification.
- Backup retention policy.
- Restore procedure.
- Recovery testing.
- Audit logging of backup and restore operations.

A database backup without its associated attachment storage is not a complete system backup.

The backup and recovery process must therefore cover both database records and attachment files.

---

# 13. Future ERMS Integration

The FCR module must be designed as a modular component of the future ERMS platform.

Future integration may include:

- Common user and identity management.
- Shared organizational roles.
- Common notification service.
- Common document/attachment management.
- Shared audit logging.
- Integration with other engineering request modules.
- Integration with Facility Change Management.
- Centralized reporting.

However, these integrations are outside the initial implementation scope unless separately approved.

The FCR module must avoid unnecessary dependencies that would prevent its future integration into a larger ERMS platform.

---

# 14. Unresolved Decisions Requiring Company IT Approval

The following items must remain explicit open decisions:

| Decision | Current Position |
|---|---|
| .NET version | Recommend .NET 10 LTS; subject to IT approval |
| Production database | Open – SQLite, SQL Server, PostgreSQL, or approved alternative |
| Authentication | Open – company-approved SSO / identity provider |
| Hosting environment | Centralized server; operating system and infrastructure subject to IT |
| Attachment storage | Centralized storage; exact technology subject to IT |
| Email notification | Subject to approved company mail service |
| Frontend technology | Retain existing frontend initially; future modernization optional |
| Workflow templates | Configurable; business approval required |
| Reviewer selection rules | To be defined by FCR Admin / organization |
| Parallel approval rules | To be confirmed |
| Final approval authority | To be confirmed |
| File upload limits | To be defined by IT |
| Backup retention | To be defined by IT |

---

# 15. Implementation Rules for Claude Code

The following rules are mandatory throughout implementation.

1. Do not start coding before the user approves the Master Implementation Plan.
2. Do not delete or overwrite the existing Python application.
3. Do not modify the original SQLite database during development.
4. Always create a verified backup before database migration.
5. Implement one phase at a time.
6. Do not proceed to the next phase until the current phase tests pass and the user approves progression.
7. Do not invent missing business requirements.
8. When a business rule is unclear, document it as an open decision.
9. Preserve existing verified functionality.
10. Do not introduce unnecessary architectural complexity.
11. Do not hardcode reviewer names, approval sequences, or user permissions.
12. Enforce authorization on the backend.
13. Keep database and file-storage implementation independent.
14. Use transactions for operations that must update multiple related records.
15. Implement concurrency handling for multi-user editing and approval operations.
16. Record important workflow and administrative actions in the audit log.
17. Maintain clear technical documentation.
18. Report changed files, implemented features, tests, and outstanding issues after every phase.
19. Never claim that a feature is complete without corresponding tests.
20. Do not perform production deployment or destructive data migration without explicit user authorization.

---

# 16. Final Acceptance Criteria

The project will be considered ready for organizational acceptance only when all the following conditions are met:

### Existing FCR Dashboard

- All existing FCR records are preserved.
- All verified existing FCR fields remain available.
- Existing search, filtering, sorting, statistics, reports, and exports work.
- Backup and recovery are tested.
- No existing critical functionality is lost.

### Organizational Workflow

- Requesters can submit initial FCR requests.
- Initial submission does not require completion of the full FCR technical form.
- FCR Admin receives and manages submitted requests.
- FCR Admin can assign FCR Codes and complete administrative fields.
- Sequential and parallel review are supported.
- Reviewers can approve, comment, and reject.
- Rejected and returned requests are routed to FCR Admin.
- Admin can coordinate corrections and resubmit.
- All review and decision history is preserved.
- Workflow approval is independent of execution and closure status.

### Centralized Application

- Multiple authorized users can access the application through a browser.
- User roles and permissions are enforced.
- Attachments are centrally stored and securely accessible.
- Concurrent operations are handled safely.
- Audit logs are available.
- The application can be deployed and maintained centrally.

### Technical Quality

- Automated tests pass.
- Database migration is verified.
- Security tests pass.
- Backup and recovery are verified.
- IT and business stakeholders approve production deployment.

---

# 17. Recommended Execution Sequence

The recommended execution sequence is:

1. Approve this Master Implementation Plan.
2. Complete Phase 0 – Repository Inspection and Technical Baseline.
3. Review and approve the technical inventory.
4. Complete Phases 1–3 – .NET foundation, existing functionality migration, and centralized deployment.
5. Verify existing FCR Dashboard parity.
6. Complete Phases 4–6 – Authentication, Requester/Admin modules, and Review Workflow.
7. Complete Phases 7–8 – Audit, reporting, testing, and production cutover.
8. Obtain final organizational acceptance.
9. Proceed with broader ERMS integration as a separate approved development stage.

**Final Principle:** The new application must not simply be a .NET copy of the current FCR Dashboard. It must preserve the proven technical capabilities of the existing application while introducing a controlled, secure, multi-user FCR request and approval workflow suitable for future integration into the company's ERMS/FCM environment.
