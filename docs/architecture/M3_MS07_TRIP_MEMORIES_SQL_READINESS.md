# M3-MS07 read-only schema reconciliation and SQL test plan

Status: M2 approved the logical stop-retention contract. Physical schema, deployment sequencing, and the test target and guards are DECISION_REQUIRED from the DB Coordinator. This report authorizes no DDL.

## Branch and comparison

PR #267 head `91abe1861b893caccf3c1b68240d91cb30cd65be` was compared with fetched `origin/develop` at `0598adddb03b0cc89061595975ec31c2c4707e0c`. The feature is one commit ahead and four behind. Develop added M7 advertisement-image work. The read-only merge preview identified edits to `Program.cs` on both branches at different locations and showed no conflict markers. No branches were integrated. The model comparison below applies to this feature worktree. Recompute it after any separately approved sync, including the M7 media changes.

## Read-only EF evidence

Compared the SQL Server design-time relational model with the checked-in EF snapshot using EF Core's in-memory relational model differ. Inspected table, FK, check and index metadata. No database connection, migration generation, DDL, or change to the snapshot was involved. The temporary inspection test was removed. The baseline is `20261117093023_InitialSharedSchema`; deployed database state is outside this evidence.

| Proposed addition | Metadata |
|---|---|
| `AK_TripStops_Id_TripPlanId` | Unique alternate key on `TripStops(Id, TripPlanId)`. Existing stop PK, trip owner authorization, and `(TripPlanId, OrderIndex)` uniqueness are unchanged. `TripPlanId` becomes a tracked key property in this model. |
| `TripSafety_TripMemories` | `PK_TripSafety_TripMemories` on application-assigned GUID `MemoryId`. Required GUID `TripPlanId` and `CreateOperationId`; nullable GUID `TripStopId`; required `nvarchar(256)` owner and `nvarchar(1000)` caption; integer visibility with Private default; required `datetimeoffset` create/update, nullable delete timestamp; required SQL-generated `rowversion`. |
| Memory FKs | `FK_TripSafety_TripMemories_Trips_TripPlanId` to Trips.Id, `FK_TripSafety_TripMemories_Users_OwnerUserId` to Users.UserId, and `FK_TripSafety_TripMemories_TripStops_TripStopId_TripPlanId` to the composite alternate key. SQL action is NO ACTION for all. The stop FK uses EF ClientNoAction to preserve the optional link on tracked dependents. |
| Memory indexes | `IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreateOperationId` unique, including tombstones; `IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreatedAtUtc` filtered on `DeletedAtUtc IS NULL`; nonunique `IX_TripSafety_TripMemories_OwnerUserId` and `IX_TripSafety_TripMemories_TripStopId_TripPlanId`. |
| Memory checks | `CK_TripSafety_TripMemories_Visibility` permits 0/1. `CK_TripSafety_TripMemories_Timestamps` requires update and any delete timestamp to be at or after create. |
| `TripSafety_TripMemoryMedia` | `PK_TripSafety_TripMemoryMedia` on application-assigned GUID `OperationId`. Required GUID `MemoryId`, `nvarchar(64)` MIME, `bigint` length, `datetimeoffset` creation, integer state and SQL-generated `rowversion`. Nullable `nvarchar(450)` key, `nvarchar(256)` version, GUID claim and `datetimeoffset` cleanup timestamp. |
| Media FK and indexes | `FK_TripSafety_TripMemoryMedia_TripSafety_TripMemories_MemoryId` to MemoryId with NO ACTION. `IX_TripSafety_TripMemoryMedia_MemoryId` unique; `IX_TripSafety_TripMemoryMedia_ObjectKey` unique where `ObjectKey IS NOT NULL`; `IX_TripSafety_TripMemoryMedia_State_CreatedAtUtc` nonunique. |
| Media checks | `CK_TripSafety_TripMemoryMedia_Size` requires length greater than zero. `CK_TripSafety_TripMemoryMedia_State` gates key/version, cleanup claim and completion timestamp by Pending, Attached, CleanupClaimed and Cleaned state. |

The model also differs from the snapshot outside MS07:

- M3 SafeCheckIn has two tables and the live-session alternate key. Membership revocation has its outbox table. Hazards adds Description, Latitude, Longitude, PhotoAssetReference and its coordinate check.
- Identity adds password-reset credentials and user-role assignments. M2 adds trip-copy attributions.
- M6 adds voucher request decision and owner snapshot columns, issuance lifecycle and rowversion columns, inventory metadata and rowversion columns, coin rule dates and actor columns, and ledger actor/reason fields and unique indexes. Issuance InventoryId and the existing status check and unique inventory index also differ.

Proposed sequence after approval: reconcile deployed history and owner-specific drift; review an explicit MS07-only operation set and snapshot transition; create the principal stop alternate key ahead of dependent memory and media objects; verify stop delete and memory write behavior in isolated SQL; deploy schema and coordinated M2/M3 code with runtime still disabled; enable only after separate operational and provider approvals. A full-model scaffold would include unrelated operations and is unsuitable for an MS07-only submission without coordinator reconciliation.

## Proposed SQL profile

DECISION_REQUIRED: approve a new, test-only `WAYHUNTER_M3_MS07_TEST_SQL_CONNECTION` profile. No existing SafeCheckIn or shared test database may be reused.

- Require exact allow-listed local server `(localdb)\MSSQLLocalDB`, Integrated Security, and exact template catalog `Wayhunter_M3_MS07_IntegrationTest`. Validate before any connection. Reject credentials, attached database files, failover/alternate destinations and unknown connection options. Reconstruct the effective test connection from allow-listed settings and use an explicit timeout.
- Derive one fresh catalog per fixture named `Wayhunter_M3_MS07_IntegrationTest_<32_hex_guid>`. Fail if pre-existing. Use independent connections to that catalog for concurrency tests. Limit DDL to the approved isolated-fixture schema in that catalog. Never use shared migrations or a production/shared connection.
- Track whether this run successfully created the exact generated catalog. Teardown only that catalog after disposing fixture connections, including after failed assertions. Revalidate exact server and the registered generated catalog before teardown. Never drop the template, wildcard-matching databases, or any unowned catalog. Use test-only fake media. SQL tests remain opt-in and must skip until the profile is approved and supplied.

## Required SQL scenarios and assertions

| Case | Required evidence |
|---|---|
| 1. Active reference | Authorized delete of its trip stop gets 409; stop and active memory link survive. |
| 2. Tombstone reference | Same after soft deletion; tombstone retains its stop FK. |
| 3. Unreferenced stop | Existing owner, draft and ETag rules still apply. Valid delete commits and atomically normalizes order. |
| 4. Cross-trip stop | Service rejects it and the composite FK rejects an inconsistent raw insert. No partial memory exists. |
| 5. Memory wins race | Hold memory transaction before commit after validation and insert. Start delete from another connection, then release memory commit. Delete returns safe 409. The original stop and memory survive. |
| 6. Stop delete wins race | Hold delete transaction before commit after the delete. Start memory create on another connection, then release delete commit. Create revalidates, fails safely, and leaves no dangling memory or operation. |
| 7. Delete rollback | Compare parent rowversion, stop order, surviving stops, and links before and after a restricted delete. No partial committed update. |
| 8. Safe FK translation | Exercise real constraint enforcement including races. Return a stable 409 without SQL text or details for the relevant relationship conflict. Do not classify unrelated integrity failures as this conflict. Recheck in a clean context if classification needs confirming. |
| 9. Media retry | Parallel or repeated reservation with matching operation and payload returns the same record. A changed request cannot reuse the identity. |
| 10. Memory duplicate | Parallel independent connections with the same scope and create operation commit exactly one record. Stable retry result and payload comparison survive conflict/retry. |
| 11. Cleanup fencing | Race attach/delete/cleanup. A lost CAS never calls the fake provider. The winning claim freezes the exact version and replay stays idempotent. |
| 12. Owner isolation | Another owner's asset or a live/shared reference is denied. Assert zero provider delete calls and unchanged committed metadata. |

Use barriers/interceptors and bounded waits to establish both race orders. Assert from a separate read context after completion. The proposed transaction boundary preserves the existing M2 owner, ETag, draft and row ordering semantics. The final lock order and retry strategy need joint M2/M3 and coordinator validation. **Status: SQL scenarios NOT EXECUTED.**
