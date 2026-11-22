# WAYHUNTER M4 Draft and Evidence — Final Decisions for Approval

**Branch:** `task/m4-hunterplaces-api-completion`
**Document status:** Decision proposal; not an implementation or authorization to expose routes.
**Scope:** Secure Hunter Draft lifecycle, capture sessions, trusted media binding, submission for Admin review, and proposed mobile/Admin HTTP contracts.

## 1. Executive decision

M4 has a Draft-capable submission entity, owner-checked capture-session handlers, single-use session stores, an owner-scoped status reader, and an atomic Draft-to-PendingReview transition. It does not yet have a concrete `IHunterSubmissionService`, an initial Draft creation workflow, a trusted media verification adapter, an evidence-to-capture-session relationship, or public M4 controllers.

The recommended design is to create a server-owned Draft before capture; issue short-lived sessions bound to that Draft and trusted Hunter; accept media only through a backend-controlled provider boundary; verify provider ownership and capture metadata; then, in one database transaction, bind each verified asset to its evidence row and consume its capture session. Submission for review remains a separate atomic status transition which stamps server time and revokes unused sessions.

Everything in the order-of-operations section beyond existing behavior is a **proposal** until approved by the Project Lead, M4 owner, media/provider owner, M1 owner where applicable, and DB Coordinator. No public endpoint should be mapped on the basis of this document alone.

## 2. Sources and evidence classification

### Existing source and approved rules

- `docs/second-brain/ACTIVE_RULES.md`: an approved Hunter place requires Admin approval; the active owner rules specify +200 Coins once for an approved Hunter submission. M4 must not mutate a wallet directly; reward delivery is an M6-owned boundary.
- `docs/architecture/M4_MS04_TRUSTED_IDENTITY_MAPPING_CONTRACT.md`: Team Leader-approved identity principle: M1 `UserAccount.UserId` and M4 `HunterId` are the same logical identity; the string GUID is strictly validated; identity comes from the server principal; client `HunterId` and fallback identities are forbidden. Final JWT claim/issuer and DB relationship decisions remain pending.
- `docs/architecture/M4_HUNTER_CAPTURE_INTEGRATION_READINESS.md`: current capture authorization sequence; account-to-Hunter production identity integration, persistence and submission eligibility details remain pending in this document.
- `WAYHUNTER_M4_HUNTERPLACES_IMPLEMENTATION_REPORT.md`: no M4 public routes; no trusted media adapter; no initial Draft/evidence binding sequence; SQL acceptance tests have not been run.
- Current source mappings inspected: `HunterSubmission`, `HunterEvidence`, `HunterCaptureSessionRow`, their EF configurations, `IHunterCaptureSessionStore`, and `IHunterSubmissionStatusTransitionStore`.

### Existing implementation facts (not additional business approvals)

- `HunterSubmission`: GUID ID, string `UserId`, required username snapshot, place name, district, coordinates, optional GPS accuracy/capture time, `Draft` default status, created time, nullable submitted time.
- Mapping constraints: place name max 200; description max 2,000; district max 100; latitude -90..90; longitude -180..180; GPS accuracy is nullable and must be nonnegative in SQL. User ID is FK-linked to `Users.UserId`; `(Id, UserId)` is an alternate key.
- `HunterEvidence`: GUID ID, required submission ID, evidence type (`Guided360Video=1`, `LivePhoto=2`), required private media reference max 2,048, required captured/uploaded timestamps, optional capture coordinates and duration. It has no `CaptureSessionId`, uploader ID, provider verification status, provider asset ID, or idempotency key.
- Evidence mapping has a submission FK and a non-unique `(HunterSubmissionId, EvidenceType)` index. It does not currently enforce one evidence per type or a unique session/evidence binding.
- `HunterCaptureSessionRow`: GUID session ID, GUID Hunter ID, submission ID, computed owner user ID, issue/expiry times, consumed/revoked flags. Existing store contract requires atomic ownership, expiry, and single-use checks. Current service uses server time and a ten-minute lifetime.
- Capture handlers require an existing owner-matching `Draft`. Session consumption is separate from evidence persistence today.
- Existing transition code supports an atomic Draft-to-PendingReview operation that stamps `SubmittedAtUtc` and revokes unused sessions. The service has no public controller integration.
- Existing `HunterSubmissionResponseDto` exposes submission ID, place name, district, status, created time, and submitted time.
- `IHunterSubmissionService` declares submit and owner-read methods only; there is no draft create/update/delete contract or implementation.

### Not approved or unknown

- The repository contains no approved M4 route/DTO/error contract. `docs/members/M4_SCOPE.md` is a placeholder and refers to a milestone brief in the Obsidian vault, not present in this repository. The configured vault MCP was not available in this session’s tool inventory, so no claim is made about external vault approvals.
- The M4 owner must still settle the lifecycle, evidence/capture binding, capture order, retry/replay, and transition semantics.
- The provider owner must define asset identity, upload method, ownership proof, metadata attestation, deletion/retention and failure behavior. A client-supplied media reference is not proof of ownership or verification.
- The DB Coordinator must approve schema relationships, unique constraints, transaction boundaries and isolated SQL acceptance.

## 3. Proposed domain workflow

The steps below are recommended, but those labeled **PROPOSED / APPROVAL REQUIRED** must not be treated as current behavior.

### Step A — Create the Draft

**Proposal:** `POST /api/mobile/hunter/submissions/drafts` creates one Draft owned by the authenticated account. The service derives `UserId` and `HunterId` from trusted identity and resolves any required `UserNameSnapshot` from authoritative account data; the client never supplies owner IDs or the username snapshot.

The request supplies place name, optional description, claimed district, coordinates, optional accuracy, and optional client capture time for the location. Existing validation and EF bounds apply. `CreatedAtUtc` and ID are server-generated. The response returns the submission ID and current Draft projection.

**Approval required:** decide whether all location/place fields are required at Draft creation or may be saved incrementally; confirm the authoritative source for `UserNameSnapshot`; approve update/delete semantics; decide whether a repeated create request is idempotent and for how long.

### Step B — Start a capture session

Existing behavior authorizes an authenticated account, checks ownership of an existing Draft, resolves the trusted Hunter identity, and issues a ten-minute session using backend time. The session store must bind the session to the Hunter and submission and enforce atomic checks at consumption.

**Proposal:** a session is purpose-bound to one evidence type and one submission. The server records an idempotency operation key or an equivalent persisted request identity. This purpose binding is not present in the current capture-session model; approval and schema work are required. Whether evidence order is video-before-photo or unrestricted is **not decided by current M4 contracts**.

### Step C — Upload media through a trusted provider boundary

**Proposal:** the API issues or accepts an upload only through a backend-controlled adapter. The adapter returns a server-observed opaque asset identifier/receipt; it does not expose provider credentials or accept a raw provider URL as trusted evidence. Client-provided `mediaId`, timestamps, GPS metadata, MIME type, or ownership claims are inputs only until independently verified.

The API/application layer must call a provider verification interface that confirms, at minimum, that the opaque asset exists, is private, belongs to the authenticated Hunter and intended upload operation, has an allowed media type, and is not already bound elsewhere. Provider-verified metadata must be distinguished from device-supplied capture metadata. If the provider cannot establish a required fact, fail closed and do not create verified `HunterEvidence`.

**Approval required:** provider identity contract, whether uploads are proxied or direct-to-provider, upload-token format and expiry, media types/size limits, ownership verification evidence, attested capture metadata fields, private retrieval/access, retention/deletion, orphan cleanup, and provider outage responses. No provider is activated by this proposal.

### Step D — Verify, bind, and consume atomically

**Proposal:** after provider verification succeeds, one SQL transaction must:

1. Lock or conditionally claim the submission and assert the authenticated owner and `Draft` status.
2. Lock or conditionally update the capture session and assert matching Hunter/submission/purpose, not expired, not revoked, not consumed.
3. Insert the `HunterEvidence` row with server-generated evidence ID, protected private reference, provider-verified media facts, server upload time, and approved capture metadata.
4. Record the session-to-evidence binding and consume the session exactly once.
5. Record the idempotency result in the same transaction.
6. Commit only if all conditions succeed; otherwise roll back all database changes.

**Schema implications (approval required):** add a nullable/required `CaptureSessionId` FK on evidence to the session row, with a unique constraint so one session cannot back multiple evidence rows; add an approved provider asset identifier and/or verification receipt digest; add request idempotency storage; consider a unique `(HunterSubmissionId, EvidenceType)` constraint only if exactly one item of each evidence type is approved. The current non-unique index is not such a constraint. Do not implement these schema changes before DB Coordinator approval.

External media upload/verification cannot be in the SQL transaction. If media succeeds but the DB transaction fails, the design needs an approved provider cleanup/retention strategy. Do not delete provider media automatically unless the provider contract authorizes it; do not mark it bound until the DB transaction commits.

### Step E — Submit the Draft for Admin review

Existing behavior is the atomic transition from `Draft` to `PendingReview`, set server `SubmittedAtUtc`, and revoke all unused sessions in the same transaction boundary. A generic status-only transition fails closed for submission.

**Proposal:** the submit command must load the owner’s Draft, verify all required evidence rows are present and provider-verified, verify each has a consumed single-use session binding, then call the existing atomic transition. Missing evidence or invalid capture binding is a validation error; a stale state/revision is a conflict. Whether submit may be replayed and whether a retry after a lost response returns the existing submitted representation must be explicitly approved and backed by idempotency state.

### Step F — Admin review and reward boundary

The current internal review service supports only `PendingReview -> Approved`, with a server-derived reviewer ID/time, persisted review decision, and revocation of unused sessions in a SQL transaction. It does not support Held/Rejected and does not write a wallet.

The approved product rule requires a +200 Hunter reward exactly once after approval; emitting that reward must be a separately approved M6 event/outbox integration after the approval transaction commits. Admin review must not debit/credit a wallet directly, and must not claim reward delivery if the M6 boundary is unavailable.

## 4. Proposed HTTP API contract (all routes remain unmapped)

The route paths and DTOs below are proposals for review only. They use the repository’s `api/mobile` and `api/admin` namespaces as style references; the active rules explicitly say route namespaces are proposals, not proof of approved contracts.

### Mobile routes

| Method and proposed path | Request | Response | Authorization and behavior | Status |
|---|---|---|---|---|
| `POST /api/mobile/hunter/submissions/drafts` | `CreateHunterDraftRequest`: `placeName`, `description?`, `claimedDistrict`, `latitude`, `longitude`, `gpsAccuracyMeters?`, `locationCapturedAtUtc?` | `HunterSubmissionResponseDto` plus any separately approved revision field | Authenticated active Hunter; owner ID and username snapshot server-derived. Validate mapped lengths and coordinate constraints. | Proposed; approval required |
| `GET /api/mobile/hunter/submissions/{submissionId:guid}` | None | Existing `HunterSubmissionResponseDto`; evidence summary only if approved | Owner-scoped. Missing and foreign submissions should both return 404 to avoid disclosure (proposed mapping consistent with owner concealment in the application status service). | Proposed; approval required |
| `PUT /api/mobile/hunter/submissions/{submissionId:guid}/draft` | `UpdateHunterDraftRequest`: same editable place/location fields; no status, owner, evidence IDs, or timestamps | Draft response plus approved revision | Owner and Draft only. State or revision mismatch -> 409. Whether all fields replace or patch is a decision. | Proposed; approval required |
| `POST /api/mobile/hunter/submissions/{submissionId:guid}/capture-sessions` | `StartHunterCaptureSessionRequest`: proposed `evidenceType`; current handler expects a submission ID in its DTO | `HunterCaptureSessionResponse`: `captureSessionId`, `expiresAtUtc`, and purpose only if approved | Authenticated owner of Draft; Hunter ID server-derived; server time; single-use. | Existing internal pattern; public route and purpose binding unapproved |
| `POST /api/mobile/hunter/submissions/{submissionId:guid}/evidence` | `BindHunterEvidenceRequest`: `captureSessionId`, approved `evidenceType`, opaque upload receipt/asset token, device `capturedAtUtc`, optional capture coordinates/duration | `HunterEvidenceResponse`: evidence ID, type, verified status, server `uploadedAtUtc`; never private storage URL | Authenticated owner; provider verification is mandatory and fail-closed. Bind + consume + idempotency are one DB transaction. Exact upload receipt is provider-owner decision. | Proposed; blocked on provider and DB contracts |
| `POST /api/mobile/hunter/submissions/{submissionId:guid}/submit` | `SubmitHunterDraftRequest`: proposed `Idempotency-Key` header; optionally expected revision if DB owner approves a concurrency token | `HunterSubmissionResponseDto` | Owner and Draft. Verify required evidence/session binding then atomic transition to PendingReview, timestamp and session revocation. | Internal transition exists; preconditions/API semantics require approval |

No draft-delete route is proposed for immediate implementation. Retention, soft deletion, cancellation and evidence cleanup semantics are absent and require a separate decision. No public media download route is proposed; evidence must remain private and access-mediated.

### Admin routes

| Method and proposed path | Request | Response | Authorization and behavior | Status |
|---|---|---|---|---|
| `GET /api/admin/hunter/submissions?status=PendingReview&cursor=...` | Optional approved status/page parameters | `AdminHunterSubmissionPage` with safe summary fields | Centrally authorized Admin policy; never rely on a client role/ID claim without the platform’s trusted Admin policy. | Proposed; route/paging contract required |
| `GET /api/admin/hunter/submissions/{submissionId:guid}` | None | Detail plus evidence metadata; no public media reference | Admin only; evidence access is separately audited and signed/private if approved. | Proposed; privacy contract required |
| `POST /api/admin/hunter/submissions/{submissionId:guid}/approve` | `ApproveHunterSubmissionRequest`: optional note only if approved; no actor ID | Approved response and review timestamp | Admin only. Invoke current Approved-only service; conditional `PendingReview -> Approved`; conflict on stale state. Reward emitted only through approved M6 post-commit idempotent boundary. | Internal service exists; public route and reward integration require approval |

No `hold` or `reject` route is proposed for implementation until the M4 owner approves valid transitions, reason rules, retry behavior, and whether Held items can return to review. No approved-place discovery route is included because visibility and geography contracts are not yet approved.

### Proposed common HTTP behavior

These are proposals, not current M4 API behavior:

- `401` missing/invalid authentication; `403` authenticated principal lacking the Admin policy; `404` absent or foreign owner-scoped resource; `400` malformed/invalid input; `409` state, revision, or idempotency-key conflict; `503` trusted media service unavailable (without persisting evidence as verified).
- Use the platform’s established safe error envelope; do not return exception text, provider diagnostics, private references, or internal SQL details. Exact envelope and error codes need API-owner confirmation.
- For all mobile routes, ignore/reject client-supplied actor IDs, `HunterId`, `UserId`, status, approval state, server timestamps, owner snapshots, or provider verification flags.

## 5. Idempotency, retry, replay and concurrency proposal

| Operation | Safe proposed behavior | Required durable support / decision |
|---|---|---|
| Create Draft | Same scoped key and same normalized request returns original Draft; same key with different payload returns 409. | Unique `(UserId, Operation, IdempotencyKeyHash)` record and request fingerprint; retention duration approval. |
| Start capture session | Retry with same key returns the same session and expiry; a fresh key creates a fresh session only while the Draft is eligible. | Persisted idempotency mapping; approval on whether one active session per evidence purpose is allowed. |
| Upload provider asset | Retry must not convert a different provider asset into the original request; bind only one verified asset per idempotent operation. | Provider idempotency/receipt contract and server request fingerprint. |
| Bind evidence/consume session | Exact same idempotent request may return the already committed evidence; a different request using a consumed session returns 409. A failed DB transaction leaves the session unconsumed. | Atomic evidence insert + conditional consume + idempotency result. Unique `CaptureSessionId` binding. |
| Submit for review | First valid call transitions once. Retry with same key after lost acknowledgement returns the committed submission result; a different key against a non-Draft returns 409 unless an explicit idempotent repeat rule is approved. | Atomic transition and idempotency result; conditional state update or approved rowversion. |
| Admin approve | Existing service accepts only PendingReview and conditionally records one decision; retry must not create a second review/reward. | Review idempotency key or unique decision invariant plus M6 reward source identity; post-commit event/outbox transaction design. |

Never consume a capture session before provider verification. Never mark media verified because an upload endpoint returned success alone. Never allow cross-owner session/evidence rebinding. The recommended concurrency boundary is conditional state update/row lock within a transaction; exact isolation and rowversion/schema choice are DB Coordinator decisions.

## 6. Persistence and transaction decisions

### Existing schema

- `Hunter_Submissions` has `UserId -> Users.UserId` FK, `(Id, UserId)` alternate key, status/GPS checks and indexes by owner and district/status.
- `Hunter_Evidence` has a submission FK, media reference and metadata; only a non-unique submission/type index.
- `Hunter_CaptureSessions` has composite FK `(HunterSubmissionId, OwnerUserId)` to `(Id, UserId)`, where owner is computed from HunterId, plus state indexes. It has no evidence relationship.
- Existing `TrySubmitDraftAndRevokeCaptureSessionsAsync` is intended to atomically transition status, stamp server submit time, and revoke unconsumed sessions.

### Proposed DB work (DB Coordinator approval required)

1. Add a persisted capture-session-to-evidence link, preferably `HunterEvidence.CaptureSessionId` with FK and unique index, or an equivalent link table if evidence can combine multiple sessions. Select only after the M4 owner confirms one-session-per-evidence semantics.
2. Persist a trusted media asset identifier/verification receipt digest and verifier version/time. Do not store public URLs or credentials. Exact fields depend on the provider contract.
3. Add durable idempotency records keyed by account, operation, and hashed key; persist canonical request digest and stable response/resource ID.
4. Decide whether exactly one video and one photo per submission are required, then enforce with a unique filtered index or another approved invariant. Current evidence index does not enforce uniqueness.
5. Add an explicit concurrency token (e.g. SQL rowversion) only if the owner/API chooses optimistic revision semantics; otherwise document conditional status and update predicates as the concurrency contract.
6. Approve deletion/retention and orphaned provider media reconciliation. Physical media cleanup cannot be inferred from database rollback.
7. Review the `HunterId`/`UserId` relationship and all FKs with the DB Coordinator; do not add migrations from this proposal.

### Transaction boundaries

- Draft create/update and its idempotency record: one DB transaction.
- Session creation and operation key: one DB transaction; session remains bound to existing owner Draft.
- Provider upload/verification: outside SQL transactions, through an approved backend adapter.
- Verified evidence binding + session consume + idempotency result: one SQL transaction and conditional update/lock. Commit together or roll back together.
- Submit status + `SubmittedAtUtc` + unused-session revocation + submit idempotency result: one SQL transaction. Existing transition code covers the status/timestamp/revocation part; the idempotency integration is not implemented.
- Admin approval + review record + unused-session revocation: existing SQL store transaction; M6 reward publication must use the approved durable post-commit outbox/event boundary and stable submission ID.

No SQL execution or migration is authorized by this document.

## 7. Security and privacy requirements

- Authenticate with the platform’s validated session/JWT pipeline. M1 claim/issuer finalization and M4 production DI remain cross-owner decisions; use only the authenticated server principal and trusted resolver.
- Derive owner and reviewer identities server-side. Do not accept `UserId`, `HunterId`, reviewer ID, role, `Status`, timestamps, or verified flags from request bodies.
- Enforce owner + Draft checks before session creation and evidence mutation; conceal missing/foreign mobile resources consistently if the API owner confirms 404 behavior.
- Store only private media references. Do not emit provider URLs, raw storage keys, upload credentials, or raw file data in logs/errors.
- Fail closed if the media verifier is absent, times out, returns ambiguous ownership, detects an already-bound asset, or cannot verify required media facts.
- Distinguish device-provided `CapturedAtUtc`/GPS from provider-attested metadata. Business rules must identify which source is authoritative; do not silently treat EXIF/client timestamps as trusted server time.
- Audit Admin reads of sensitive evidence and all decisions. Do not implement `Held`/`Rejected` or reward behavior under this document.
- Existing product rule: approved Hunter reward is +200 Coins once. M4 must not write wallet balances; M6 must approve and verify the durable event/idempotency boundary.

## 8. Project Lead approval checklist

### Project Lead + M4 owner

- [ ] Approve Draft creation before capture and determine which place fields can be incomplete while Draft.
- [ ] Approve Draft update and deletion/cancellation semantics, including evidence retention/cleanup.
- [ ] Confirm required evidence types and count; approve or reject video-before-photo order.
- [ ] Decide whether a capture session is bound to an evidence type and whether every evidence item requires exactly one consumed session.
- [ ] Approve when a submission is complete and may transition to PendingReview.
- [ ] Approve retry/lost-acknowledgement results for create, session start, evidence bind, submit, and Admin approve.
- [ ] Approve Held/Rejected transitions, reason rules, resubmission rules, and stale-state conflict behavior—or keep them disabled.
- [ ] Approve/adjust mobile/Admin route paths, request/response fields, paging, status/error mapping, and privacy behavior in Section 4.
- [ ] Confirm whether public endpoints remain off until durable SQL/provider acceptance, or whether an explicitly isolated development-only host is allowed.

### Media/provider owner

- [ ] Name the supported provider and define its trusted server API, opaque asset IDs and receipt verification.
- [ ] Define ownership evidence, upload authorization, media type/size/duration checks, capture metadata attestation and duplicate detection.
- [ ] Define private retrieval, retention/deletion, orphan cleanup, timeout and unavailable-provider behavior.
- [ ] Approve whether provider upload occurs through the backend or direct upload with server-verified callbacks/tokens.

### M1 owner + M4 owner

- [ ] Confirm authoritative signed identity claim/issuer and active/verified/deletion/suspension semantics for M4 requests.
- [ ] Approve production registration and lifetime for trusted account reader and M4 resolver after M1 identity integration is reviewed.
- [ ] Confirm username snapshot source or approve removing/altering that persistence requirement.

### DB Coordinator

- [ ] Approve database ownership, schema relationship and migration ownership for Draft, evidence, capture session and idempotency records.
- [ ] Approve unique constraints, concurrency token/conditional write strategy and transaction/isolation boundaries.
- [ ] Approve an isolated disposable SQL test target and explicit execution of M4 SQL concurrency/rollback tests.
- [ ] Review schema readiness against the authorized development backup only through a separately approved isolated restore process; this proposal does not authorize backup restoration or SQL execution.

### M6 owner + Project Lead

- [ ] Confirm the +200 reward event source identity, atomic post-approval outbox boundary, consumer idempotency and reconciliation. Keep reward issuance outside M4 until approved.

## 9. Exact implementation sequence after approval

1. Record signed decisions and update this proposal to an approved contract version; capture exact route and DTO identifiers in source-controlled API contract tests.
2. Finalize M1 trusted identity claim/issuer/account-state integration and review PR overlap; wire M4 resolver only after its approved composition is present.
3. Define application contracts: `CreateHunterDraft`, owner-scoped update/read, capture purpose, media verification result, evidence binding, submit, and Admin review. Make actor identity server-derived in every handler.
4. Add validators and unit tests for mapped limits, owner concealment, Draft-only state, expired/revoked/replay sessions, evidence purpose/ownership, idempotency key reuse, and state conflicts.
5. Obtain DB Coordinator approval, then implement EF mappings/repositories and migration only under a separately authorized change. Add isolated SQL tests for concurrent create/update/bind/consume/submit, transaction rollback, unique constraints, lost acknowledgement/idempotent retry, and cross-owner attempts.
6. Implement a fail-closed provider adapter contract and fake verifier for tests. Verify positive and negative ownership/metadata outcomes; never make the fake provider a production registration.
7. Implement the evidence bind transaction so verified evidence insert, session consume/link, and idempotency persistence commit together. Add provider orphan/retry tests for the approved recovery policy.
8. Implement Draft creation/update and submission application services, reusing the existing status transition transaction. Test lost acknowledgement and duplicate submission before exposing routes.
9. Add controllers only for the routes signed off in Section 4; derive identity from the authenticated principal; apply mobile owner and centralized Admin policies; verify 400/401/403/404/409/503 envelopes in HTTP tests.
10. Add the M6 reward outbox/event only after M6 approval, with committed approval as source and submission ID as stable idempotency identity. Add exactly-once consumer/reconciliation tests.
11. Run full solution build and unit tests; run non-SQL HTTP integration tests with SQL/provider suites explicitly excluded; after DB Coordinator approval run only against the isolated disposable SQL DB; keep production providers and feature flags disabled until separate acceptance.

## 10. Current readiness and next decision

**Code-ready today:** preserve and continue testing the existing Draft ownership, capture single-use/expiry/revocation, submission transition timestamp/revocation, owner-scoped read, and Approved-only review logic.

**Not safe to implement until decisions are recorded:** initial Draft service/controller; evidence upload/binding; media verification result model; capture-purpose binding; durable idempotency; schema uniqueness/FKs; public mobile/Admin routes; Held/Rejected review; M6 reward publication.

**Exact next actionable task:** Project Lead and M4 owner approve the Draft/evidence lifecycle items in the first checklist section; media owner specifies the verifier contract; DB Coordinator approves the evidence/session/idempotency persistence design. Once those are recorded, implement the application interfaces and tests before adding any controllers.

**Status:** Decision proposal only. Existing M4 code remains unchanged by this document; no route is exposed, no provider activated, no SQL/migration run, and no reward issued.
