# WAYHUNTER — M3 Trip Session & History Schema Draft

**Task ID:** M3-MS01-T04  
**Owner:** Mathi (M3)  
**Module:** TripSafety  
**Status:** DRAFT — PENDING SCHEMA REVIEW  
**Migration:** FROZEN

## 1. Purpose

Define the proposed relationships between trip plans, ordered stops, live trip sessions and completed trip history.

Planned dates and actual trip timestamps must remain separate.

## 2. Current M2 TripPlan Entity

Owner: M2 — TravelPlanning

| Field | Current C# Type | Notes |
|---|---|---|
| Id | Guid | Primary identifier |
| OwnerUserId | string | User reference |
| TripName | string | Trip name |
| StartDate | DateOnly | Planned start date |
| EndDate | DateOnly | Planned end date |
| DraftStatus | TripDraftStatus | Draft lifecycle |
| BudgetLkr | decimal | Planned budget |
| VehicleType | string | Planned vehicle |
| Headcount | int | Planned group size |

M2 owns TripPlan persistence and its final database mapping.

## 3. Current M2 TripStop Entity

| Field | Current C# Type | Notes |
|---|---|---|
| Id | Guid | Stop identifier |
| TripPlanId | Guid | Parent TripPlan |
| OrderIndex | int | Stop ordering |
| Name | string | Stop name |
| PlaceId | string? | Optional place reference |
| Latitude | double? | Optional coordinate |
| Longitude | double? | Optional coordinate |

Proposed schema constraints:

- Every stop belongs to one TripPlan.
- Preserve stop order.
- Review a unique constraint on TripPlanId + OrderIndex.
- Coordinates may be null together.
- Stop order must not be negative.

Final constraints require M2 approval.

## 4. Current M3 LiveTripSession Entity

Owner: M3 — TripSafety

| Field | Current C# Type | Notes |
|---|---|---|
| SessionId | Guid | Session primary identifier |
| TripPlanId | string | Temporary type; mismatch pending |
| Status | LiveTripStatus | Active / Paused / Finished |
| StartedAtUtc | DateTimeOffset | Actual start timestamp |
| FinishedAtUtc | DateTimeOffset? | Actual finish timestamp |

Existing EF configuration proposes table:

`TripSafety_LiveTripSessions`

Existing configuration also defines a filtered unique index on TripPlanId for Active and Paused states.

This does not mean the database or migration has been created.

## 5. M2–M3 ID Contract Blocker

Current code:

- M2 TripPlan.Id = Guid
- M3 LiveTripSession.TripPlanId = string

**Proposed decision:** Use Guid consistently.

Before implementing a SQL foreign key:

1. M2 must review the shared identifier.
2. M3 must review the required changes.
3. Team Leader / DB Coordinator must approve.
4. Affected C# entities, services, repositories and tests must be aligned.

Do not generate a foreign key between incompatible types.

## 6. Planned vs Actual Time

Planned trip dates:

- TripPlan.StartDate
- TripPlan.EndDate

Actual trip times:

- LiveTripSession.StartedAtUtc
- LiveTripSession.FinishedAtUtc

Rules:

- Creating a plan must not count as starting a trip.
- Actual start time comes from the Backend.
- Actual finish time comes from the Backend.
- A trip must not finish before its actual start.
- Missing GPS data must not generate fabricated trip history.

## 7. Proposed Trip History

Initial proposal: Completed LiveTripSession records provide session history.

Required history information:

- SessionId
- TripPlanId
- StartedAtUtc
- FinishedAtUtc
- Final session status

Whether an additional TripHistory table is needed remains pending.

Do not physically delete completed sessions merely to remove them from active-trip views.

## 8. Relationships and Constraints

Proposed relationships:

- One TripPlan has multiple ordered TripStops.
- One TripPlan may have historical LiveTripSessions.
- Only one open Active or Paused session is allowed per TripPlan.
- A finished session must not block future history merely because it shares a TripPlanId.
- Trip owner and accepted-member authorization are enforced by Backend services.

Pending database decisions:

- Shared TripPlanId type
- Foreign-key delete behavior
- TripHistory storage strategy
- Retention policy
- Final table and index names
- SQL Server migration ownership

## 9. Schema Freeze Requirements

Before database migration:

- M1 UserId contract review
- M2 TripPlan / TripStop relationship review
- M3 session and history review
- Shared schema constraint review
- Seven explicit member approvals
- Team Leader-appointed migration coordinator

## 10. Migration Audit

- Shared migration generated in this task: NO
- Shared migration executed in this task: NO
- SQL database updated in this task: NO

Existing local migration history must be reported separately.

## 11. Approval

- Mathi (M3): PENDING
- Thadsha (M2): PENDING
- Shayanthan (M1): PENDING
- Team Leader / DB Coordinator: PENDING

**Final Status: DRAFT — PENDING REVIEW**

No SQL migration, database update or shared DbContext change is authorized by this document.