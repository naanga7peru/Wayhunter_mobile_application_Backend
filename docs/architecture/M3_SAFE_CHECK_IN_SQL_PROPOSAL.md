# M3 safe check-in SQL proposal for PR #259

**Status: conditional DB Coordinator review. Final SQL approval is DECISION_REQUIRED.** Team Leader approval covers M3 implementation. This document requests approval for an additive shared schema and its deployment. It does not authorize a migration or rollout.

## Verified baseline

- `WayhunterSharedDbContext` maps `Trips`, `Users`, `TripSafety_LiveTripSessions`, `TripSafety_TripMembers` and GPS consent. The migration, snapshot and SQL script for `InitialSharedSchema` contain no safe check-in table. The `(SessionId, UserId)` key on GPS consent belongs to a different feature.
- `Trips.Id` and `TripSafety_LiveTripSessions.SessionId` are SQL `uniqueidentifier`. Sessions point to `Trips.Id` and the filtered open-plan index permits only one Active or Paused session per trip. Historical finished sessions remain.
- `Users.UserId` is a stable M1 string key backed by `nvarchar(256)`. `Trips.OwnerUserId` and `TripSafety_TripMembers.MemberUserId` reference it. The filtered active membership index is on `(TripPlanId, MemberUserId)`. M1 resolves trusted claims to a canonical non-empty GUID string. The SQL FK must keep the Users key type and collation.
- `M3NotificationOutboxDispatcher` uses a revocation-specific row and sender, with conditional claims, lease tokens, retry schedules and a stable idempotency header. The working EF model maps `TripSafety_MemberRevocationOutbox`. The baseline migration, snapshot and script omit that table. A revocation row and worker are unsuitable as a safe-check-in event contract.

## Proposed additive objects

**DECISION_REQUIRED** Approve the names, sizes, keys, delete behavior and retention below. Every column is NOT NULL unless explicitly marked nullable. GUID PKs are assigned by the application. A NULL NextAttemptAtUtc means immediately due for a pending event. Use `NO ACTION` for proposed FKs, with retention and identity-deletion handling agreed with M1. Use server-generated UTC timestamps and IDs.

| Object | Column | SQL type and constraint |
|---|---|---|
| `TripSafety_SafeCheckIns` | `CheckInId` | `uniqueidentifier` primary key |
| | `TripPlanId` | `uniqueidentifier`, FK to `Trips.Id` |
| | `SessionId` | `uniqueidentifier`, composite FK with TripPlanId to the authoritative session |
| | `UserId` | `nvarchar(256)`, FK to `Users.UserId` |
| | `CheckedInAtUtc` | `datetimeoffset(7)` |
| `TripSafety_SafeCheckInOutbox` | `Id` | `uniqueidentifier` primary key and stable delivery idempotency key |
| | `CheckInId` | `uniqueidentifier`, unique FK to `TripSafety_SafeCheckIns.CheckInId` |
| | `RecipientUserId` | `nvarchar(256)`, FK to `Users.UserId` |
| | `CreatedAtUtc` | `datetimeoffset(7)` |
| | `DeliveryAttempts` | `int`, default 0, nonnegative |
| | `IsDelivered` | `bit`, default 0 |
| | `DeliveredAtUtc`, `NextAttemptAtUtc`, `LeaseUntilUtc`, `DeadLetteredAtUtc` | nullable `datetimeoffset(7)` |
| | `LeaseToken` | nullable `uniqueidentifier` |
| | `LastError` | nullable `nvarchar(256)`, sanitized error classification only |

The primary key constraints are `PK_TripSafety_SafeCheckIns` and `PK_TripSafety_SafeCheckInOutbox`. The proposed alternate key is `AK_TripSafety_LiveTripSessions_SessionId_TripPlanId` on `(SessionId, TripPlanId)`. It is added through the shared check-in FK mapping. The existing session PK and filtered open-trip index remain. The composite FK rejects a check-in that pairs a valid session with the wrong trip. In EF the alternate-key properties are immutable on tracked sessions. Current session transitions change status and finish time only.

| Table | Index | Ordered columns | Unique | Filter |
|---|---|---|---|---|
| Check-ins | `UX_TripSafety_SafeCheckIns_SessionId_UserId` | SessionId, UserId | Yes | None |
| Check-ins | `IX_TripSafety_SafeCheckIns_SessionId_TripPlanId` | SessionId, TripPlanId | No | None |
| Check-ins | `IX_TripSafety_SafeCheckIns_TripPlanId` | TripPlanId | No | None |
| Check-ins | `IX_TripSafety_SafeCheckIns_UserId` | UserId | No | None |
| Outbox | `UX_TripSafety_SafeCheckInOutbox_CheckInId` | CheckInId | Yes | None |
| Outbox | `IX_TripSafety_SafeCheckInOutbox_RecipientUserId` | RecipientUserId | No | None |
| Outbox | `IX_TripSafety_SafeCheckInOutbox_PendingDue` | NextAttemptAtUtc, CreatedAtUtc, Id | No | `[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL` |
| Outbox | `IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry` | LeaseUntilUtc, Id | No | Pending filter above and `[LeaseUntilUtc] IS NOT NULL` |

The check-in FKs are `FK_TripSafety_SafeCheckIns_Trips_TripPlanId`, `FK_TripSafety_SafeCheckIns_Users_UserId`, and `FK_TripSafety_SafeCheckIns_LiveTripSessions_SessionId_TripPlanId`. The outbox FKs are `FK_TripSafety_SafeCheckInOutbox_SafeCheckIns_CheckInId` and `FK_TripSafety_SafeCheckInOutbox_Users_RecipientUserId`, both required and `NO ACTION`. The unique CheckInId index allows at most one event per check-in. The application transaction enforces creation of the companion event on every successful first check-in.

The four `CK_TripSafety_SafeCheckInOutbox_` constraints are `Attempts` (nonnegative), `DeliveryState` (delivered iff a delivery timestamp exists, with no dead letter on delivery), `LeasePair` (token and expiry both set or both absent), and `TerminalState` (delivered and dead-lettered rows carry neither a lease nor a next retry). The dedicated table defines the event type. Read the minimal event data through its check-in FK. There are no GPS fields, free-form payloads or location-data FKs.

## Write transaction and retry

**DECISION_REQUIRED** Approve SQL Server serializable execution with indexed reads and the lock order below, or an equivalent locking design demonstrated against existing writers.

1. Resolve the current authenticated M1 session at the API. Take the acting UserId from the trusted principal only. Validate the trip, exact session and current authorization inside the SQL transaction. Read the trip first, then session, then indexed active membership for a non-owner, then the check-in key. Require the trip owner or an active member, a non-cancelled trip and an exact trip/session match. Require Active or Paused for a new insert. Apply the proposed post-Finish retry policy below to an existing row. Resolve the notification recipient from the authoritative trip owner in this transaction.
2. Hold serializable shared locks on the trip and session rows and shared or key-range locks on the indexed active-membership and check-in lookups through both inserts. Do not use snapshot reads or NOLOCK for these decisions. Existing writers must take conflicting exclusive locks on the affected trip, session or member key. Verify the actual SQL Server query plans and locks, including filtered-index membership revocation and missing-key ranges. A shared-lock conversion deadlock is a transaction retry, never permission to bypass a read. Validate the lock behavior against concurrent Finish, trip cancellation and member revocation. A competing transition that commits first must be observed by the check-in. If the check-in commits first, it retains its historical record.
3. Return an existing authorized check-in with its original ID and timestamp and no new event. On first creation, save the check-in and one event using one shared context and transaction. Use the provider execution strategy around the entire operation with a clean transaction on retry. On deadlock or ambiguous commit, re-read through the same authorization and idempotency path. For a uniqueness collision, recover only after verifying the same logical key and its existing event.
4. **DECISION_REQUIRED** Proposed retry after Finish: if the actor is still authorized, the trip is not cancelled, the session belongs to the trip and an existing check-in is found, return the original ID and timestamp with Created=false. A first check-in after Finish returns conflict. Perform authorization and cancellation checks before the existing-record lookup. Proposed owner self check-ins enqueue the same single owner event. Confirm both choices before wiring runtime behavior.

## Notification delivery

**DECISION_REQUIRED** Approve channel, offline behavior, attempt limits, lease duration, privacy, retention, and controlled re-drive authority. Revalidate that the captured recipient still matches the authorized trip owner before delivery. Define a terminal outcome if authorization has changed. Resolve that outcome without redirecting the event to another recipient. Use a dedicated sender contract independent of GPS consent, SignalR location groups, and GPS coordinates.

Claim events with one conditional update by Id. Require a pending row, attempts below the configured maximum, due time absent or at/before trusted UTC now, and lease expiry absent or at/before now. Record a fresh token, expiry and incremented attempt in the same write. Treat an affected-row count of zero as a lost claim. Acknowledge or reschedule only under the matching unexpired token, clearing both lease fields together. Acknowledge atomically sets IsDelivered and DeliveredAtUtc and clears NextAttemptAtUtc. Keep the send timeout inside the lease. Use bounded backoff and dead-letter exhausted events, including lease-expiry recovery after a worker exits. Send the event Id as a stable `Idempotency-Key` and require durable consumer-side deduplication. An external send followed by a lost acknowledgement can be retried, so leasing alone does not guarantee exactly-once delivery. Preserve the event Id during authorized replay. Proposed recovery marks a final-attempt expired lease as dead-lettered and clears its lease and retry fields. Re-drive is a privileged audited update of a dead-lettered row under a conditional predicate, with cleared terminal and lease fields, reset attempt budget, and the same Id. Recipient mismatch is terminal and requires an audited decision to re-drive. Revalidate ownership and account eligibility immediately before every send, and retry transient authorization-store failures without delivery.

## Reminder ordering

The approved guard queries the exact trip and session from persisted state. Missing, mismatched and finished sessions suppress dispatch. A store failure prevents sending. An older finished session does not suppress a newer one. The guard is a read followed by an external action, so Finish can still commit between the read and the send.

**DECISION_REQUIRED** Before wiring production dispatch, approve the ordering guarantee and cancellation boundary for in-flight provider calls. A proposed durable reminder claim or cancellation must serialize against Finish for the same session, with a stable event Id and consumer deduplication. A local lock or a second read does not close the cross-instance race.

## Ownership and verification

**DECISION_REQUIRED** Confirm the DB Coordinator owns the additive `WayhunterSharedDbContext` migration, script review and rollout. Coordinate keys and retention with M1/M2 without changing their public contracts. Reconcile the revocation mapping drift explicitly so a generated migration does not silently bundle pending unrelated changes. Keep `InitialSharedSchema` and its script immutable. Review expected schema diffs, rollback/forward recovery, permissions, and upgraded databases before applying anything.

Deployment sequence, subject to final approval: inventory model-to-snapshot drift, isolate the proposed session alternate key and two tables from other pending work, generate and review the additive migration and idempotent script, verify against a dedicated SQL baseline and upgrade, deploy schema first under the coordinator, and deploy the application with runtime flags still disabled. Confirm schema readiness and verification evidence before a separate enablement change. The relational dependency order is session alternate key, check-in table and indexes, then dependent outbox table and indexes. Rollback must disable writers/workers first and preserve any committed business or delivery history under an approved recovery plan.

After approval, implement the check-in store and dedicated outbox sender in Infrastructure. Keep authentication and request mapping in API and orchestration in Application. Keep runtime unavailable until the reviewed additive migration and delivery contract are verified. Use a dedicated SQL test database with the approved schema. Required cases include:

- Schema key/FK enforcement, upgrades from the baseline, and cross-trip or invalid-user rows rejected.
- Owner and active-member success, outsider and removed member denial, exact session binding, cancellation and Finish, and the approved retry behavior.
- Parallel independent connections with one check-in and one event, stable retry after restart or ambiguous commit, and rollback if enqueue fails.
- Coordinated races with Finish, cancellation and revocation, exercising both commit orders.
- Parallel outbox claims, stale lease fencing, expired lease recovery, retry and dead-letter behavior, and deduplication after delivery before acknowledgement.
- Owner eligibility revalidation, offline behavior, no GPS fields or dependencies, and production-disabled configuration tests.
