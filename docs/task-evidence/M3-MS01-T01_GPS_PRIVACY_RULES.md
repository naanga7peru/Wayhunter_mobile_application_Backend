# WAYHUNTER — GPS Consent & Privacy Rules

**Task ID:** M3-MS01-T01  
**Owner:** Mathi (M3)  
**Module:** Active Trip, Safety & Notifications  
**Status:** DRAFT — PENDING REVIEW  
**Migration Required:** NO

## 1. GPS Consent Rules

- GPS sharing must be disabled by default.
- Every trip member must explicitly grant their own location-sharing consent.
- Accepting a trip invitation must not automatically enable GPS sharing.
- Consent can be revoked by the user.
- Revoked consent must stop further location sharing.
- Trip completion must stop live location publishing.
- Consent must not bypass trip membership and authorization checks.

## 2. Live GPS Privacy

- Only authorized trip members may access permitted shared locations.
- An unauthorized or removed member must not receive live GPS data.
- A stale location must not be shown as a current live position.
- GPS updates must not restart automatically after consent revocation.
- Pausing a trip must stop live location publishing.

## 3. Backend Implementation Status

Implemented as domain logic:

- `TripGpsConsent.CreateDisabled()`
- `TripGpsConsent.Grant()`
- `TripGpsConsent.Revoke()`
- Consent state and timestamps
- Domain unit tests

Not yet implemented:

- Authentication and membership authorization
- Real GPS publishing
- Consent persistence in SQL Server
- Revocation enforcement in live connections
- Device-level end-to-end tests

## 4. Data Retention — Decisions Pending

The following require team approval:

- Raw GPS coordinate retention period
- Whether and how location history is stored
- Consent audit-record retention period
- Location-data deletion and cleanup policy
- Stale GPS timeout threshold

No retention duration is assumed approved.

## 5. Required Test Scenarios

- Invitation accepted without GPS consent
- Consent granted explicitly
- Consent revoked
- Duplicate grant and revoke
- Unauthorized location viewer rejected
- Trip paused
- Trip finished
- Reconnect after revocation
- Stale GPS handling

## 6. Approval

- Mathi (M3): PENDING
- M1 Identity/Auth Review: PENDING
- Team Leader: PENDING
- Peer Code Review: PENDING

**Final Status: DRAFT — NOT YET ACCEPTED**

No database migration is authorized by this document.