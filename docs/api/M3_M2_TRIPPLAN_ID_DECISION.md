# WAYHUNTER - M2/M3 TripPlan ID Contract

Status: PENDING REVIEW
Owners: M2 Thadsha, M3 Mathi

## Current Implementation
- M2 TripPlan.Id: Guid
- M3 LiveTripSession.TripPlanId: string
- M3 EF mapping: nvarchar(128)

## Proposed Decision
- Use Guid as the shared TripPlan ID.
- M2 owns TripPlan creation.
- M3 owns LiveTripSession.
- Validate trip ownership before Start.
- Prevent duplicate open sessions.
- No shared migration without team approval.

## Changes After Approval
1. Update M3 domain entity.
2. Update repository interfaces and service.
3. Update repository implementations.
4. Update EF configuration.
5. Update unit tests.

## Approval Status
M2 Thadsha: PENDING
M3 Mathi: PENDING
Team Leader / DB Coordinator: PENDING
