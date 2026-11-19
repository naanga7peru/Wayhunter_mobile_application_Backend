# M1-M7 Administrator Role SQL Approval Checklist

Project: WAYHUNTER Backend
Owner: M1 IdentitySocial
Consumer: M7 HomeAdsAnalytics
DB Coordinator: Kaviyan
Status: SCHEMA DESIGN AND GUARDED IMPLEMENTATION APPROVED; MIGRATION AND ACTIVATION PENDING

## 1. Purpose

Finalize database decisions required for trusted
Administrator role persistence and authorization.

Reference:
M1_M7_ADMIN_ROLE_STORAGE_SCHEMA_PROPOSAL.md

This checklist is not approval to create migrations,
grant roles, or enable production Admin access.

## 2. Approved Phase A Contract

- M1 owns trusted Administrator role resolution.
- Approved role claim type: ClaimTypes.Role
- Approved role value: Admin
- Active, verified account required.
- Valid, non-revoked session required.
- Trusted server-controlled role assignment required.
- Missing or invalid role assignment denies access.
- M7 owns the centralized Administrator API policy.
- Production role activation remains disabled.

## 3. Database Decisions Required

| ID | Decision | Proposed Design | Approval |
|---|---|---|---|
| DB-01 | Role table owner | M1 IdentitySocial | DESIGN APPROVED |
| DB-02 | Table name | UserRoleAssignments | DESIGN APPROVED |
| DB-03 | Primary key | AssignmentId uniqueidentifier | DESIGN APPROVED |
| DB-04 | User FK | UserId -> Users.UserId | DESIGN APPROVED |
| DB-05 | Delete behavior | NoAction | DESIGN APPROVED |
| DB-06 | Role column | nvarchar(64) | DESIGN APPROVED |
| DB-07 | Status column | nvarchar(16) | DESIGN APPROVED |
| DB-08 | Status values | Active, Revoked, Inactive | DESIGN APPROVED |
| DB-09 | Active role uniqueness | Filtered unique index | DESIGN APPROVED |
| DB-10 | Assignment timestamps | datetimeoffset | DESIGN APPROVED |
| DB-11 | Assignment actor FK | Coordinator decision required | PENDING |
| DB-12 | Revocation actor FK | Coordinator decision required | PENDING |
| DB-13 | Bootstrap Admin process | Coordinator decision required | PENDING |
| DB-14 | Migration owner | Coordinator decision required | PENDING |
| DB-15 | Migration ordering | Coordinator decision required | PENDING |

## 4. Mandatory Schema Validation

Before migration approval:

- Confirm actual Users table and UserId mapping.
- Match UserId SQL length, type, and collation.
- Confirm UserRoleAssignments PK and FK names.
- Prevent duplicate active Admin assignments.
- Reject unsupported Role and Status values.
- Define active and revoked timestamp constraints.
- Define audit actor and system bootstrap rules.
- Confirm Inactive status semantics.
- Confirm required indexes and query behavior.
- Confirm migration ownership and rollback plan.

All decisions must be based on the merged
database model, not an isolated proposed schema.

## 5. Security Requirements

- Never accept client-provided Administrator roles.
- Never grant Admin based on configuration alone.
- Validate active, verified M1 account.
- Validate persisted session on every request.
- Reject revoked or expired sessions.
- Reject deletion-requested or suspended accounts.
- Reject revoked, inactive or mismatched roles.
- Reject ambiguous active role assignments.
- Fail closed if the role database is unavailable.
- Preserve cancellation behavior.
- Add safe operational logging without secrets.

## 6. Planned Implementation After Approval

Phase B1:
Finalize approved role entity and EF mapping.

Phase B2:
Implement trusted SQL Administrator role reader.

Phase B3:
Add SQL mapping and integration tests.

Phase B4:
Create migration under approved DB ownership.

Phase B5:
Verify migration against approved SQL Server.

Phase B6:
Implement approved role provisioning and audit.

Phase B7:
Integrate trusted M1 role resolution into the
production authentication pipeline.

Phase B8:
Verify M7 Advertisement Admin API with real
M1 authentication and approved role storage.

No phase is authorized by this checklist alone.

## 7. Existing Verification Evidence

- M1 Administrator role resolver tests passed.
- M1-M7 policy contract: 8 tests passed.
- M1-M7 real JWT HTTP: 17 tests passed.
- Full backend baseline: 2992 total.
- Passed: 2933.
- Failed: 0.
- Skipped: 59.

These results were recorded before this document
was created and do not constitute SQL verification.

## 8. Verified EF Model Evidence

Baseline: develop e0e0810

Source files:

- UserAccountConfiguration.cs
- WayhunterSharedDbContext.cs
- M1M7AdministratorSqlReadinessTests.cs

Verified current EF mapping:

- UserAccount maps to Users.
- Users.UserId is the primary key.
- UserId uses CLR string.
- UserId maximum length is 256.
- UserId is required.
- UserId is not database-generated.
- The shared model includes UserAccount.
- UserRoleAssignments is not currently mapped.

Targeted test results:

- Total: 2
- Passed: 2
- Failed: 0
- Skipped: 0

Build: succeeded.

These tests validate the current EF model using
an in-memory provider. They do not prove the
actual SQL Server schema or migration readiness.

Remaining decisions:

- Confirm deployed Users schema and collation.
- Approve UserRoleAssignments schema and ownership.
- Approve FK, indexes and audit constraints.
- Approve bootstrap and migration procedures.
- Verify approved mappings against SQL Server.

DB Coordinator approval remains PENDING.

## 9. Conditional Implementation Approval

Authority: Kaviyan, WAYHUNTER Team Leader
Decision: Schema design and guarded implementation APPROVED

### Approved Schema Design

- M1 owns UserRoleAssignments.
- EF ownership: WayhunterSharedDbContext.
- AssignmentId: uniqueidentifier, required,
  server-generated primary key.
- UserId: nvarchar(256), required.
- UserId foreign key references Users.UserId.
- User deletion behavior: NoAction.
- Role: nvarchar(64), required, exact Admin.
- Status: nvarchar(16), required.
- Allowed statuses: Active, Revoked, Inactive.
- AssignedAtUtc: required datetimeoffset.
- AssignedByUserId: nullable nvarchar(256).
- RevokedAtUtc: nullable datetimeoffset.
- RevokedByUserId: nullable nvarchar(256).
- Unique filtered index: (UserId, Role)
  WHERE Status = 'Active'.
- Enforce allowed Role and Status values.
- Enforce consistent revocation timestamps.
- Inactive assignments never authorize Admin.

Nullable audit actor IDs are permitted only
under separately approved, audited system rules.

### Approved Implementation Work

- EF entity and configuration.
- EF metadata and constraint tests.
- Trusted SQL Administrator role reader.
- Duplicate-active-role tests.
- Historical assignment tests.
- Revocation and unavailable-reader tests.
- M6 and M7 Bearer authorization regressions.
- Implementation on a separate feature branch.

### Runtime Security

- Read server-controlled assignments only.
- Require active, verified M1 account.
- Require valid, non-revoked session.
- Require exact active Admin assignment.
- Reject missing and ambiguous assignments.
- Reject inactive and revoked assignments.
- Recheck revocation on subsequent requests.
- Fail closed when role storage is unavailable.
- Respect cancellation.
- Never trust client-supplied role claims.
- No public Administrator assignment endpoint.

### Final DB Coordinator Decisions Pending

- Final physical schema approval.
- FK SQL collation compatibility.
- Audit actor relationships.
- System bootstrap audit process.
- Migration owner and ordering.
- M3 schema drift reconciliation.
- Migration rollback and SQL verification plan.
- Isolated real SQL Server test approval.

Preserve the already applied InitialSharedSchema.

### Explicitly Not Approved

- Independent shared SQL migration generation.
- Applying shared SQL migrations.
- Modifying the shared database.
- Administrator account creation or seeding.
- Production Administrator role activation.
- Weakening M1, M6 or M7 authorization.

SQL role resolution must remain disabled
by default until separate activation approval.

## 10. Approval Record

DB Coordinator:
Decision: PHYSICAL SCHEMA AND MIGRATION APPROVAL PENDING
Approval date: PENDING
Approved schema reference: PENDING
Migration owner: PENDING
Migration order: PENDING

M1 Owner:
Implementation readiness: PENDING

M7 Owner:
Integration readiness: PENDING

## 11. Current Boundary

Documentation and non-production regression
tests may proceed on a feature branch.

DO NOT:
- Create or apply Administrator SQL migrations.
- Register an unapproved SQL role reader.
- Enable production Administrator role claims.
- Seed or grant Administrator assignments.
- Modify production authentication behavior.

Final approval must be recorded before these actions.

## Final AssignmentId Generation Approval

Status: APPROVED by Kaviyan, WAYHUNTER Team Leader / M6 Owner.

### Approved Backend-Generated GUID Contract

- `UserRoleAssignments.AssignmentId` is a
  `uniqueidentifier NOT NULL` primary key.
- The trusted backend generates the ID using `Guid.NewGuid()`.
- EF Core mapping uses `ValueGeneratedNever()`.
- SQL Server `DEFAULT NEWID()` is not permitted.
- EF Core `ValueGeneratedOnAdd()` is not permitted.
- Client-supplied AssignmentId values must never be trusted.
- `Guid.Empty` must be rejected.
- Database primary-key uniqueness enforcement must remain enabled.

The term "server-generated" means generation by the
trusted backend application server, NOT by SQL Server.

### Implementation Verification

- Entity generation: `Guid.NewGuid()`.
- EF Core configuration: `ValueGeneratedNever()`.
- Full backend build: PASS.
- Full backend tests: 3,013 total.
- Passed: 2,952.
- Failed: 0.
- Skipped: 61.
- SQL Administrator reader DI activation: NOT enabled.
- Shared SQL migration: NOT generated.

### Remaining Approval Boundaries

- Preserve the existing `InitialSharedSchema`.
- Coordinate physical schema and migration with the DB Coordinator.
- Do not create or seed Administrator accounts.
- Do not update the shared production database.
- Do not activate production Administrator privileges
  without separate approval.

Production activation status: ON HOLD.