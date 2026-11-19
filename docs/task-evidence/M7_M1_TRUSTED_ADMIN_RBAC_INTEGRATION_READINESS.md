# M7 - M1 Trusted Administrator RBAC Integration Readiness

## Status

Design and regression-test readiness reviewed.
Production Administrator activation is NOT approved.

## Ownership

- M1 owns trusted Administrator role resolution, DI registration and SessionBearer authentication integration.
- M7 owns Advertisement Administrator policy and authorization regression readiness.
- M6 Admin endpoints must reuse the trusted M1 identity contract.
- DB Coordinator owns final Administrator role storage and migration approval.

## Approved Authentication Contract

1. Validate M1 signed JWT.
2. Validate active and verified M1 account.
3. Validate persisted session ownership and validity.
4. Resolve Administrator assignment from an approved server-controlled source.
5. Verify assignment UserId matches authenticated UserId.
6. Require Role = Admin and Status = Active.
7. Issue ClaimTypes.Role = Admin only after successful trusted verification.
8. Deny Administrator access when any validation fails.

Existing sub, sid and NameIdentifier claims must be preserved.

Client-provided role claims must never grant privileges.

## Existing Implementation

- ITrustedAdministratorRoleReader
- TrustedAdministratorRoleResolutionService
- AdministratorRoleAssignment
- SessionBearerAuthenticationHandler
- AdvertisementAdministratorPolicy
- AdvertisementAdminPrincipalReadiness

## Verified Security Regression Coverage

- Valid trusted Administrator assignment
- Missing or revoked role assignment
- Forged JWT Administrator role
- Invalid or missing bearer token
- Suspended or unverified account
- Deletion-requested account
- Invalid or expired persisted session
- Session revocation
- Unavailable trusted role source
- Mismatched assignment UserId
- Invalid Administrator configuration
- Anonymous and normal-user Advertisement access denied

## Baseline Test Results

- M1/M7 focused unit tests: 26 passed, 0 failed.
- M1/M7 real-bearer HTTP tests: 17 passed, 0 failed.
- Latest previously verified full unit suite: 2790 passed.
- Latest previously verified full integration suite: 91 passed, 59 skipped.
- Backend build: Passed.

## Confirmed Production Wiring Gap

Current SessionBearerAuthenticationHandler creates:
- sub
- sid
- ClaimTypes.NameIdentifier

It does not currently call
TrustedAdministratorRoleResolutionService.

Source search found no production invocation of
ResolveAdminRoleClaimAsync.

This is a fail-closed readiness gap, not authorization bypass.

## Proposed M1-Owned Wiring

- Register the trusted role resolution service through M1 DI.
- Keep the production role reader unavailable or disabled until approved.
- Invoke role resolution only within the authenticated M1 session flow.
- Preserve existing authentication behavior and identity claims.
- Append trusted Administrator role claim only after approved verification.
- Fail closed for missing, revoked or unverified assignments.
- Ensure role revocation remains effective under the approved session-security contract.
- Add observability without leaking sensitive role or token data.

M7 does not directly modify M1 authentication or role storage.

## Remaining Blockers

1. Final UserRoleAssignments schema approval.
2. Server-controlled production role reader implementation.
3. M1 authentication-pipeline wiring and DI registration.
4. Role revocation and observability acceptance.
5. Dedicated SQL verification and DB Coordinator sign-off.
6. Explicit production Administrator activation approval.

## Restrictions

- No SQL migrations created or applied.
- No shared database modifications.
- No client-controlled role claims.
- No M7 policy bypass.
- No production Administrator activation.

## Submission

Feature branch:
task/m7-m1-trusted-admin-integration-readiness

Target branch:
develop

PR scope:
M7 readiness evidence only, unless new uncovered
regression scenarios are explicitly identified.
