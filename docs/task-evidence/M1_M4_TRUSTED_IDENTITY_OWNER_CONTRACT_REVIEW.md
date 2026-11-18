# M1–M4 Trusted Identity Contract — M1 Owner Review

**Module:** M1 IdentitySocial
**Related module:** M4 HunterPlaces
**Branch:** task/m1-m4-trusted-identity-contract-review
**Date:** 2026-11-18
**Status:** PROPOSED — CROSS-MODULE REVIEW REQUIRED
**Scope:** Documentation and existing-code verification only

## 1. Purpose

Record the M1 authentication and identity decisions proposed for
the M1–M4 trusted Hunter identity mapping contract.

This document does not authorize production DI registration,
schema changes, or database migrations.

## 2. Verified M1 Implementation

The following implementation was inspected:

- TrustedIdentityAccessContract
- SessionTokenClaimsFactory
- SignedSessionTokenService
- SessionBearerAuthenticationHandler
- AuthenticatedSessionAuthorizationService
- TrustedIdentityResolutionService
- ITrustedIdentityAccountReader

### 2.1 Authoritative user identity

- M1 authoritative identity field: UserAccount.UserId
- CLR type: string
- Required format: nonempty canonical GUID ("D" format)
- M4 HunterId CLR type: Guid
- The approved mapping principle treats both as the same logical user.

### 2.2 JWT identity claims

- Issued JWT identity claim: sub
- Session identifier: sid
- JWT identifier: jti
- SessionBearer creates a server-side ClaimsPrincipal.
- The principal contains sub and ClaimTypes.NameIdentifier.
- Both identity claims are populated from the same validated UserId.

Proposed M1 contract:

- sub is the authoritative JWT identity claim.
- NameIdentifier is a server-generated compatibility claim.
- Missing, duplicate, invalid or conflicting identity claims must
  be rejected by the shared trusted identity contract.
- Client-provided HunterId must never be used as authenticated identity.

### 2.3 JWT validation

Existing SignedSessionTokenService validates:

- Configured JWT issuer
- Configured JWT audience
- HMAC-SHA256 signed token
- Signing key and token lifetime
- Zero clock skew
- Required sub, sid and jti GUID values

JWT issuer and audience are configured using:

- Authentication:Jwt:Issuer
- Authentication:Jwt:Audience

The signing secret is provided by:

- Authentication:Jwt:SigningSecret

No production issuer value or signing secret is recorded here.

### 2.4 Persisted account and session authorization

AuthenticatedSessionAuthorizationService verifies:

- JWT validity
- Trusted persisted UserAccount
- Active account status
- Verified account
- No deletion request
- Account and session identity ownership
- Valid verification timestamps
- Persisted session exists
- Session is not revoked
- Session has not expired

These checks execute when AuthorizeAsync is invoked.

## 3. Proposed M1 Account Eligibility Contract

| Account condition | Proposed behavior |
|---|---|
| Active and verified | Eligible, subject to session checks |
| PendingVerification | Reject |
| Suspended | Reject |
| DeletionRequested | Reject |
| Active with DeletionRequestedAtUtc | Reject |
| Active but unverified | Reject |
| Account not found | Reject |
| Identity mismatch | Reject |
| Revoked or expired session | Reject |

The proposal follows the current authenticated-session
authorization behavior.

## 4. M4 Integration Review

Existing M4 components:

- IHunterCaptureSessionIdentityResolver
- TrustedHunterCaptureSessionIdentityResolver
- HunterCaptureSessionIdentityResolutionService
- HunterStartAuthorizedCaptureSessionHandler
- HunterConsumeAuthorizedCaptureSessionHandler

The M4 resolver validates a backend-supplied GUID user ID,
loads a trusted M1 account and maps it to HunterId.

The M4 Start and Consume handlers reference the identity
resolution service.

Production dependency injection registration and complete
endpoint behavior are not yet verified.

## 5. Findings Requiring Follow-up

### F1 — Account deletion-state consistency

TrustedIdentityAccessContract.IsActiveAccountOwner checks
Active status and verification state.

It does not explicitly check DeletionRequestedAtUtc.

AuthenticatedSessionAuthorizationService and the JWT
claims factory do check the deletion-request timestamp.

Confirm whether the shared account-owner contract should
reject this state explicitly.

No code change is made in this documentation task.

### F2 — Trusted account reader implementation

ITrustedIdentityAccountReader is defined.

A production concrete implementation and its DI registration
were not identified in the inspected source searches.

This is an unverified integration dependency, not proof
that an implementation is absent.

### F3 — M4 production registration

Existing source searches did not identify production DI
registration for the trusted M4 identity resolver.

The M4 owner must verify the required runtime wiring.

### F4 — HTTP error contract

The SessionBearer authentication handler returns an
authentication failure for invalid or inactive sessions.

The final endpoint-specific HTTP status and error response
contract for M4 operations still requires review.

## 6. Proposed Responsibility Split

### M1 IdentitySocial owner

- Confirm authoritative UserId and identity claim.
- Confirm JWT issuer and audience validation requirements.
- Confirm account and session eligibility rules.
- Confirm trusted identity validation contract.
- Review the deletion timestamp consistency finding.
- Confirm expected authentication failure behavior.

### M4 HunterPlaces owner

- Verify resolver and handler integration.
- Confirm authenticated UserId originates server-side.
- Confirm capture-session authorization and ownership rules.
- Review endpoint error behavior.
- Verify production DI requirements.

### DB Coordinator

- Confirm final M1/M4 database ownership.
- Decide whether a direct capture-session-to-Users FK is required.
- Approve compatible column types and integrity strategy.
- Approve delete/update behavior and shared migration plan.

### Team Leader

- Review the complete cross-module identity contract.
- Approve production integration and any subsequent changes.

## 7. Existing Verification Baseline

Latest reviewed develop commit: edb1297

- Backend build: PASSED
- Full test suite: 2685 total
- Passed: 2682
- Failed: 0
- Skipped: 3
- M1-MS05 SQL runtime verification: merged

These results were collected before this documentation task.
No new runtime tests are claimed by this document.

## 8. Approval and Implementation Status

- [x] Existing M1 authentication code reviewed
- [x] Existing M1 trusted identity code reviewed
- [x] Existing M4 identity resolver code reviewed
- [x] Proposed identity claim contract documented
- [x] Proposed account eligibility contract documented
- [x] Integration gaps recorded
- [ ] M1 owner confirms the proposed decisions
- [ ] M4 owner reviews integration requirements
- [ ] DB Coordinator approves database relationship strategy
- [ ] Team Leader approves final cross-module contract
- [ ] Production registration separately authorized
- [ ] Shared SQL migration separately authorized

**Final status:** REVIEW REQUIRED.

No production implementation, DI registration, schema
modification, or migration is authorized by this document.
