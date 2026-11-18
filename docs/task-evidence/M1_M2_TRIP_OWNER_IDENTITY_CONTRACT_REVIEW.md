# M1-M2 Trip Owner Identity and Database Relationship Review

**Project:** WAYHUNTER Backend
**Modules:** M1 IdentitySocial / M2 TravelPlanning
**Review Date:** 2026-11-18
**Base Commit:** dbbf5f9
**Status:** PROPOSED - Cross-module approval pending
**Change Type:** Documentation only

---

## 1. Purpose

Review the existing relationship between M1 user identity
and M2 trip ownership before shared SQL migration approval.

This document records observed implementation, test coverage,
security considerations, and outstanding decisions.

It does not approve or execute database migrations.

## 2. M1 Identity Contract

Source:
- UserAccountConfiguration.cs
- SessionTokenClaimsFactory.cs
- SessionBearerAuthenticationHandler.cs
- AuthenticatedSessionAuthorizationService.cs
- TrustedIdentityAccessContract.cs

Observed contract:

- M1 uses the Users table.
- UserAccount.UserId is a string primary key.
- The UserId column has a maximum length of 256.
- M1-generated authenticated user IDs are GUID-formatted strings.
- M1 issues a canonical JWT sub claim.
- The trusted session authentication handler also produces
  ClaimTypes.NameIdentifier for the authenticated user.
- The trusted identity contract checks identity claim consistency.
- The M1 trust checks cover account and session eligibility,
  including deletion-request guards.

The M1 identity contract must remain authoritative for
cross-module authenticated user identity.

## 3. M2 Trip Owner Mapping

Source:
- TripPlan.cs
- TripPlanConfiguration.cs

Observed EF Core mapping:

Table: Trips
Primary Key: Id (Guid)
Owner Property: OwnerUserId (string)
Owner Column Maximum Length: 256
Owner Column Required: Yes

Existing relationship:

Principal Entity: UserAccount
Principal Table: Users
Principal Key: UserId

Dependent Entity: TripPlan
Dependent Table: Trips
Foreign Key: OwnerUserId

Constraint Name:
FK_Trips_Users_OwnerUserId

Delete Behavior:
NoAction

Index:
Trips.OwnerUserId

Conclusion:

The M1-to-M2 owner foreign key is already configured
in EF Core metadata.

A duplicate foreign key implementation is unnecessary.

The existence of EF mapping alone does not establish
that the physical constraint exists in a live SQL database.

## 4. Existing EF Metadata Tests

Source:
- tests/Wayhunter.UnitTests/Modules/TravelPlanning/
  M2TripOwnerForeignKeyTests.cs
- tests/Wayhunter.UnitTests/Modules/SharedPersistence/
  M2SharedMigrationReadinessTests.cs

Observed assertions include:

- TripPlan references UserAccount.
- Trips.OwnerUserId is the dependent foreign key.
- Users.UserId is the principal key.
- DeleteBehavior is NoAction.
- The foreign key constraint name is correct.

The shared migration readiness test also verifies
additional proposed M2 constraints and relationships.

These tests inspect EF metadata.

They do not establish successful SQL Server migration
execution or physical foreign key enforcement.

Test files were inspected during this review.
A new test execution is not claimed here.

## 5. M2 Controller Authentication

Source:
- TripPlansController.cs

Observed behavior:

- The controller uses [Authorize].
- The controller requires an authenticated identity.
- GetAuthenticatedUserId() reads:
  ClaimTypes.NameIdentifier ?? sub
- Create passes the resolved ID to CreateTripPlanService.
- List and detail endpoints pass the resolved ID
  into the relevant application services.
- Update also passes the authenticated user ID.

The inspected controller does not use the shared
TrustedIdentityAccessContract directly.

The current fallback does not itself enforce the same
duplicate/conflicting-claim checks as that contract.

However, the M1 production session authentication
handler validates and constructs the principal.

This difference is a contract consistency review item,
not a confirmed production security vulnerability.

## 6. Trip Creation Ownership

Source:
- CreateTripPlanService.cs
- TripPlan.cs

Observed behavior:

- CreateTripPlanService.CreateAsync receives ownerUserId
  as a separate argument from CreateTripPlanDto.
- The service rejects a blank ownerUserId.
- The service validates the trip details.
- The TripPlan constructor stores the supplied ownerUserId.
- The repository persists the created trip plan.

The inspected flow takes the owner ID from the
authenticated controller principal rather than
a client-supplied owner ID in the request body.

This review does not establish that all other callers
or endpoints enforce identical identity handling.

## 7. Review Findings

### F1 - EF Foreign Key Already Exists

Status: Implemented in EF metadata.

Action:
Preserve the existing M1-to-M2 FK definition.
Do not create a duplicate mapping.

### F2 - Shared FK Metadata Tests Exist

Status: Existing test coverage identified.

Action:
Maintain test coverage for principal key, dependent
key, delete behavior, and constraint name.

### F3 - Controller Identity Resolution Differs

Status: Requires cross-module review.

Action:
M1 and M2 owners should decide whether M2 must call
the existing trusted identity contract or another
approved shared identity resolver.

Do not change M2 authentication implementation
without the M2 owner's approval.

### F4 - Live SQL Foreign Key Not Verified

Status: Pending.

Action:
DB Coordinator must confirm actual migration state,
physical FK existence, and enforcement in the
approved SQL Server environment.

### F5 - Migration and Data Compatibility

Status: Requires DB Coordinator review.

Action:
Verify that existing Trips.OwnerUserId values map
to real Users.UserId records before enforcing the FK.

Review legacy or test data, migration ordering,
and applicable data cleanup requirements.

### F6 - Ownership Authorization Test Coverage

Status: Additional review recommended.

Action:
Confirm create, list, detail, update, and cancel
flows consistently restrict access to the
authenticated trip owner.

Include missing, duplicate, or conflicting identity
claim tests where relevant to the approved contract.

## 8. Proposed M1-M2 Identity Contract

Proposal only - not yet approved.

1. M1 is the authoritative source of authenticated
   user identity.

2. M2 stores the same logical M1 UserId in
   Trips.OwnerUserId.

3. Cross-module identity checks should follow
   one approved trusted identity resolution contract.

4. M2 must not trust client-supplied owner identity
   for authorization.

5. The existing owner FK and NoAction delete behavior
   remain the baseline unless the owners and DB
   Coordinator approve another design.

6. SQL migration ownership and order must be agreed
   before modifying the shared database.

## 9. Responsibilities

M1 IdentitySocial Owner:
- Confirm canonical authenticated UserId semantics.
- Confirm trusted identity resolver contract.
- Confirm session and account validity requirements.

M2 TravelPlanning Owner:
- Confirm trip ownership enforcement.
- Review controller identity resolution consistency.
- Approve any later M2 production code changes.
- Confirm ownership authorization test coverage.

DB Coordinator:
- Approve Users and Trips relationship.
- Review existing migrations and live schema.
- Verify foreign key compatibility with existing data.
- Approve migration ownership and execution order.

Team Leader:
- Confirm cross-module decisions.
- Approve implementation follow-up tasks.

## 10. Verification Boundary

Reviewed source and existing test definitions
on the develop baseline dbbf5f9.

The review did not execute:

- New backend builds
- New test runs
- SQL migrations
- Live SQL foreign key verification

No production code was modified.

## 11. Approval Checklist

- [ ] M1 owner confirms identity contract.
- [ ] M2 owner confirms ownership authorization.
- [ ] M2 owner reviews identity resolver consistency.
- [ ] DB Coordinator approves final FK strategy.
- [ ] DB Coordinator validates legacy data compatibility.
- [ ] DB Coordinator confirms live SQL migration plan.
- [ ] Team Leader approves cross-module follow-up.

## 12. Final Review Status

Documentation review prepared.

Existing EF FK implementation and metadata tests
have been identified.

Cross-module approval and actual SQL verification
remain separate pending activities.

No migrations or production changes are authorized
by this document.
