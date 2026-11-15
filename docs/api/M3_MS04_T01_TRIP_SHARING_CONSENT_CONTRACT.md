
# WAYHUNTER — M3-MS04-T01

## Trip Sharing Consent & Revocation API

Module: M3 TripSafety
Owner: Mathi
Task: M3-MS04-T01
Status: Implementation for review
Migration: HOLD
Date: 2026-11-15

## 1. Acceptance Requirement

Only the authenticated and currently active,
accepted trip member may change their own
GPS sharing consent.

Invitation acceptance must never automatically
enable location sharing.

## 2. API Endpoint

PUT /api/mobile/trips/{tripPlanId}/sessions/{sessionId}/gps-consent

### Grant GPS Sharing

Request:

{
  "isGranted": true
}

### Revoke GPS Sharing

Request:

{
  "isGranted": false
}

### Response

GpsConsentDto contains:

- SessionId
- UserId
- IsGranted
- GrantedAtUtc
- RevokedAtUtc

UserId must come from authenticated claims.
It must not be accepted from the request body.

## 3. HTTP Status Codes

200 - Consent successfully updated.

400 - Invalid request or identifiers.

401 - Missing authenticated identity.

403 - User is not an active trip member
      or does not have permission.

404 - Live trip session not found.

409 - Session finished or invalid
      consent state transition.

## 4. Business Rules

1. GPS sharing is disabled by default.

2. Accepting a trip invitation does not
   enable GPS consent.

3. Only the authenticated user may
   change their own consent.

4. Membership must be active.

5. Consent Grant is idempotent.

6. Consent Revoke is idempotent.

7. Revoked membership must prevent
   future GPS consent changes.

8. Invalid or unrelated sessions
   must be rejected.

9. Consent must use trusted server time.

10. GPS publishing must separately verify
    session status, membership, and consent.

## 5. Automated Tests

Test file:

tests/Wayhunter.UnitTests/Modules/TripSafety/
TripSharingConsentTests.cs

Build:

dotnet build Wayhunter.slnx

Targeted tests:

dotnet test Wayhunter.slnx --filter
"FullyQualifiedName~TripSharingConsent"

Full tests:

dotnet test Wayhunter.slnx

## 6. Test Evidence

Build: TO VERIFY

Targeted tests: TO VERIFY

Full test suite: TO VERIFY

Failed tests: TO VERIFY

GitHub PR: TO VERIFY

Do not mark as passed without actual output.

## 7. Integration Limitations

This implementation uses a development
in-memory repository.

It is not yet connected to the canonical
invitation, membership and trip session storage.

Pending integration:

- M1 real authentication setup
- Production membership repository
- Production trip session repository
- T04 membership revocation integration
- SignalR location event authorization
- SQL persistence and transactions
- End-to-end device tests

Production GPS sharing is not ready.

## 8. Migration and Approval

EF Core migration: HOLD

Database update: HOLD

Team Leader approval: PENDING

PR review: PENDING

Task completion must be based on
verified tests and integration evidence.
