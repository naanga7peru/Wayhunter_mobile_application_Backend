# M7-MS02-T05 — Durable Advertisement Image Reconciliation

Status: PROPOSED — DB COORDINATOR APPROVAL REQUIRED
Owner: M7
Target DbContext: WayhunterSharedDbContext
Migration status: NOT AUTHORIZED
Production activation: DISABLED

## 1. Existing Architecture

The authoritative SQL model uses WayhunterSharedDbContext.

Existing Advertisement persistence uses AdvertisementRepository
and the AdvertisementConfiguration EF mapping.

The image-only update currently performs a conditional
ExecuteUpdateAsync on:

- Advertisement.Id
- Expected Advertisement.ImageUrl
- Advertisement.Status != Archived

The existing InitialSharedSchema migration must be preserved.

## 2. Proposed Table

Provisional name: AdvertisementImageUploadOperations

Columns:

| Column | Proposed SQL Type | Rules |
|---|---|---|
| UploadOperationId | uniqueidentifier | PK, NOT NULL |
| AdvertisementId | uniqueidentifier | NOT NULL |
| AdministratorId | nvarchar(128) | NOT NULL; trusted M1 identity |
| Provider | nvarchar(32) | NOT NULL; Cloudinary V1 |
| ProviderAssetId | nvarchar(512) | NULL until known |
| PreviousImageUrl | nvarchar(2048) | NOT NULL |
| ReplacementImageUrl | nvarchar(2048) | NULL until known |
| State | int | NOT NULL; approved enum values |
| CreatedAtUtc | datetimeoffset | NOT NULL |
| UpdatedAtUtc | datetimeoffset | NOT NULL |
| CompletedAtUtc | datetimeoffset | NULL |
| RetryCount | int | NOT NULL, default 0 |
| NextRetryAtUtc | datetimeoffset | NULL |
| LastErrorCode | nvarchar(128) | NULL; sanitized codes only |
| ClaimToken | uniqueidentifier | NULL |
| LeaseExpiresAtUtc | datetimeoffset | NULL |
| Revision | bigint | NOT NULL; monotonic fence |
| RowVersion | rowversion | NOT NULL |
| CleanupDecisionCode | nvarchar(128) | NULL |
| CleanupDecisionAtUtc | datetimeoffset | NULL |

Final field lengths and SQL mapping require DB approval.

## 3. Keys and Constraints

- Primary key: UploadOperationId
- FK candidate: AdvertisementId -> Advertisements.Id
- Deletion policy: RESTRICT or an approved tombstone strategy
- No cascade deletion of unresolved upload operations
- RetryCount >= 0
- Revision >= 0
- Claim token and lease expiry must be consistent
- Valid State values require an explicit CHECK constraint
- Provider asset IDs must be backend-generated
- Provider namespace must be verified independently

A deleted Advertisement must not erase unresolved recovery evidence.

## 4. Proposed Indexes

1. Unique primary key on UploadOperationId
2. Index on AdvertisementId, State
3. Index on State, NextRetryAtUtc
4. Index on ClaimToken, LeaseExpiresAtUtc
5. Filtered index on ProviderAssetId WHERE NOT NULL

The DB Coordinator must decide whether provider asset uniqueness
is global or scoped to a provider/cloud account.

## 5. Durable Write Sequence

1. Authenticate the trusted M1 Administrator.
2. Validate Advertisement and upload request.
3. Generate UploadOperationId and immutable provider asset ID.
4. Persist the pending operation before calling Cloudinary.
5. Upload using the stable asset ID.
6. Persist the verified Cloudinary result.
7. Perform conditional Advertisement image update.
8. Persist committed/conflict/uncertain outcome.
9. Recover uncertain outcomes using authoritative SQL state.

If a provider timeout occurs, never blindly re-upload.

If a SQL response is lost, never assume rollback.

## 6. Atomic Recovery Fencing

Repository writes must use a single atomic conditional SQL
operation that verifies:

- UploadOperationId
- Expected revision
- Current claim token
- Unexpired lease according to authoritative SQL time
- Allowed operation state
- Immutable AdvertisementId and provider identity

A worker holding an expired or superseded claim must fail.

The in-memory C# fencing policy is defense in depth only.

## 7. Reference Verification

Before deleting any Cloudinary asset, verify:

- Backend-generated M7 asset namespace
- Exact provider asset identifier
- Current Advertisement image references
- Pending and uncertain upload operations
- Concurrent replacement and recovery activity
- Advertisement deletion/tombstone state
- Required recovery grace period
- Durable cleanup authorization/audit record

An ordinary unlocked SELECT is insufficient.

SQL verification and deletion authorization must prevent
new references to an asset being established while deletion
is pending. A durable deletion reservation or equivalent
transactional reference fence is required.

When verification cannot establish safety, preserve the asset.

## 8. Audit Proposal

Persist immutable audit events for:

- Operation creation
- Provider upload request/result/uncertainty
- SQL CAS success/conflict/uncertainty
- Worker claim acquisition/rejection
- Retry scheduling and exhaustion
- Reference verification decision
- Cleanup authorization and provider deletion result
- Manual-review escalation and resolution

Proposed separate table:
AdvertisementImageUploadAuditEvents

Suggested fields:
EventId, UploadOperationId, EventType, TimestampUtc,
ActorKind, ActorId, ErrorCode, CorrelationId,
PreviousState, NewState.

Never store provider credentials or raw exception secrets.

## 9. Recovery Rules

- Unknown upload outcome: verify provider state using the
  stable immutable asset identifier.
- Unknown SQL outcome: read authoritative references.
- Committed asset: preserve.
- Referenced by another active/pending operation: preserve.
- Missing Advertisement: do not assume asset is unreferenced.
- CAS conflict: initiate reference-safe reconciliation.
- Provider deletion timeout: verify before retry.
- Retry exhaustion: manual review, no blind deletion.

## 10. Runtime Guardrails

- Advertisements:ImageUpload:Enabled = false
- Advertisements:ImageUpload:LifecycleEnabled = false
- Recovery worker execution disabled by default
- Cloudinary real writes disabled pending authorization
- No migrations generated or applied
- Existing Administrator authorization preserved
- Fail closed if SQL, Cloudinary, audit or reference checks
  are unavailable.

## 11. DB Coordinator Decisions Required

1. Final table/entity ownership and schema name
2. Administrator FK/identity mapping to M1
3. Advertisement deletion and unresolved-operation FK policy
4. Provider asset uniqueness and reuse rules
5. Rowversion and Revision combination
6. Atomic claim and lease SQL implementation
7. Transaction boundary for Advertisement CAS and state update
8. Safe asset-reference reservation/deletion fence
9. Audit event storage and retention
10. Migration ordering after InitialSharedSchema
11. Isolated real-SQL verification procedure

## 12. Authorization Boundary

Approved now:
- Offline implementation and tests
- Persistence model and EF mapping proposal
- SQL-independent recovery logic
- Guarded runtime integration
- Feature branch and PR preparation

Not approved:
- Shared SQL migration generation/application
- Production Cloudinary writes
- Production feature activation
- Final PR merge without review

This document is a proposal, not an approved SQL schema.