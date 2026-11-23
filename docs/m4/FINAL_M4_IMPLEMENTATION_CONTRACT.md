# M4 Hunter final implementation contract

**Status:** Proposed implementation decisions adopted for `task/final-m4-write-completion` development only  
**Date:** 2026-11-23  
**Production/Official SQL authorization:** Not granted. The documented M4-MS07 migration and live-SQL hold remains in force.

This contract chooses safe implementation defaults so development can proceed. It records branch-local engineering decisions, not approval by the M4, M1, M6, media, DB, or Team Leader owners. No Official database is to be modified.

## 1. Draft lifecycle

- `POST /api/mobile/hunter/submissions` creates a server-ID, server-time `Draft` owned by the active verified account from the trusted principal. The only snapshot name is M1 `UserAccount.DisplayName`; clients cannot set `UserId`, `HunterId`, `UserNameSnapshot`, ID, status, or timestamps.
- Required request fields: `placeName` (trimmed, 1–200 chars), `claimedDistrict` (trimmed, 1–100 chars), finite latitude in [-90, 90], finite longitude in [-180, 180]. Optional description is trimmed and at most 2,000 chars. Optional accuracy must be finite and >0. Optional location capture time must be UTC and no more than five minutes in the future relative to server time. District is a hunter claim and is not represented as independently verified.
- `PUT /api/mobile/hunter/submissions/{id}` replaces the same editable fields only while the authenticated owner still has a Draft. It cannot change identity, snapshots, status, timestamps, or evidence. The SQL update uses the current state predicate and row lock/transaction; a stale or non-Draft write returns conflict.
- `DELETE` is permitted only for the owner's Draft with no evidence and no capture-session rows. This preserves capture-session audit rows under their no-action FK. If evidence or any session exists, return conflict; do not orphan or automatically purge media. Once submitted, a submission is retained for review/audit.
- Reads conceal foreign and missing submissions consistently. Review and capture actions require an active verified account as well as trusted identity.

## 2. Evidence contract and trusted provider boundary

- The existing types are retained: exactly one `Guided360Video`, followed by exactly one `LivePhoto`. The existing domain rule requires the video upload to precede photo capture. A second item of either type is rejected.
- Each evidence item must bind to the same submission, its authenticated owner, and one unexpired, unconsumed, unrevoked capture session issued for that owner/submission. A session can bind to at most one evidence row; an opaque provider asset can be consumed once globally. A successful bind atomically inserts the evidence, records the session binding, consumes the session, and stores the idempotency result.
- Provider verification must return a server-verifiable private asset receipt with owner, media type, capture operation, and metadata facts. Client URLs, IDs, EXIF, timestamps, GPS or upload claims alone are untrusted. Provider capture time is required and cannot be after server receipt time. Private reference only; credentials and public URLs are never persisted or logged.
- **Provider availability:** no Hunter provider verifier exists in this repository. The API will use an explicit disabled implementation returning unavailable, and evidence bind/submission/approval requiring evidence will fail closed. This does not claim upload or verification is implemented.
- Provider upload/verification occurs outside SQL transactions. A successful upload followed by failed bind leaves an orphan; no automatic delete is attempted without a provider cleanup API. Retain the opaque receipt for operator reconciliation only if policy and provider contract allow it. No automatic media retention purge is introduced in this branch.

## 3. Submission, retries, and concurrency

- `POST /api/mobile/hunter/submissions/{id}/submit` permits only the authenticated owner's Draft with the exact verified evidence set (one video and one photo) and valid video-before-photo timing.
- Submission status, `SubmittedAtUtc`, and unused-session revocation update atomically. Concurrent submitters yield one transition. A repeated request after a committed transition returns the existing PendingReview projection only when the stored state and verified evidence still match; all other stale-state attempts conflict.
- Draft creation requires a bounded `Idempotency-Key` (1–128 printable ASCII chars). Scope is `(actor UserId, operation, key)`; persist a SHA-256 key hash, request digest and canonical resource ID. Reuse with a different digest conflicts. Marker and new Draft commit in one SQL transaction. PUT is a replacement operation guarded by a submission row lock; DELETE is allowed only before evidence/session creation, so its repeated final state is safe. Evidence replay uses unique session and asset bindings; an identical bind returns the committed evidence ID, while mismatched reuse conflicts. Submit has no body and returns the existing PendingReview projection after a completed submission; one conditional status transition wins concurrent calls. Failed SQL transactions never return success.
- Use SQL Server serializable transactions, `UPDLOCK/HOLDLOCK` on submission/session rows, conditional expected-status updates and unique indexes. No submission rowversion is added; locking and expected-state predicates provide the current concurrency boundary. Do not claim exactly-once external provider operations; provider calls are at-least-once and require receipt idempotency or reconciliation.

## 4. Admin review and audit

- Admin actions require the existing centralized administrator policy plus a canonical reviewer ID derived from the authenticated principal. Never accept reviewer IDs from request bodies.
- `PendingReview → Approved` requires both verified evidence rows, records reviewer/reason/time, revokes unused sessions, and commits atomically. A nonempty reason is accepted and capped at 2,000 chars; approval reason may be omitted.
- `PendingReview → Rejected` requires a nonblank reason (trimmed, 1–2,000 chars), persists the decision, revokes unused sessions, and commits atomically. Decisions are append-only. Replays conflict; re-review, Held, and resubmission are excluded.
- Existing review persistence has audit storage but did not verify evidence or implement Rejected. Branch implementation adds those checks and transition. SQL effects remain branch-local until separately deployed/accepted.

## 5. M6 reward handoff

- M4 never credits Coins. Existing M6 grants +200 once for an approved Hunter using submission ID as stable source identity and persists through its idempotent grant/outbox infrastructure.
- M4 approval becomes a committed source record; M6's existing SQL reconciliation is the retryable handoff. A ledger uniqueness conflict means already credited; M4 does not report credit. Outbox worker/writer remains behind its existing disabled-by-default flags. No new M4 reward event table is added unless future verification shows the existing M6 source/outbox cannot meet this contract.

## 6. SQL and migration safety

- Branch-local migrations are `20261123110145_M4HunterWriteWorkflow` and `20261123113519_M4EnforcePositiveGpsAccuracy`. They add provider verification/binding fields, the idempotency table, composite submission-owner and session-submission foreign keys, evidence/session/provider-asset uniqueness, verified evidence-per-type uniqueness, and a positive optional GPS-accuracy check. Existing `Hunter_ReviewDecisions` remains the review audit store. No rowversion column is introduced; SQL locking and expected-state predicates provide concurrency protection.
- SQL migration and integration verification use one newly created, uniquely named `Wayhunter_Test_M4_<32-hex-guid>` LocalDB catalog. Opt-in tests must be explicitly configured to this catalog and must reject any other server or catalog name. The disposable Draft HTTP test does not create another catalog. Never target `Wayhunter_Official`, a shared DB, or any catalog not created for this run. Keep the verified test catalog available for inspection; remove it only after confirming its exact name and ownership.
- Keep the historic M4-MS07 hold. This branch-local migration/test is not DB Coordinator approval, schema freeze, production verification, or authorization to update any shared/Official database.

## 7. Route and error behavior

- New endpoints use existing `/api/mobile/hunter/submissions` and `/api/admin/hunter/submissions` families. Mobile authenticated actions use trusted active-account identity; Admin actions use the centralized administrator policy. Missing/foreign resources are concealed as 404; invalid input is 400; stale state, duplicate asset/session, and idempotency digest mismatch are 409; unavailable SQL/media is 503. No success response is returned until SQL commit succeeds.
- Existing GET list/detail, capture-session start, approved-place listing, and Admin pending queue routes are reused. No duplicate route is added.

## 8. Implementation sequence / remaining production gates

1. Preserve existing identity and M3 safety code; add application validators/services and unit tests.
2. Add SQL repositories and EF model changes; generate an additive migration and model-diff checks.
3. Add authenticated HTTP-to-SQL tests using only the owned disposable LocalDB database; exercise owner isolation, authorization, rollback, races, replay and idempotency.
4. Keep Hunter evidence binding, submission and approval unavailable until a real provider verifier is configured and passes ownership/replay tests. This is a technical gate, not a reason to trust client media.
5. Keep M6 reward delivery disabled unless its existing runtime flags are explicitly configured; verify durable reconciliation/ledger uniqueness before claiming delivery.
6. Before production: obtain required owner reviews, verify migration against the deployment schema, authorize a deployment process, and explicitly lift the M4-MS07 hold. This development contract does none of those.
