
# WAYHUNTER — M2/M3 TripPlan ID Contract

**Owners**
- M2 Travel Planning: Thadsha
- M3 Trip Safety: Mathi
- Final Review: Team Leader / DB Coordinator

**Implementation status:** GUID alignment merged
**Current code checkpoint:** develop cba9dd4
**Schema approval:** Pending formal review
**Migration:** HOLD

## 1. Purpose

This document records the shared TripPlan ID contract
between M2 Travel Planning and M3 Trip Safety.

M2 owns TripPlan creation and persistence.

M3 owns live session and trip safety persistence.

Both modules use the same TripPlan ID type.

## 2. Actual M1 User Identity Contract

Entity:

`Wayhunter.Domain.Modules.IdentitySocial.Entities.UserAccount`

Property:

`UserId: string`

Existing EF mapping:

`UserAccountConfiguration.cs`

Table:

`Users`

Primary key:

`UserId`

Maximum length:

`256`

M3 string user references must be compatible with
this current M1 contract.

## 3. Actual M2 TripPlan Contract

Entity:

`Wayhunter.Domain.Modules.TravelPlanning.Entities.TripPlan`

Property:

`Id: Guid`

Configuration:

`TripPlanConfiguration.cs`

Table:

`Trips`

Primary key:

`Id`

SQL type:

`uniqueidentifier`

## 4. Actual M3 Live Session Contract

Entity:

`LiveTripSession`

Properties:

- SessionId: Guid
- TripPlanId: Guid
- Status: LiveTripStatus
- StartedAtUtc: DateTimeOffset
- FinishedAtUtc: DateTimeOffset?

Configuration:

`LiveTripSessionConfiguration.cs`

Table:

`TripSafety_LiveTripSessions`

Primary key:

`SessionId`

Foreign key:

`TripSafety_LiveTripSessions.TripPlanId`
references `Trips.Id`.

Proposed delete behavior:

`NoAction`

Open-session unique index:

`UX_TripSafety_LiveTripSessions_OpenPlan`

Filter:

`[Status] IN (1, 2)`

## 5. GUID Alignment Evidence

Merged PR:

https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/91

Foreign key correction:

https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/110

Implementation evidence:

- M2 TripPlan.Id uses Guid.
- M3 LiveTripSession.TripPlanId uses Guid.
- M3 SQL type is uniqueidentifier.
- EF model contains the TripPlan relationship.
- Metadata tests verify the relationship.

## 6. M3 Persistence Relationships

The approved EF mapping task adds:

- TripInvitation -> Trips
- TripInvitation.SenderUserId -> Users.UserId
- TripInvitation.RecipientUserId -> Users.UserId
- TripMember -> Trips
- TripMember.MemberUserId -> Users.UserId
- TripMember.InvitationId -> TripInvitations
- TripGpsConsent.SessionId -> LiveTripSessions
- TripGpsConsent.UserId -> Users.UserId
- HazardReport.ReporterUserId -> Users.UserId
- HazardReport.VerifiedByUserId -> Users.UserId
- HazardReport.ResolvedByUserId -> Users.UserId

The proposed table names, indexes, constraints and
delete behavior require final shared schema approval.

## 7. Ownership Rules

M2 owns:

- TripPlan creation
- TripPlan editing
- TripPlan owner information
- TripPlan persistence

M3 owns:

- Trip invitation lifecycle
- Accepted trip membership
- Live session lifecycle
- GPS sharing consent
- Group trip safety

M3 must not create or replace M2 TripPlan IDs.

## 8. Shared DbContext

The existing `WayhunterSharedDbContext` contains:

- M1 Identity mappings
- M2 TravelPlanning mappings
- M3 LiveTripSession mapping
- Other existing module mappings

The M3 EF readiness task adds the four remaining
TripSafety configurations.

This is model configuration work only.

## 9. Approval Status

GUID implementation: MERGED

FK implementation: MERGED

Formal M2/M3 contract sign-off: PENDING

Seven-member shared schema approval: PENDING

DB Coordinator approval: PENDING

Team Leader migration authorization: NOT GRANTED

## 10. Migration Restrictions

No migration generation is authorized by this task.

No database update is authorized.

Only the appointed DB Coordinator may perform
the approved consolidated migration after all
required member approvals.

**Final status: EF implementation review pending;
shared migration remains HOLD.**
