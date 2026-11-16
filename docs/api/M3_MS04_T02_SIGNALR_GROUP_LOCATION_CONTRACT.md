# WAYHUNTER — M3-MS04-T02

## SignalR Group Authorization & Location Events

Module: M3 TripSafety
Owner: Mathi
Task: M3-MS04-T02
Status: Implementation for review
Migration: HOLD

## Acceptance Requirement

A subscriber without active trip membership
and explicit GPS consent must not receive
precise GPS updates.

## SignalR Endpoint

/hubs/trip-location

## Client Hub Methods

### JoinTrip

Arguments:

- tripPlanId: Guid
- sessionId: Guid

Requires:

- Authenticated user
- Active accepted trip membership
- Active session
- Explicit unrevoked GPS consent

### PublishLocation

Arguments:

- tripPlanId: Guid
- sessionId: Guid
- request:
    - latitude: double
    - longitude: double
    - accuracyMeters: double

Sender identity comes from authenticated
server-side claims.

Sender must have joined the same session
and pass authorization again.

### LeaveTrip

Removes the current connection from
its registered trip session.

## Server-to-Client Event

TripLocationUpdated

Payload fields:

- tripPlanId
- sessionId
- userId
- latitude
- longitude
- accuracyMeters
- recordedAtUtc

Timestamp comes from server time.

## Security Rules

1. Client-supplied user identity is never trusted.
2. Invitation acceptance does not grant GPS consent.
3. Membership and consent are checked on subscription.
4. Publisher authorization is checked per event.
5. Every receiver is checked before event delivery.
6. A revoked receiver is removed from the local registry.
7. A paused or finished trip cannot publish updates.
8. Cross-trip and cross-session access is denied.
9. Invalid coordinates are rejected.
10. Precise GPS must not be broadcast blindly
    using Clients.Group.

## Known Integration Limitations

This implementation uses a single-process,
in-memory repository and subscriber registry.

Not yet implemented:

- Production M1 JWT authentication setup
- Canonical session/membership persistence
- T04 production membership revocation binding
- Durable consent storage
- Cross-node authorization consistency
- Atomic authorization and outbound delivery
- Two-device SignalR integration tests
- Subscriber disconnection on revocation
- Horizontal scaling / Redis backplane

Do not treat unit tests as proof of production
SignalR security.

## Verification

dotnet build Wayhunter.slnx

dotnet test Wayhunter.slnx --filter
"FullyQualifiedName~TripLocationSignalRTests"

dotnet test Wayhunter.slnx

Build result: TO VERIFY
Targeted tests: TO VERIFY
Full tests: TO VERIFY
PR status: TO VERIFY

Migration: HOLD
Team Leader approval: PENDING