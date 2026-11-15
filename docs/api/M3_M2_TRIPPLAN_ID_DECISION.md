
# WAYHUNTER - M2/M3 TripPlan ID Contract

Status: IMPLEMENTED - FORMAL APPROVAL PENDING

Owners:
- M2 Travel Planning: Thadsha
- M3 Trip Safety: Mathi
- Final Approval: Team Leader / DB Coordinator

Last Reviewed: 2026-11-15

## 1. Purpose

This document defines the shared TripPlan ID contract
between M2 Travel Planning and M3 Trip Safety.

M2 creates and owns Trip Plans.

M3 manages Live Trip Sessions linked to those plans.

Both modules must use the same TripPlan ID type.

## 2. Current Implementation

### M2 Travel Planning

Entity:
`Wayhunter.Domain.Modules.TravelPlanning.Entities.TripPlan`

Property:

`public Guid Id { get; private set; }`

Database configuration:
`TripPlanConfiguration.cs`

Table: `Trips`

Primary key: `Id`

ID type: `Guid`

### M3 Trip Safety

Entity:
`Wayhunter.Domain.Modules.TripSafety.Entities.LiveTripSession`

Property:

`public Guid TripPlanId { get; }`

Database configuration:
`LiveTripSessionConfiguration.cs`

Table: `TripSafety_LiveTripSessions`

Primary key: `SessionId`

TripPlan ID type: `Guid`

SQL column type: `uniqueidentifier`

### Contract Result

M2 TripPlan.Id = Guid

M3 LiveTripSession.TripPlanId = Guid

The previous string-versus-Guid type mismatch
has been corrected in application code.

## 3. Implementation Status

The following changes are merged into develop:

- M3 domain entity GUID alignment
- Repository interface GUID alignment
- Service interface GUID alignment
- Application service GUID alignment
- DTO GUID alignment
- Validation contract GUID alignment
- In-memory repository GUID alignment
- EF Core column type alignment
- Related unit test updates
- Reminder service GUID alignment

GitHub PR:
https://github.com/naanga7peru/Wayhunter_mobile_application_Backend/pull/91

PR Status: MERGED

Merge Date: 2026-11-15

## 4. Module Ownership Rules

M2 owns:
- Trip Plan creation
- Trip Plan editing
- Trip Plan ownership information
- Trip Plan persistence
- Trip Plan lifecycle rules

M3 owns:
- Live Trip Session creation
- Live Trip Session pause
- Live Trip Session resume
- Live Trip Session finish
- Group trip safety
- GPS sharing consent validation

M3 must not generate or replace the M2 TripPlan ID.

M3 receives the existing M2 TripPlan ID.

## 5. Start Trip Requirements

Before creating a Live Trip Session,
the backend must verify:

1. TripPlanId is a valid non-empty Guid.
2. The Trip Plan exists in M2.
3. The authenticated user has permission
   to start the requested trip.
4. The trip is eligible to start.
5. No duplicate open session is created.

M3 currently contains open-session
duplicate-prevention logic.

M2 ownership and trip eligibility integration
must be verified before production use.

## 6. Database Relationship

Expected relationship:

Principal table:
`Trips`

Principal key:
`Trips.Id`

Dependent table:
`TripSafety_LiveTripSessions`

Foreign key:
`TripSafety_LiveTripSessions.TripPlanId`

Relationship:

One Trip Plan can have multiple historical
Live Trip Sessions.

Only one Active or Paused session is allowed
for the same TripPlanId.

The current M3 configuration contains a
filtered unique index for open sessions.

The actual M2/M3 foreign key relationship
has not yet been configured in the shared
EF Core model.

Status: PENDING DB INTEGRATION

Foreign key delete behavior requires approval
from M2 and the DB Coordinator.

## 7. Shared DbContext

The final database integration must register
both M2 and M3 entity configurations in an
approved EF Core DbContext.

The shared DbContext ownership and registration
strategy must be decided by the Team Leader
and DB Coordinator.

Status: PENDING

M3 developers must not create an independent
production migration for this relationship
without coordination.

## 8. Migration Rules

Database migrations must not be executed
until the following are complete:

- M2/M3 contract reviewed
- M2/M3 foreign key design approved
- Shared DbContext configured
- Relevant module mappings reviewed
- Solution build successful
- Unit tests passed
- Team Leader / DB Coordinator approval

Production and existing development database
changes require appropriate backup and review.

## 9. Verification

Implementation:

- [x] M2 TripPlan.Id uses Guid
- [x] M3 LiveTripSession.TripPlanId uses Guid
- [x] M3 EF mapping uses uniqueidentifier
- [x] M3 GUID alignment PR merged into develop

Integration:

- [ ] M2/M3 FK configured in shared EF model
- [ ] M2 ownership authorization verified
- [ ] M2 trip eligibility verification completed
- [ ] All relevant module schema mappings reviewed
- [ ] Shared DbContext integration completed
- [ ] Full unit test results recorded
- [ ] Database migration generated and reviewed
- [ ] Database migration applied

## 10. Approval Status

M2 Thadsha:
PENDING FORMAL REVIEW

M3 Mathi:
PENDING FORMAL SIGN-OFF

Team Leader:
PENDING APPROVAL

DB Coordinator:
PENDING MIGRATION APPROVAL

A merged PR confirms code integration.
It does not automatically confirm formal
contract approval or database migration approval.

## 11. Next Actions

1. M2 Thadsha reviews the current Guid contract.
2. M3 Mathi prepares FK integration changes
   without executing migrations.
3. DB Coordinator confirms shared DbContext design.
4. Team Leader reviews cross-module relationships.
5. Team runs build and tests.
6. Approved migration is generated and reviewed.
7. Database migration is applied only after approval.
