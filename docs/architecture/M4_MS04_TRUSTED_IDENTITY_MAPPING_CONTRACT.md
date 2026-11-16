
# WAYHUNTER — M4-MS04 Trusted Identity Mapping Contract

**Module:** M4 — HunterPlaces
**Related Module:** M1 — IdentitySocial
**Task:** M4-MS04
**Date:** 2026-11-16
**Status:** DRAFT — CROSS-MODULE DECISIONS PENDING
**Migration Status:** HOLD
**Scope:** Documentation only
---

## 1. Purpose

This document defines the proposed trusted identity mapping contract between M1 IdentitySocial and M4 HunterPlaces.

The purpose is to establish how M1 `UserAccount.UserId` is securely resolved and mapped to M4 `HunterId` for Hunter capture-session operations.

This document records existing implementation evidence, approved Product Owner decisions, and pending cross-module decisions.

No implementation, database schema, or migration changes are authorized.

## 2. Approved Product Owner Decisions

The following decisions are already approved by the WAYHUNTER Team Leader:

1. M1 `UserAccount.UserId` and M4 `HunterId` represent the same logical user identity.
2. M1 string GUID must be strictly validated before mapping to M4 `Guid`.
3. Client-provided `HunterId` must not be trusted.
4. Identity must be resolved from the authenticated server-side principal.
5. Invalid GUID, identity mismatch, inactive account, and suspended account must be rejected.
6. No random or client-controlled fallback identity is permitted.

**Decision status: APPROVED**

## 3. Existing Repository Implementation

### 3.1 M1 Trusted Identity Contract

Source:

`src/Wayhunter.Api/Contracts/TrustedIdentityAccessContract.cs`

Existing implementation:

- Requires an authenticated `ClaimsPrincipal`.
- Reads `ClaimTypes.NameIdentifier` and `sub`.
- Rejects missing identity claims.
- Rejects duplicate identity claims.
- Rejects malformed or empty GUID values.
- Rejects conflicting identity claim values.
- Normalizes valid GUIDs into standard "D" format.
- Checks that the trusted account identity matches.
- Requires an active and verified M1 account.

These are existing implementation behaviors.

The final authoritative claim and JWT issuer requirements remain pending M1 Owner confirmation.

### 3.2 M4 Trusted Hunter Identity Resolver

Source:

`src/Wayhunter.Api/Services/HunterPlaces/TrustedHunterCaptureSessionIdentityResolver.cs`

Existing implementation:

- Accepts a backend-supplied authenticated user identifier.
- Validates that the identifier is a nonempty GUID.
- Resolves the corresponding trusted M1 user account.
- Rejects invalid or unauthorized identities.
- Converts the validated string identity to M4 `Guid` HunterId.
- Returns `null` when trusted identity resolution fails.

The existing resolver must not be duplicated.

Production integration readiness requires final contract approval.

### 3.3 M1 Account Status

Source:

`src/Wayhunter.Domain/Modules/IdentitySocial/Enums/UserAccountStatus.cs`

| Account Status | Value | Capture Eligibility |
|---|---:|---|
| PendingVerification | 0 | Reject |
| Active | 1 | Eligible only when verified |
| Suspended | 2 | Reject |
| DeletionRequested | 3 | Reject |

The final account lifecycle contract remains subject to M1 Owner confirmation.

## 4. Trusted Identity Resolution Flow

The proposed secure identity resolution flow is:

1. Receive the incoming authenticated API request.
2. Validate authentication using the approved backend authentication scheme.
3. Obtain the authenticated server-side `ClaimsPrincipal`.
4. Extract the identity claim according to the approved M1 contract.
5. Reject missing, duplicate, malformed, empty, or conflicting claims.
6. Validate and normalize the user identifier to GUID "D" format.
7. Load the corresponding trusted M1 `UserAccount`.
8. Verify that the resolved account identity matches.
9. Reject inactive, suspended, unverified, or deletion-requested accounts.
10. Convert the validated identity into M4 `Guid` HunterId.
11. Verify Hunter submission ownership.
12. Verify applicable capture-session eligibility rules.
13. Permit the requested M4 operation only after all required checks succeed.

**Important:** Client-supplied HunterId must never override the authenticated identity.

## 5. Identity Validation and Failure Handling

| Condition | Required Behavior |
|---|---|
| Unauthenticated request | Reject |
| Missing identity claim | Reject |
| Duplicate identity claims | Reject |
| Invalid GUID | Reject |
| Empty GUID | Reject |
| Conflicting NameIdentifier and sub | Reject |
| M1 user account not found | Reject |
| User identity mismatch | Reject |
| PendingVerification account | Reject |
| Suspended account | Reject |
| DeletionRequested account | Reject |
| Active but unverified account | Reject |
| Client-provided HunterId | Never trust as identity |
| Valid, active, verified account | Continue to M4 authorization checks |

Exact HTTP status codes and final error response contracts require M1 Owner confirmation.

## 6. Existing Database Relationship

### 6.1 M1 Users

Source:

`src/Wayhunter.Infrastructure/Persistence/Configurations/IdentitySocial/UserAccountConfiguration.cs`

Current mapping:

- Table: `Users`
- Primary Key: `UserId`
- CLR type: `string`
- Maximum length: 256
- SQL Server convention: `nvarchar(256)`

### 6.2 M4 Hunter Submissions

Source:

`src/Wayhunter.Infrastructure/Persistence/Configurations/HunterPlaces/HunterSubmissionConfiguration.cs`

Current mapping:

- Table: `Hunter_Submissions`
- UserId CLR type: `string`
- Maximum length: 256
- Existing FK: `FK_Hunter_Submissions_Users_UserId`
- Delete behavior: `NoAction`

### 6.3 M4 Hunter Capture Sessions

Source:

`src/Wayhunter.Infrastructure/Persistence/Configurations/HunterPlaces/HunterCaptureSessionRowConfiguration.cs`

Current mapping:

- Table: `Hunter_CaptureSessions`
- HunterId CLR type: `Guid`
- HunterId SQL type: `uniqueidentifier`
- HunterSubmissionId SQL type: `uniqueidentifier`
- Submission FK: `FK_Hunter_CaptureSessions_Submissions`
- Delete behavior: `NoAction`

No direct FK from `Hunter_CaptureSessions.HunterId` to `Users.UserId` is currently configured.

The different SQL column types require a final database relationship decision.

## 7. M1 Owner Decisions

**Status: PENDING M1 OWNER**

The M1 Authentication Owner must confirm:

1. Authoritative M1 user identity field.
2. Exact authenticated identity claim (`sub`, `NameIdentifier`, or another claim).
3. JWT issuer and issuer validation requirements.
4. Whether both claims must match when both are present.
5. Active, inactive, suspended, and deactivated account handling.
6. Trusted identity resolver ownership and interface.
7. Expected authentication and authorization error behavior.

Existing code is supporting evidence, not final approval.

## 8. DB Coordinator Decisions

**Status: PENDING DB COORDINATOR**

The DB Coordinator must confirm:

1. Authoritative M1/M4 database ownership.
2. Whether M1 and M4 tables reside in the same production database.
3. Whether a direct physical FK from CaptureSessions to Users is required.
4. Approved FK target and compatible SQL column types.
5. Uniqueness and integrity requirements.
6. Delete and update behavior.
7. Whether the existing HunterSubmission-to-Users FK is sufficient.
8. Logical relationship validation strategy if no direct FK is approved.

No schema changes may be made before these decisions are finalized.

## 9. Implementation Readiness Checklist

- [x] Product Owner identity mapping principle approved.
- [x] Existing M1 identity contract reviewed.
- [x] Existing M4 trusted resolver reviewed.
- [x] Existing M1/M4 EF relationships reviewed.
- [x] Identity validation flow documented.
- [x] Invalid and inactive account handling documented.
- [ ] M1 Owner confirms authoritative claim.
- [ ] M1 Owner confirms JWT issuer requirements.
- [ ] M1 Owner confirms final account status contract.
- [ ] M1 Owner confirms error handling contract.
- [ ] DB Coordinator confirms database ownership.
- [ ] DB Coordinator confirms final FK strategy.
- [ ] Final cross-module contract reviewed.
- [ ] Team Leader approves final contract.
- [ ] Shared migration separately authorized.

## 10. Previous Verification Evidence

The following results were previously reported for M4:

- PR #187: MERGED.
- Backend build: PASSED.
- M4 EF and repository guard tests: 18 PASSED.
- M4 isolated SQL integration tests: 3 PASSED.
- Concurrent capture-session consumption: PASSED.
- Status transition and session revocation: PASSED.
- Transaction rollback verification: PASSED.
- Full regression: 2,414 PASSED, 0 FAILED.

These results belong to earlier M4 verification work.

They do not represent fresh execution of tests for this documentation branch.

## 11. Restrictions

The following actions are prohibited under M4-MS04:

- Modifying existing M1 implementation.
- Modifying existing M4 implementation.
- Changing database schemas.
- Creating or applying EF migrations.
- Running `dotnet ef database update`.
- Executing manual SQL DDL.
- Assuming pending decisions are approved.
- Merging the documentation PR without required review.

## 12. Final Readiness Status

| Item | Status |
|---|---|
| M4-MS04 Task | APPROVED |
| Identity Mapping Principle | APPROVED |
| Existing Code Review | COMPLETED |
| Documentation Contract | DRAFT |
| M1 Claim Contract | PENDING |
| M1 Issuer Contract | PENDING |
| DB FK Strategy | PENDING |
| Production Implementation | HOLD |
| Shared SQL Migration | HOLD |

**Final Conclusion:**

The existing repository provides a proposed trusted identity mapping implementation and EF relationship mappings.

M4-MS04 documentation may proceed for review.

The final production identity contract and database FK strategy remain pending cross-module decisions.

No implementation or migration changes are authorized by this document.
