# WAYHUNTER — M3 Trip, Friends & GPS API Contract

**Task:** M3-MS01-T05  
**Owner:** Mathi (M3)  
**Status:** DRAFT — PENDING CROSS-MODULE APPROVAL  
**Migration:** NOT REQUIRED

## 1. Module Ownership

- M1 IdentitySocial owns authenticated user identity and social relationships.
- M2 TravelPlanning owns TripPlan and ordered trip stops.
- M3 TripSafety owns live trip sessions, trip invitations, accepted membership, GPS consent and live sharing rules.

## 2. Current ID Contracts

| Field | Current implementation |
|---|---|
| M2 TripPlan.Id | Guid |
| M2 TripPlan.OwnerUserId | string |
| M3 LiveTripSession.TripPlanId | string |
| M3 LiveTripSession.SessionId | Guid |
| M3 TripGpsConsent.SessionId | Guid |
| M3 TripGpsConsent.UserId | string |

### Pending Decision

**Proposal:** Use Guid for the shared TripPlan ID in both M2 and M3.

Existing M3 types must not change without M2 and Team Leader approval.

## 3. Proposed Trip DTO

- TripPlanId
- OwnerUserId
- TripName
- PlannedStartDate
- PlannedEndDate
- TripStatus

The exact field types and public API names require M2 review.

## 4. Proposed Session DTO

- SessionId: Guid
- TripPlanId: pending shared ID decision
- SessionStatus: Active, Paused or Finished
- StartedAtUtc: server timestamp
- FinishedAtUtc: nullable server timestamp

## 5. Proposed Trip Member DTO

- TripPlanId
- MemberUserId
- MembershipStatus
- InvitationReference
- AcceptedAtUtc

Membership acceptance must not automatically grant GPS consent.

## 6. Proposed GPS Consent DTO

- SessionId: Guid
- UserId: string
- IsGranted: boolean
- GrantedAtUtc: nullable timestamp
- RevokedAtUtc: nullable timestamp

Consent must belong to the authenticated user.

## 7. Security Requirements

- Only authenticated users may act on their own location-sharing consent.
- Trip ownership and accepted membership must be validated.
- Inviting or accepting a member must never enable GPS automatically.
- Revoked users must stop receiving newly published private GPS updates.
- Finished trips must stop live location publishing.
- Sensitive GPS data must not be returned to unauthorized users.
- Client-provided UserId must never replace authenticated identity.

## 8. API Operations — Proposed

- StartTrip
- PauseTrip
- ResumeTrip
- FinishTrip
- InviteTripMember
- AcceptTripInvitation
- RevokeTripMembership
- GrantGpsConsent
- RevokeGpsConsent
- GetLiveTripSession
- GetAuthorizedSharedLocations

Exact HTTP routes and request/response schemas are pending review.

## 9. Implementation Dependencies

- M1 authentication and user identity contract
- M2 TripPlan ID and ownership contract
- M3 session lifecycle contract
- Shared schema freeze and migration approval for persistence
- Authorization tests before real GPS integration

## 10. Review & Approval

- Mathi (M3): PENDING
- Thadsha (M2): PENDING
- Shayanthan (M1): PENDING
- Team Leader / DB Coordinator: PENDING

**Task remains pending review. No API endpoints or SQL migrations are authorized by this document alone.**