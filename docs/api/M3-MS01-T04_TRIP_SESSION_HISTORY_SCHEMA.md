
# WAYHUNTER — M3 Trip Session & History Schema

**Task ID:** M3-MS01-T04
**Owner:** Mathi (M3)
**Module:** Trip Safety
**Date reviewed:** 2026-11-16

**Domain implementation:** Present
**EF configuration:** Present
**Shared model integration:** Present for LiveTripSession
**Formal schema approval:** Pending
**Migration:** HOLD

## 1. Purpose

Record the actual M2/M3 relationship between planned
TripPlans, ordered stops and actual live trip sessions.

Planned dates and actual session timestamps remain separate.

## 2. M2 TripPlan

Entity:

`TripPlan`

Owner: M2 Travel Planning

| Property | Type |
|---|---|
| Id | Guid |
| OwnerUserId | string |
| TripName | string |
| StartDate | DateOnly |
| EndDate | DateOnly |
| BudgetLkr | decimal |
| VehicleType | string |
| Headcount | int |
| DraftStatus | TripDraftStatus |

Table: `Trips`

Primary key: `Id`

M2 owns TripPlan persistence.

## 3. M2 TripStop

Entity:

`TripStop`

| Property | Type |
|---|---|
| Id | Guid |
| TripPlanId | Guid |
| OrderIndex | int |
| Name | string |
| PlaceId | string? |
| Latitude | double? |
| Longitude | double? |

Table: `TripStops`

TripPlan relationship is configured by M2.

## 4. M3 LiveTripSession

Entity:

`LiveTripSession`

| Property | Type |
|---|---|
| SessionId | Guid |
| TripPlanId | Guid |
| Status | LiveTripStatus |
| StartedAtUtc | DateTimeOffset |
| FinishedAtUtc | DateTimeOffset? |

Table:

`TripSafety_LiveTripSessions`

Primary key: `SessionId`

TripPlan foreign key:

`TripPlanId -> Trips.Id`

SQL type:

`uniqueidentifier`

Status:

- Active = 1
- Paused = 2
- Finished = 3

Filtered unique index:

`UX_TripSafety_LiveTripSessions_OpenPlan`

Only one open session per TripPlan is allowed.

## 5. Live Session Timestamp Rules

- Creating a TripPlan is not starting a live trip.
- StartedAtUtc is the actual server-side start time.
- FinishedAtUtc is nullable until completion.
- Finish cannot precede session start.
- Completed sessions remain available as historical records.
- Missing GPS data must not generate fabricated history.

## 6. Additional TripSafety EF Mappings

The M3 persistence mapping foundation includes:

- TripInvitation
- TripMember
- TripGpsConsent
- HazardReport

These mappings use:

- M2 TripPlan.Id as Guid
- M1 UserAccount.UserId as string (max 256)
- M3 SessionId as Guid

Relationships and proposed delete behavior must receive
cross-module schema review.

## 7. History and Retention

A completed LiveTripSession remains a historical session.

Whether a separate TripHistory table is required
is not approved by this mapping task.

GPS coordinate retention and deletion durations
remain decision-required.

Do not invent a retention period.

## 8. Migration Status

EF model preparation: In progress

Metadata verification: Pending task test results

Shared schema review: Pending

Migration generated: No

Database updated: No

## 9. Approval

M2 review: Pending

M3 review: Pending

M1 identity contract review: Pending

DB Coordinator: Pending

Team Leader migration authorization: Not granted

**Final status: Documentation corrected for the actual
GUID implementation; database migration remains HOLD.**
