# M4/M5 contract gaps and implementation boundary

## Implemented read contracts in this branch

- `GET /api/mobile/hunter/submissions` returns the authenticated owner's safe submission status projection, newest first. Owner identity is resolved from the authenticated principal and active account.
- `GET /api/mobile/hunter/submissions/{submissionId}` remains owner scoped and conceals missing and foreign records as 404.
- `POST /api/mobile/hunter/submissions/{submissionId}/capture-sessions` remains owner and Draft scoped, with server timestamps, SQL ownership, expiration, revocation and atomic single-use consumption.
- `GET /api/mobile/hunter/places` returns only approved submissions that have a persisted Admin approval decision, with public place name, district, coordinates and review timestamp. It requires an active authenticated account.
- `GET /api/admin/hunter/submissions` returns the pending-review queue under the configured trusted Admin policy. It is read-only; no review decision is recorded by this route.
- `GET /api/mobile/games/me` returns only the authenticated creator's records, including expired/archived history. It does not expose creator IDs or proof data.
- Existing M5 draft CRUD remains restricted to the approved Development-only feature gate; Games SQL persistence now serves the My Games query but does not activate draft writes in production.

## Exact remaining M4 decisions

1. **Draft creation contract:** confirm whether creation begins in `Draft`, which place and GPS fields are required, and the maximum lengths/ranges. The minimal request proposal is `placeName`, optional `description`, `claimedDistrict`, latitude/longitude, optional accuracy and capture time. User ID, Hunter ID, username snapshot, status and server timestamps must be derived or set server side.
2. **Evidence requirements:** confirm whether each submission needs one guided 360 video and one live photo, and whether other combinations are allowed. Existing enum values alone do not establish cardinality or completion requirements.
3. **Media verification authority:** select the private media provider and receipt format. The provider must attest immutable asset identity, private access, authenticated uploader, upload operation, media type, and that the asset is not bound elsewhere. Until then, evidence intake remains unmapped and fails closed.
4. **Capture purpose and replay:** approve binding a one-time capture session to `(submission, evidence type, owner)`; require an atomic unique session-to-evidence relation and a client idempotency key bound to the normalized request. Decide key retention and same-key/different-payload behavior.
5. **Capture time:** approve accepted clock skew and maximum age. Store provider/server upload time separately from device capture time; never treat client EXIF time as server time.
6. **Provider/SQL failure recovery:** define orphan-media cleanup and retry semantics when provider verification succeeds but SQL commit fails. The backend must not report evidence as bound until its SQL transaction commits.
7. **Admin rejection/hold:** approve allowed transitions, mandatory reason rules, Held re-entry, and retry behavior. Current review service enables approval only; no reject/hold route was added.
8. **Reward delivery:** M6 must approve and verify the durable post-commit +200 Coins outbox/idempotency boundary before Hunter approval is exposed as a complete reward workflow.
9. **Admin approve API:** although the internal state transition is atomic, exposing it as HTTP remains gated on verified evidence and the M6 reward event boundary; the pending queue alone does not imply review completion.

## Minimal secure M4 write proposal for Team Lead approval

- Draft creation: `POST /api/mobile/hunter/submissions/drafts`, active session only. Request contains place/GPS claims only. Server derives owner and username snapshot, generates ID and `CreatedAtUtc`, validates canonical district/name/coordinates and bounded accuracy, and stores only `Draft`.
- Evidence bind: `POST /api/mobile/hunter/submissions/{id}/evidence`, active owner of a Draft, one-time capture session, opaque provider upload receipt, allowed evidence type, device capture time and optional captured coordinates. Provider verification is mandatory. Store a protected provider reference, immutable provider receipt digest, server upload time and the verified session link in one SQL transaction. Client cannot provide verification flags, storage URLs, owner IDs or server times.
- Replay: require `Idempotency-Key`; unique key scope `(owner, submission, operation)`, payload fingerprint, same-key/same-payload replay returns the committed result, same-key/different-payload returns 409. Session consumption and evidence binding are atomic and unique.
- Submission: do not expose until required evidence cardinality and verified binding are confirmed; transition Draft to PendingReview once, server-stamp submission, revoke unused sessions atomically. Admin queue lists only PendingReview. Approve is conditional; reject/hold are unavailable pending decision 7.

## Exact remaining M5 decisions and blockers

1. **Production draft route activation:** existing approved routes are explicitly Development-only. Production activation needs Team Lead/M5 acceptance of durable SQL create/update/delete, client revision (`ETag`/`If-Match`) or last-write-wins, create idempotency, draft-delete retention, and error semantics. SQL rowversion exists internally, but there is no approved client precondition/idempotency behavior.
2. **Admin Game review:** domain approval/rejection transitions exist, but SQL approval/reward persistence is not wired to the API and no M5 Admin authorization policy is registered. Do not expose a controller that relies on in-memory queue or claims alone.
3. **Proof provider:** choose private media provider and immutable capture receipt. Verify uploader, game/attempt/stage binding, media type, capture freshness, and globally unique consumption before SQL progression. Current fail-closed media provider cannot attest successful uploads.
4. **Replay and progression transaction:** approve stable proof operation key, retry semantics, attempt/stage uniqueness and transaction behavior for media claim, proof row and stage advancement. Existing domain rules and SQL repositories do not establish an end-to-end verified HTTP evidence contract.
5. **Winner/reward boundary:** exactly one Admin-verified winner excluding creator; durable atomic winner decision and unique +100 Coins event/outbox must be demonstrated before winner routes or rewards are enabled.
6. **Expiry worker:** retain approved records in My Games, hide expired records from playable/public views, and activate the repeatable SQL archive worker only after runtime/operational acceptance.

## Schema and test handling

No migration was added. The existing four migrations were applied only to a new `Wayhunter_Test_20261123_M4M5_A7C91E37` LocalDB catalog. The SQL integration fixtures now accept only a `Wayhunter_Test_` catalog prefix; no test connection points at `Wayhunter_Official`.
