# M3-MS07 Trip Memories and private media readiness

## Scope and evidence

SOURCE: Second Brain `M3_FULLSTACK_MILESTONES_TASKS.md`, commit `42a5cf38001c1afbb0c69e75718c0aa615bdc39d`. Backend scope is M3-MS07-T01, T02, T04, T05, and T06. M3-MS07-T03 belongs to Flutter. The canonical milestone lists M3-MS02 and M1-MS05 as dependencies. This branch starts at develop `38f7c275f95c303dd04298811ed13995895a1158`, which contains the merged SafeCheckIn work. M2 supplies trip and stop data.

CODE_VERIFIED: M1's shared authentication pipeline and trusted claim contract supply the user ID. The M2 trip repository supplies authoritative trip and stop context. Existing M3 SafeCheckIn production wiring is outside this change. The inspected backend contains no approved memory-specific private signed upload, immutable asset verification, or conditional delete adapter. Hunter capture authorization does not upload evidence. The readiness patch adds model mappings and contracts without activating a durable store or a cloud client.

## T01 proposed shared SQL objects

| Object | Fields and keys |
|---|---|
| `TripSafety_TripMemories` | Application-assigned GUID `MemoryId` primary key; GUID `TripPlanId`; nullable GUID `TripStopId`; required `OwnerUserId` as `nvarchar(256)`; GUID `CreateOperationId`; required `Caption` as `nvarchar(1000)`; integer `Visibility`, default Private (0), Public (1); `CreatedAtUtc`, `UpdatedAtUtc` and nullable `DeletedAtUtc` as `datetimeoffset`; SQL `rowversion`. |
| `TripSafety_TripMemoryMedia` | Application-assigned GUID `OperationId` primary key; unique GUID `MemoryId`; required MIME `nvarchar(64)`; positive `ContentLength` as `bigint`; `CreatedAtUtc` as `datetimeoffset`; integer state; nullable private `ObjectKey` and immutable `ObjectVersion` as `nvarchar(450)` and `nvarchar(256)`; nullable GUID `CleanupClaimId`; nullable `CleanedAtUtc` as `datetimeoffset`; SQL `rowversion`. Media inherits trip and owner from its memory. |

All proposed foreign keys use NO ACTION. Memory references `Trips.Id` and `Users.UserId`. Optional stop binding uses `(TripStopId, TripPlanId)` against a new shared-model alternate key `TripStops(Id, TripPlanId)`. Media references exactly one memory. The unique memory key `(TripPlanId, OwnerUserId, CreateOperationId)` retains idempotency through soft deletion. A filtered trip/owner/creation-time index supports live reads. The media row has unique memory and non-null object key indexes, and a `(State, CreatedAtUtc)` lookup for recovery. State and timestamp checks protect the tombstones and claim shape.

DECISION_REQUIRED: DB Coordinator approval of both tables, keys, stop alternate key, footprint from existing model-to-snapshot drift, isolated SQL test schema, and additive migration. A linked memory would currently restrict physical removal of its stop under the proposed NO ACTION FK, including for a tombstone. Coordinate with M2 on clearing the optional link, retaining references, or another approved deletion policy before deploying. No shared migration, initial schema or snapshot change is part of this work.

A durable implementation must recheck trip ownership and stop binding atomically with insert, recover unique-key collisions by reauthorizing and comparing the normalized payload, and preserve the tombstone. A duplicate with changed caption, visibility or stop conflicts. A reused deleted operation conflicts. Reserve, attach, soft delete and cleanup must enforce owner/state at their transactional write boundary, with rowversion or equivalent CAS. Verify both race orders on isolated SQL. EF model and fake-store tests do not prove these guarantees in SQL.

## T02 and T04 API and media contract

All routes are under `/api/mobile/trips/{tripPlanId}/memories` and require trusted M1 authentication. The user ID comes from the authenticated principal. Only the authoritative trip owner can create, list, view, upload or delete. Get and list exclude tombstoned records. A foreign trip, foreign memory, deleted memory or nonowner read fails closed. Public is stored as a visibility choice but has the same owner-only read boundary while public audience and eligible member policy are DECISION_REQUIRED. No discovery endpoint is introduced.

| Method and suffix | Request and result |
|---|---|
| POST collection | `operationId`, optional `tripStopId`, optional `caption`, optional `visibility` (numeric Private 0 by default, Public 1). Returns 201 with Location on create or 200 on an exact retry. |
| GET collection | Owned live memory views. Pagination/limits are DECISION_REQUIRED before durable activation. |
| GET `/{memoryId}` | One authorized live memory view. |
| DELETE `/{memoryId}` | Soft delete, 204 for an owned record including an idempotent repeat. |
| POST `/{memoryId}/media/uploads` | `operationId`, `contentType`, and `contentLength`. Returns the same operation identity, a private HTTPS upload grant and expiry when an approved provider is available. |
| POST `/{memoryId}/media/uploads/{operationId}/complete` | No client asset reference. Returns verified media metadata or fails closed. |

A view contains the memory, trip and optional stop IDs, caption, visibility, timestamps, and optional media type and byte length. It exposes no owner-supplied identity, storage object ID, private download URL or provider credentials. The signed upload URL is returned only to the authenticated owner from the grant endpoint with no-store caching. Invalid trusted identity returns 401, invalid IDs/fields 400, missing or inaccessible scoped resources 404, conflicting operations 409, and unavailable dependencies 503. Current normal production wiring has unavailable store and provider adapters.

Provisional implementation ceilings are 5 MiB for JPEG/PNG and 16 MiB for MP4. The service rejects missing/unsupported MIME, empty and oversized input, mismatched size and invalid signatures. Full decoding of content and private immutable object attestation are required from the future provider. The local byte checks are format checks, not a complete media decoder. The provider must sign private uploads with type and length constraints, allocate one operation-scoped key, limit grants to the original reservation window, seal uploaded versions, and verify exact operation, owner, memory, MIME, size, and privacy before serving bytes for verification. Raw client object IDs are never accepted as proof. Failed issuance or verification leaves the same pending operation available for safe retry. Successful attach uses verified evidence and an atomic state transition.

DECISION_REQUIRED: approved upload transport/provider, private read delivery policy, MIME/size/expiry limits, trusted decoding safety, private object versioning, allowed upload host, retry window, and required provider cancellation and reconciliation behavior. This patch does not return live signed URLs or register cleanup.

## T05 orphan cleanup and deleted posts

Delete must commit the tombstone before cleanup. The store selects only a deleted-memory or expired pending reservation after grants can no longer write. The provider must prove ownership and return a sealed immutable version before a CAS claim can be persisted. The claim transaction must exclude live or shared references and permanently fence new attachment for that object. Failure to prove identity or win the CAS means no provider delete. An existing durable claim can resume with the frozen evidence after a crash, including when the provider already deleted the same version. A provider error leaves the claim recoverable and does not change the memory tombstone. Cleanup finalization must be conditional on the claim and retain sanitized identifiers and timestamps for audit. A missing/unverifiable upload must be held for approved reconciliation. Do not infer ownership or absence from a lost response.

DECISION_REQUIRED: durable SQL claim and expiry implementation, tested conditional delete/version fencing, orphan retention and deleted-post retention, provider reconciliation, redrive authority, audit and removal policy. The production cleanup worker and any public media delivery remain unregistered.

## T06 verification and handoff

The new tests cover service authorization, hidden private and proposed public memories, trip/stop binding, concurrent idempotency against a test double, failed upload retry, media size/type/signature and provider evidence, delete/cleanup recovery, HTTP fail-closed behavior, and shared-model metadata. An approved dedicated MS07 SQL test profile is absent. MS07 SQL concurrency/integrity tests were NOT EXECUTED.

Restore and build passed with 0 warnings and 0 errors. The full run passed 3,034 unit tests and 147 integration tests with 0 failures and 70 opt-in SQL skips. The integration count includes 15 existing SafeCheckIn LocalDB cases. The focused M3-MS07 run passed 22 unit/metadata tests and 3 HTTP tests with 0 failures and 0 skips.
