# WAYHUNTER - M1-M3 Authentication & GPS Security Contract Review

**Owner:** M1 IdentitySocial
**Related Module:** M3 TripSafety
**Date:** 2026-11-18
**Branch:** task/m1-m3-auth-gps-contract-review
**Status:** PROPOSED - CROSS-MODULE REVIEW REQUIRED
**Scope:** Documentation and existing-code review only

## 1. Purpose

Review the authentication and trusted user identity contract
between M1 IdentitySocial and M3 TripSafety.

The review covers:

- JWT authenticated user identity
- GPS consent endpoint authorization
- SignalR live-location identity handling
- Trip membership and explicit GPS consent
- Existing security test coverage
- Pending M1/M3 integration decisions

This document does not approve production code changes,
database schema modifications, or SQL migrations.

## 2. M1 Authentication Contract

The existing M1 authentication implementation uses:

- UserAccount.UserId as the authoritative user identity
- Canonical nonempty GUID string identity
- JWT sub claim as the issued user identity
- JWT sid claim as the authenticated session identifier
- Server-generated ClaimTypes.NameIdentifier compatibility claim

The existing SessionBearer authentication handler creates
both sub and NameIdentifier from the validated user identity.

TrustedIdentityAccessContract rejects:

- Unauthenticated principals
- Missing identity claims
- Duplicate identity claims
- Invalid or empty GUID identities
- Conflicting sub and NameIdentifier values

AuthenticatedSessionAuthorizationService also verifies:

- JWT signature, issuer, audience and lifetime
- Persisted M1 account
- Active and verified account state
- No account deletion request
- Persisted session ownership
- Non-revoked and non-expired session

These checks apply when M1 session authorization executes.

## 3. M3 GPS Consent API Review

Reviewed source:

src/Wayhunter.Api/Controllers/Mobile/TripSafety/TripSharingConsentController.cs

Existing controller behavior:

- Uses Authorize attribute
- Rejects unauthenticated requests
- Reads ClaimTypes.NameIdentifier with sub fallback
- Uses the authenticated identity for own-consent operations
- Returns 403 for unauthorized consent changes
- Delegates membership and consent checks to the service

Existing M3 controller extraction does not itself call
TrustedIdentityAccessContract.TryResolveUserId.

The production SessionBearer handler supplies matching
server-generated identity claims after authentication.

Directly constructed principals and any other approved
authentication schemes require separate contract review.

No production identity bypass is concluded from the
existing evidence.

## 4. M3 SignalR Hub Review

Reviewed source:

src/Wayhunter.Api/Hubs/TripSafety/TripLocationHub.cs

Existing implementation:

- Uses Authorize attribute
- Requires an authenticated principal
- Extracts NameIdentifier with sub fallback
- Checks trip/session access before joining
- Checks publisher access before publishing
- Rechecks recipients before precise GPS delivery
- Excludes revoked or unauthorized recipients
- Cleans up connection subscriptions

The hub does not directly use
TrustedIdentityAccessContract.TryResolveUserId.

Confirm how the final M1 trusted-identity policy should
apply to authenticated SignalR connections.

The review must distinguish authentication at connection
establishment from authorization during live operations.

## 5. Existing Test Coverage

Reviewed test files:

- CrossModuleAuthorizationHttpTests.cs
- ProgramAuthenticationStartupTests.cs
- ValidJwtCrossModuleHttpTests.cs
- TripSharingConsentTests.cs
- TripLocationSignalRTests.cs
- TrustedIdentityAccessContractTests.cs
- TrustedIdentityHardeningTests.cs

Verified existing test scenarios include:

- Anonymous GPS requests rejected
- Missing trusted user claim rejected
- Authenticated accepted member allowed consent
- Non-member consent modification rejected
- Valid JWT used for cross-module GPS consent
- Consent required before live-location access
- Revoked consent blocks future sharing
- Paused and finished trip restrictions
- Unauthorized receiver exclusion
- Invalid location data rejection

The reviewed TripLocationSignalRTests focus mainly on
repository access, subscriber registry, fanout behavior
and location validation.

Direct hub authentication tests for conflicting identity
claims were not identified in the reviewed file.

This is a test coverage review finding, not evidence of
an exploitable production vulnerability.

## 6. Cross-Module Findings

### F1 - Identity Claim Consistency

M1 shared identity validation rejects conflicting,
duplicate and malformed claims.

M3 GPS controller and SignalR hub currently use
NameIdentifier with sub fallback.

Decision required:

Should M3 use a shared trusted identity resolver, or rely
on an explicitly approved authenticated principal contract?

M1 and M3 owners must review the final integration approach.

### F2 - Unit Test Identity Format

Some existing M3 unit tests use synthetic identity strings
such as friend-01.

M1 production-issued identity uses canonical GUID strings.

Direct application of M1 GUID enforcement to these tests
would require coordinated fixture updates.

Do not change M3 test fixtures without M3 owner review.

### F3 - SignalR Authentication Coverage

The reviewed SignalR test file covers many GPS authorization
and fanout behaviors.

Direct tests for duplicate or conflicting principal claims
were not identified.

Confirm whether additional hub-level or integration-level
authentication tests are required.

### F4 - Authorization Responsibilities

M1 owns authentication, persisted account eligibility
and authenticated user identity.

M3 owns trip membership, session permissions, GPS consent,
live-location publishing and receiver authorization.

Successful M1 authentication must never automatically
grant M3 GPS access.

### F5 - GPS Privacy

- GPS consent must be disabled by default.
- Invitation acceptance must not enable GPS automatically.
- Consent must be explicitly granted by the user.
- Revocation must block subsequent authorized delivery.
- Unauthorized users must not receive private GPS data.
- Paused or finished sessions must not publish live GPS.

GPS retention and data deletion durations require
separate approved product and privacy decisions.

## 7. Proposed M1-M3 Contract

1. M1 UserAccount.UserId is the authoritative identity.

2. Production-issued M1 JWT uses sub for the user ID.

3. NameIdentifier is a compatibility claim created by
   trusted backend authentication.

4. Missing, duplicate, invalid or conflicting identity
   claims must not be accepted as trusted user identity.

5. M3 must never use a client-supplied UserId to override
   the authenticated account.

6. Authentication alone must not grant GPS access.

7. M3 must enforce accepted membership, session state
   and explicit GPS consent independently.

8. SignalR connection and live-operation authentication
   and authorization behavior require joint review.

These are proposed contract decisions. Final M3 integration
approval has not been granted by this document.

## 8. Responsibility Split

### M1 IdentitySocial Owner

- Confirm authenticated identity contract.
- Confirm supported claim format.
- Confirm JWT validation requirements.
- Review consistency with shared identity validation.
- Review account/session eligibility expectations.

### M3 TripSafety Owner

- Review GPS controller identity extraction.
- Review SignalR hub authentication behavior.
- Review membership and consent enforcement.
- Confirm required integration and regression tests.
- Own any approved changes to M3 implementation.

### Team Leader

- Review and approve the joint M1-M3 contract.
- Authorize any separately proposed implementation work.

### DB Coordinator

- Review database relationships only if required.
- Approve any subsequent shared schema changes.
- Coordinate migrations separately.

## 9. Existing Verification Baseline

Starting develop commit: 917152e

Previously reported backend verification:

- Total tests: 2695
- Passed: 2686
- Failed: 0
- Skipped: 9
- Backend build: PASSED

These are baseline results from before this review branch.

No new build, integration-test execution or security
penetration test is claimed by this document.

## 10. Approval Checklist

- [x] M1 JWT and trusted identity implementation reviewed
- [x] M3 GPS consent controller reviewed
- [x] M3 SignalR Hub reviewed
- [x] Existing security test coverage reviewed
- [x] Identity consistency findings documented
- [x] Proposed responsibility split documented
- [ ] M1 owner formally confirms proposed contract
- [ ] M3 owner reviews authentication integration
- [ ] Team Leader approves final contract
- [ ] Any production changes separately authorized
- [ ] Any required migration separately authorized

## 11. Final Status

REVIEW REQUIRED.

This task provides M1 owner review evidence only.

No M3 production code changes, SQL schema changes,
EF migrations or GPS retention policies are approved
by this document.
