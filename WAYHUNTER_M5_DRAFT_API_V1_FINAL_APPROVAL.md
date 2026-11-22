# WAYHUNTER M5 Draft API v1 — Final Approval Package

**Branch:** `task/backend-m1-m7-completion`
**Decision status:** Project Lead approved guarded Development-only implementation on 2026-11-22; production exposure remains disabled pending durable persistence and acceptance.
**Scope:** create, owner-read, replace/update, and delete an unsubmitted Game draft only. Submission, moderation, approval, discovery, attempts, proof, rewards, and expiry are out of scope.

## 1. Evidence reviewed

- `AGENTS.md` and `docs/second-brain/ACTIVE_RULES.md`: server-side identity and authorization; Games creation requires Admin approval; approved games become public only after approval; expiry is 30 days after approval and means archive/retain, not physical deletion; no speculative routes or contracts.
- Existing internal `GameDraftApplicationAdapter`: resolves active identity and delegates to `VerifiedGameDraftService`, `GameService`, and `GameDraftDeletionService`; it has no MVC attributes and no DI registration.
- Existing DTOs: `CreateGameDraftDto(Title, Description, DurationMinutes, Stages)` and `GameDraftStageDto(StepNumber, TextHint, PhotoMediaId)`.
- Existing validation/domain: title required, title max 150, description max 2,000, positive duration, at least one stage, positive unique stage numbers, every stage has text or photo hint, and persisted stage numbers must be contiguous from 1 through N. No maximum duration, stage count, or text-hint length is currently enforced in the inspected domain validator/model.
- Media verification: clue media is verified before create/update; the registered default `UnavailableGameClueMediaAccessVerifier` rejects every media asset. Text-only clue stages are supported.
- Ownership/lifecycle: read/update/delete enforce creator identity; read only returns `Draft`; update checks Draft before replacement; delete requires Draft. Existing service exceptions: `KeyNotFoundException` when absent, `UnauthorizedAccessException` for wrong owner, `InvalidOperationException` for wrong lifecycle state.
- Identity: `TrustedIdentityResolutionService` validates the principal with `TrustedIdentityAccessContract`, loads the persisted user through `ITrustedIdentityAccountReader`, and requires active, verified, non-deletion-requested account state. No concrete `ITrustedIdentityAccountReader` implementation is registered in production DI.
- Platform errors: `ApiErrorResponse` provides `{ code, message, correlationId }`, and middleware emits `X-Correlation-ID`. Existing controllers do not use one uniform envelope: examples use bare `Unauthorized()`/`Forbid()`, `{message}`, `{code}`, and other shapes. Therefore the envelope below is a recommendation requiring API/Team Leader approval, not an established universal platform contract.
- Persistence: default M5 application registration uses a singleton in-memory store. Optional SQL CRUD persistence is gated by `Games:EnableSqlPersistence`; SQL repository supports rowversion internally, but current draft DTO/adapter does not expose a revision/ETag to clients. No SQL was executed or enabled for this review.

## 2. Proposed v1 routes and semantics

All paths below are proposals, not active routes. Recommended common route root is `/api/mobile/games/drafts`; do not add a `/v1` segment unless the mobile/API owner confirms versioning convention. The current repository's identity namespace and `/api/mobile/v1` references do not establish this module's route contract.

| Method and proposed path | Proposed success | Meaning |
|---|---:|---|
| `POST /api/mobile/games/drafts` | `201 Created` | Create a new Draft owned by the server-resolved active user. `Location` points to the owner-read URI. |
| `GET /api/mobile/games/drafts/{gameId:guid}` | `200 OK` | Return an owned draft only. Do not return submitted, approved, rejected, expired, disabled, or other users' games from this draft endpoint. |
| `PUT /api/mobile/games/drafts/{gameId:guid}` | `200 OK` | Replace the complete editable draft representation. Creator and Draft-state checks apply. |
| `DELETE /api/mobile/games/drafts/{gameId:guid}` | `204 No Content` | Remove a draft only. This is not the M5 approved-game archive operation and cannot delete non-drafts. |

No actor ID, creator ID, Game status, admin decision, reward data, expiry, submitted/approved timestamp, or internal persistence token is accepted from a client body.

## 3. Proposed request DTOs

Use the existing `CreateGameDraftDto` shape for both POST and PUT. The following JSON field names are the proposed camelCase wire representation; DTO casing/serializer behavior must be confirmed with the mobile/API owner.

```json
{
  "title": "Old Town Discovery",
  "description": "Optional description",
  "durationMinutes": 90,
  "stages": [
    {
      "stepNumber": 1,
      "textHint": "Look near the old clock",
      "photoMediaId": null
    }
  ]
}
```

| Field | Type | Required and current validation |
|---|---|---|
| `title` | string | Required, non-whitespace; domain maximum 150 characters; trimmed before storage. |
| `description` | string or null | Optional; domain maximum 2,000 characters; trimmed before storage. |
| `durationMinutes` | integer | Required and `> 0`; no upper bound exists in current code. Add a maximum only after owner approval. |
| `stages` | array of stage objects | Required, non-null, at least one. The domain requires contiguous numbering `1..N`; API validation should enforce this before persistence. No maximum count is currently defined. |
| `stages[].stepNumber` | integer | Positive and unique; collectively contiguous from 1 through number of stages. |
| `stages[].textHint` | string or null | Optional individually; may be blank only if `photoMediaId` is supplied. Text length limit is not currently defined. |
| `stages[].photoMediaId` | string or null | Optional individually; may be blank only if `textHint` is supplied. Must be a server-recognized media asset owned by the actor and allowed for a creator clue. Current default verifier rejects it. |

POST has no `gameId` or caller identity. PUT takes `gameId` from the route and treats the body as a full replacement, not a patch. Unknown fields should not affect authority. Whether the API rejects unknown JSON fields is a serializer policy decision.

## 4. Proposed response DTO

No response DTO currently exists. Add a dedicated `GameDraftResponseDto`; do not serialize the domain entity directly. Proposed fields:

```json
{
  "gameId": "00000000-0000-0000-0000-000000000000",
  "title": "Old Town Discovery",
  "description": "Optional description",
  "durationMinutes": 90,
  "status": "Draft",
  "stages": [
    {
      "stepNumber": 1,
      "textHint": "Look near the old clock",
      "photoMediaId": null
    }
  ]
}
```

Do not include `CreatorUserId`, user display data, step/hint persistence IDs, capture/proof data, moderation data, reward data, rowversion bytes, or unrelated lifecycle metadata. Use a deterministic order by `StepNumber`; join each hint to its stage by `GameStepId`. Create/GET/PUT use this response. DELETE returns no response body.

**Revision field decision:** SQL rowversion already protects repository updates, but it is not client-visible and the current service reloads the entity for each update. To prevent stale clients silently overwriting another edit, approve an opaque ETag/`If-Match` contract (recommended) or explicitly accept last-write-wins. Recommended: response includes opaque strong `ETag`; PUT and DELETE require `If-Match`; stale/missing revision returns `409 DRAFT_VERSION_CONFLICT` (missing precondition may instead be `400` if the API owner prefers). No such field/header behavior exists today. In-memory storage must implement equivalent revision behavior if used for public multi-user testing; otherwise it is development-only and does not satisfy this contract.

## 5. Identity and authorization contract

1. All four actions require authentication (`[Authorize]` once implemented).
2. Resolve `sub`/NameIdentifier only through `TrustedIdentityAccessContract`; reject absent, malformed, duplicate, or inconsistent identity claims. Do not trust `creatorUserId`, `userId`, or `actorId` from route/query/body/header.
3. Resolve the corresponding persisted `UserAccount` through a concrete, registered `ITrustedIdentityAccountReader`; require account ID match, `Active`, verified UTC timestamp, and no deletion request. The existing `TrustedIdentityResolutionService` can enforce these checks once its account reader exists.
4. Owner scope is always derived from that resolved `UserId`. Creator-only read/update/delete is enforced in application/persistence layers, not only in the controller.
5. Do not activate or expose routes until M1 owner approves the account reader source/registration and claim mapping, and Team Leader/M5 owner approve the endpoint policy.

### Production account-reader work

The adapter requires `TrustedIdentityResolutionService`, which requires `ITrustedIdentityAccountReader`. No concrete implementation was found in current API registrations. The likely data source is the authoritative M1 `UserAccount` store/`WayhunterSharedDbContext.Users`, but its suitability, SQL ownership, registration lifetime, test strategy, and behavior for deleted/missing accounts need M1 owner confirmation. Do not add a second competing user store or silently query shared SQL from M5. The eventual reader should be read-only, cancellation-aware, return null for no account, and be registered once in the shared API composition root. Add tests proving it returns only by canonical immutable `UserId` and honors account lifecycle checks in `TrustedIdentityResolutionService`.

## 6. Proposed HTTP error behavior

Recommended stable body, subject to API/Team Leader approval:

```json
{
  "code": "GAME_DRAFT_INVALID",
  "message": "The draft request is invalid.",
  "correlationId": "request-correlation-id"
}
```

Use `ApiErrorResponse` and existing `X-Correlation-ID` middleware if approved. Never echo secrets or full submitted media metadata. Prefer safe, stable codes; detailed validation may be returned as field errors only after that shape is approved.

| Status | Proposed cases | Proposed codes / security notes |
|---:|---|---|
| `400 Bad Request` | Malformed JSON; required fields absent; invalid title/description/duration/stages; non-contiguous/duplicate stage numbers; stage lacks both hint forms; unsupported media reference. | `GAME_DRAFT_INVALID`, `GAME_DRAFT_MEDIA_INVALID`. No persistence mutation on validation failure. Current media verifier signals `UnauthorizedAccessException`, so the HTTP layer must distinguish invalid/foreign media from identity failure before mapping; do not blindly map every such exception to 401. |
| `401 Unauthorized` | No authenticated principal; identity claims absent/malformed/duplicated/inconsistent. | `AUTHENTICATION_REQUIRED` or existing identity code. Preserve normal authentication challenge semantics. |
| `403 Forbidden` | Authenticated principal has no active verified account, is suspended, or has deletion requested; alternatively API owner may choose 401 for inactive accounts to align with its existing resolver behavior. | `ACCOUNT_NOT_ACTIVE` proposal. Cross-owner resource access should not reveal existence; use 404 instead. Existing My Profile returns bare 401 for malformed identity and bare 403 when account is inactive/missing. |
| `404 Not Found` | Unknown ID; another user's draft; or GET of non-Draft game (recommended to avoid an existence/state oracle). | `GAME_DRAFT_NOT_FOUND`. Returning 404 for another owner's resource is recommended; some current services throw `UnauthorizedAccessException`, so the controller must deliberately map owner mismatch to non-disclosing 404 without weakening service authorization. |
| `409 Conflict` | Owner addresses a known resource whose state has changed since it was a Draft; submitted/approved/etc. update/delete; stale ETag/revision; create idempotency key reused with different request. | `GAME_DRAFT_STATE_CONFLICT`, `GAME_DRAFT_VERSION_CONFLICT`, `IDEMPOTENCY_KEY_REUSED`. Current services use `InvalidOperationException` for state conflicts; persistence SQL may raise EF concurrency exceptions. |

Route constraint `/{gameId:guid}` means a malformed GUID path normally fails routing (404), not controller validation (400). If a 400 is desired for malformed path IDs, remove/adjust the constraint only with route approval. Do not expose raw exception messages; current controllers vary between bare status, `{message}`, `{code}`, and `{error}` bodies. The recommended shared envelope is a proposal, not a verified platform-wide rule.

## 7. Deletion, retries and idempotency

| Operation | Recommended semantics | Current behavior / decision needed |
|---|---|---|
| Create POST | Support `Idempotency-Key` scoped to authenticated creator. Same key + same normalized payload returns the original Game ID/response; same key + different payload returns 409. Persist key/result atomically with draft creation and retain for a defined period. | No key/operation ledger exists for draft creation. Without it, retry after lost 201 may create duplicate drafts. Team Leader/M5 owner and DB Coordinator must approve key format, retention, payload hash, storage, and replay response. If omitted, explicitly accept at-least-once creation and duplicate drafts. |
| Read GET | Safe to retry; no mutation. | Existing service is owner-only and Draft-only. Proposed 404 for missing/non-owned/non-Draft. |
| Update PUT | Full replacement; should be retry-safe with revision precondition. Same payload with current revision succeeds once; retry using consumed revision returns 409 with current revision or a documented retry result. | Current service full-replaces stages/clues, but no request revision/ETag exists. Without revision, a stale client can overwrite newer data. Approve ETag/`If-Match` semantics or explicitly accept last-write-wins. |
| Delete DELETE | Remove only the caller-owned Draft; never remove submitted or later states. Recommend repeat DELETE after a successful delete returns 204 (idempotent response) and unknown ID also returns 204 or 404 by explicit decision. | Current service returns `KeyNotFoundException` for already absent resource, so repeat likely maps 404; no operation ledger/tombstone. Recommend owner-approved idempotent retry policy. If a non-owner targets a still-existing resource, return 404 without deleting it. |

Deletion refers only to unsubmitted Draft records and their draft child data. It must never be reused for approved-game expiry: approved games are retained and archived under the active rules. Decide whether delete physically removes a draft and its child rows or marks it deleted; the current `IGameDraftDeletionRepository.DeleteAsync` describes physical draft deletion, while SQL FK/cascade behavior and audit requirements require DB Coordinator review.

## 8. Persistence and deployment suitability

- **In-memory store:** suitable for unit/integration tests and local development only. It is a singleton in one process, loses all drafts on restart, is not shared across replicas, and cannot provide durable idempotency/revision history. Do not deploy it as production draft storage.
- **Durable storage:** production or shared acceptance requires SQL-backed `IGameRepository`/`IGameDraftDeletionRepository` (or another DB Coordinator-approved durable store) with atomic create/update/delete, stage/hint persistence, owner filtering, concurrency, and idempotency storage if approved. The SQL path is behind `Games:EnableSqlPersistence`; this proposal does not enable it.
- **SQL acceptance:** DB Coordinator must confirm schema/migration status, rowversion behavior across requests, delete FK handling, transaction boundaries, indexes, and isolated database tests. Do not run migrations or SQL as part of contract approval.
- **Media:** until a trusted provider is approved and wired, photo clue media remains unusable; text-only drafts are the only successful create/update path using the current default verifier.

## 9. Exact sign-offs requested

| Owner | Exact decision required | Recommended decision |
|---|---|---|
| Team Leader | Approve public namespace/version and exact four method/path pairs; authorize exposing only draft CRUD. | Approve the four listed paths without `/v1` only if that matches the mobile route convention; otherwise specify the versioned route root. Explicitly exclude submit/review/proof/reward routes. |
| M5 owner | Approve create/update body and response DTO fields, full-replacement semantics, title/description constraints, duration/stage/hint limits, ordering, whether text-only clues are valid, and whether drafts may be physically deleted. | Approve existing request fields and limits as listed; add explicit max duration, stage count, and hint limits before release; confirm text-only; allow deletion only while Draft. |
| Team Leader + API/mobile owner | Approve 201/200/204 success codes, `{code,message,correlationId}` envelope, code names, 401/403/404/409 mapping, malformed-GUID behavior, non-disclosure 404, ETag/If-Match, and idempotency behavior. | Use shared envelope; malformed GUID gets routing 404; inaccessible/non-Draft reads return 404; state/version conflicts return 409; support POST idempotency key and ETag for update/delete. |
| M1 owner | Approve authoritative account source, `ITrustedIdentityAccountReader` implementation/registration/lifetime, issuer and `sub`/NameIdentifier mapping, account state-to-401/403 mapping. | Reuse the canonical M1 `UserAccount` reader; preserve `TrustedIdentityAccessContract`; do not accept actor identity in request payload. |
| DB Coordinator | Approve durable storage requirement and schema/FK/rowversion/idempotency implementation; approve isolated SQL test plan/target separately. | Require durable SQL (or approved equivalent) before production; keep in-memory only for tests/local development; verify concurrency and atomic retry semantics on disposable isolated DB before acceptance. |

## 10. Implementation-ready checklist after sign-off

### Contract and identity

- [ ] Attach explicit Team Leader and M5 owner approval for route root, verbs, DTOs, success/error semantics, draft deletion and retry/idempotency behavior.
- [ ] Attach M1 owner approval and implement concrete `ITrustedIdentityAccountReader` over the canonical M1 account source; register it in shared DI; add active/verified/deletion-state tests.
- [ ] Approve common API error envelope and establish a deterministic exception/status mapping without leaking cross-owner existence.
- [ ] Decide revision protocol and POST idempotency protocol, including server-side persistence and expiry/retention.

### Application and persistence

- [ ] Add dedicated request/response API DTOs (map the existing application DTO; do not serialize `Game`).
- [ ] Make validation consistent: contiguous steps, title <=150, description <=2000, and owner-approved duration/stage/text limits; reject before any write.
- [ ] Add `If-Match`/ETag support and repository concurrency contract if approved; map stale writes to 409.
- [ ] Confirm SQL child deletion/transaction behavior, rowversion across scoped requests, durable idempotency, and ensure draft delete cannot affect non-Draft games.
- [ ] Keep SQL feature flag off until separately authorized; never use singleton in-memory as production storage.
- [ ] Keep media verification fail-closed; add successful image clue tests only after approved verifier is wired. Text-only flow should remain covered.

### HTTP surface and tests

- [ ] Add authorized controller only after all required approvals; apply `[Authorize]`; obtain actor exclusively from trusted resolver.
- [ ] Implement POST 201+Location, GET 200, PUT 200, DELETE 204 per signed contract.
- [ ] HTTP tests: anonymous/invalid identity 401; authenticated inactive account 403; cross-owner 404; unknown ID; invalid DTO 400; invalid media; stale/non-Draft 409; create replay; same-key/different-body conflict; update replay/version; repeated delete; authorization preserved for every method.
- [ ] Verify generated OpenAPI/route table contains only these approved draft routes and no proof/reward/review routes.
- [ ] Run full build, unit tests, non-SQL HTTP suite; run isolated SQL concurrency/idempotency tests only after DB Coordinator authorizes target and execution.

## 11. Approval record

| Role | Approver / reference | Decision | Date |
|---|---|---|---|
| Team Leader | Pending | Pending | Pending |
| M5 owner | Pending | Pending | Pending |
| API/mobile owner | Pending | Pending | Pending |
| M1 owner | Pending | Pending | Pending |
| DB Coordinator | Pending | Pending | Pending |

**Current release status:** the four routes are implemented behind a Development-only MVC discovery gate. Production/default configurations leave them unmapped. Durable SQL, replay/revision acceptance, media provider approval, and production release remain blocked.

## 12. Project Lead development-scope approval (2026-11-22)

This record supersedes the earlier Pending entries only for local Development-scope implementation. It does not approve production route exposure, SQL persistence, migrations, providers, proof recovery, or rewards.

### M1 decisions approved for this implementation

- Canonical lookup source: `WayhunterSharedDbContext.Users` / immutable `UserAccount.UserId`.
- Trust the `sub` from the validated signed session token. `NameIdentifier` is acceptable only as the same-value compatibility claim generated after token validation.
- Require Active, verified, and not deletion-requested account state. Missing, malformed, suspended, unverified, deletion-requested accounts and revoked/expired sessions fail closed.
- Register `ITrustedIdentityAccountReader` scoped with the shared `WayhunterSharedDbContext` in `AddWayhunterIdentityPersistence`.

### M5 Draft API v1 development decisions

- The four exact routes in section 2 are approved for explicit Development-only routing.
- Authentication is required; actor identity is resolved only from the validated server principal. Cross-owner access is concealed as 404. Invalid request/media is 400; missing/invalid/inactive authentication is 401; state/repository revision conflicts are 409. Existing `{code,message,correlationId}` error shape is used.
- Reuse `CreateGameDraftDto` for POST and full-replacement PUT. Responses use a dedicated projection containing gameId, title, description, durationMinutes, status, and ordered stages (`stepNumber`, `textHint`, `photoMediaId`). Never return creator IDs or persistence tokens.
- Text-only clues are supported. Media must pass the existing fail-closed verifier. No proof/review/reward routes are in scope.
- Existing service semantics govern retries: POST has no idempotency key; repeated DELETE after successful physical deletion returns 404; no client ETag/revision is introduced. SQL repository concurrency exceptions map to 409, but stale-client overwrite prevention is not provided by the current DTO/service contract and remains a production decision.
- In-memory draft persistence is approved only for isolated Development/testing. Production and shared environments require separately approved durable storage and SQL verification. The development route feature guard is disabled by default, requires Development + explicit `Games:EnableDevelopmentDraftHttpApi=true` + shared authentication enabled + SQL Game persistence disabled.

**Remaining approval gates:** production route exposure, durable SQL schema/rowversion/delete/retry acceptance (DB Coordinator), client revision/idempotency semantics where required for release, media provider activation, and M5 Phase B/C proof/reward workflows remain out of scope and unapproved.
