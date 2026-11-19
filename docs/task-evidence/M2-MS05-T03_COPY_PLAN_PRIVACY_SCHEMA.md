# M2-MS05-T03 — Copy suggested plan API (REVIEW REQUIRED)

## KEEP / MODIFY / ADD
- KEEP: Existing M2 create/update/get endpoints, M3 membership and actual finish authority, source plan data.
- MODIFY: TripPlan with immutable source reference fields; optional EF column mappings; DI registrations.
- ADD: authenticated copy endpoint, application service, fail-closed store, gated SQL implementation, unit tests.

## Route and behavior
- POST `/api/mobile/trip-plans/suggested/{sourceTripPlanId}/copy` (bearer-authenticated).
- Success: 201 with new plan ID, source plan ID, copied ordered stops, new RowVersion.
- Missing/unauthorized/unverified source or flag disabled: 404 (do not reveal private trip existence).
- Copies planned stops into a new editable owner-owned draft; original trip/stops remain untouched.
- Source owner UserId and source TripPlanId are saved server-side; source owner identity not returned.
- No memories, live GPS, GPS consent, session timestamps or route tracking data are copied.

## Critical blocker: explicit reuse consent
- An ACTIVE trip membership authorizes private trip viewing, **not necessarily reuse**.
- SQL store is **NOT registered by default**. To enable it, an authorized owner decision must
  explicitly approve membership-based copying of planned stop coordinates and SQL schema migration.
- `TravelPlanning:EnableMemberPlanCopy=true` is only a technical gate AFTER such approval;
  it is NOT itself proof that the source creator consented. If approval does not establish
  that membership grants reuse, build an explicit per-trip grant/consent system instead.
- Also require `TravelPlanning:EnableSqlPersistence=true` and
  `TripSafety:EnableSqlPersistence=true`.
- Existing suggested-plan cards (T01) are separate; a card appearing does not authorize copy.

## Proposed separate SQL Server table (NOT EXECUTED)
Requires DB coordinator review and a normal, reviewed EF migration:

```sql
CREATE TABLE dbo.TripPlanCopyAttributions (
    TripPlanId uniqueidentifier NOT NULL PRIMARY KEY,
    SourceTripPlanId uniqueidentifier NOT NULL,
    SourceOwnerUserId nvarchar(256) NOT NULL,
    CopiedAtUtc datetimeoffset NOT NULL,
    CONSTRAINT CK_TripPlanCopies_NotOwnSource
        CHECK (TripPlanId <> SourceTripPlanId),
    CONSTRAINT FK_TripPlanCopyAttributions_Trips_TripPlanId
        FOREIGN KEY (TripPlanId) REFERENCES dbo.Trips(Id) ON DELETE CASCADE
);
CREATE INDEX IX_TripPlanCopyAttributions_SourceTripPlanId
    ON dbo.TripPlanCopyAttributions(SourceTripPlanId);
```

- We do **NOT** add columns to existing `Trips`. The new TripPlan provenance
  properties are ignored by EF; all existing Trips SQL queries keep working.
- The new table stores immutable attribution in the same transaction as copy.
- Do NOT run SQL blindly: inspect deployments, migration history and rollback first.
- Existing source Trips are untouched. Do not enable SQL copying until migration.
- This patch intentionally does not create/apply a shared EF migration.

## SQL correctness / risks to review
- Serializable transaction revalidates membership + genuine M3 completion at copy time.
- SQL execution strategy retries could duplicate a copy if commit succeeds but acknowledgment
  is lost. If exactly-once behavior is required, add an approved client idempotency key
  and durable uniqueness contract before enabling in production.
- Verify SQL translation, joins, cancellation and RowVersion with a dedicated test DB.
- Require a deliberate rule for completed-trip ownership, stop edits after finish,
  reuse consent/revocation, location privacy and provenance retention.
- This is an implementation candidate; NOT production-ready or a product approval.

## Suggested tests
- xUnit: new IDs, preserved source attribution, ordering, copying/editing never mutates original,
  invalid identity, fail closed, no source-owner identity in response.
- SQL integration (pending): membership removed, invitation only, incomplete/paused/cancelled,
  source owner, concurrent revocation, provider failure and actual database column presence.
- `dotnet build Wayhunter.slnx`
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --filter FullyQualifiedName~CopySuggestedTripPlanServiceTests`
- `dotnet test Wayhunter.slnx --no-restore`

## Reviewer decisions required
M2/M3/DB coordinator/Kaviyan: approve exact reuse permission policy and schema before merge
if downstream deployments assume SQL tables are migrated.
