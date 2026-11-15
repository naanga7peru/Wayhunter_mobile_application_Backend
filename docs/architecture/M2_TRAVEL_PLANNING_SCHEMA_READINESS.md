

\# WAYHUNTER — M2 Travel Planning Schema Readiness



Status: PENDING REVIEW — MIGRATION HOLD

Date: 2026-11-15

Owner: M2 Thadsha

Related Modules: M1 Identity, M3 Trip Safety

Final Approval: PENDING



\## 1. M2 Database Tables



\### Trips



Entity: TripPlan

Configuration: TripPlanConfiguration.cs



\- Id: Guid, Primary Key

\- OwnerUserId: string, required, max 256

\- TripName: string, required, max 200

\- StartDate: date, required

\- EndDate: date, required

\- BudgetLkr: decimal(18,2)

\- VehicleType: string, required, max 100

\- Headcount: int, required

\- DraftStatus: int, required

\- Index: OwnerUserId



\### TripStops



Entity: TripStop

Configuration: TripStopConfiguration.cs



\- Id: Guid, Primary Key

\- TripPlanId: Guid, required

\- OrderIndex: int, required

\- Name: string, required, max 200

\- PlaceId: string, optional, max 256

\- Latitude: float, optional

\- Longitude: float, optional



\## 2. Existing Relationships



\- TripStops.TripPlanId references Trips.Id

\- Delete behavior: Cascade

\- Unique Index: (TripPlanId, OrderIndex)



\## 3. M2/M3 TripPlanId Contract



\- M2 TripPlan.Id: Guid

\- M2 TripStop.TripPlanId: Guid

\- M3 LiveTripSession.TripPlanId: Guid



M3 Guid alignment: PR #91 merged.



Code alignment: IMPLEMENTED

Formal shared schema approval: PENDING



\## 4. Pending Schema Decisions



\- M1 UserAccount.UserId to Trips.OwnerUserId FK

\- M3 LiveTripSession.TripPlanId to Trips.Id FK

\- Cross-module delete behavior

\- Optimistic concurrency / RowVersion design

\- Database-level date, budget and headcount constraints

\- M4 PlaceId relationship

\- Final table and index review



\## 5. Migration-Dependent Tasks



M2-MS02-T01:

EF configurations implemented.

Actual migration and SQL validation BLOCKED.



M2-MS02-T02:

Initial CRUD implementation merged.

Real SQL and ownership integration pending.



M2-MS02-T06:

Draft persistence and optimistic concurrency pending.



M2-MS02-T07:

Regression QA pending.

In-memory tests may run before migration.



\## 6. Local Migration Status



\- Migration created: NO

\- Database update executed: NO

\- SQL integration verified: NO

\- Current status: MIGRATION HOLD



\## 7. Evidence



\- TripPlan.cs

\- TripStop.cs

\- TripPlanConfiguration.cs

\- TripStopConfiguration.cs

\- TravelPlanningEfModelTests.cs

\- M2 PR #15

\- M3 Guid alignment PR #91



\## 8. Approval Status



M2 evidence document: PREPARED FOR REVIEW

M1 relationship approval: PENDING

M3 relationship approval: PENDING

DB Coordinator approval: PENDING

Team Leader approval: PENDING

Seven-member Schema Freeze: PENDING



No migration is authorized by this document.



