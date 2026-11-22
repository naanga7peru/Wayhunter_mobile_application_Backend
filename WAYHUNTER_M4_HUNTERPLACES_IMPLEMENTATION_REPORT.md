# M4 HunterPlaces Implementation Report

**Branch:** `task/m4-hunterplaces-api-completion`
**Starting commit:** `e3b0e9336e9f19003811f01649491c27fa3730a5`
**Scope:** Internal submission status and Admin approval application/persistence foundation. No public routes, SQL migrations, SQL execution, provider activation, or commits were made.

## Implementation completed

### Owner-scoped status read

- Added `IHunterSubmissionStatusReader` and `HunterSubmissionStatusSnapshot` for an authoritative storage projection.
- Added `SqlHunterSubmissionStatusReader`, which performs an `AsNoTracking` query over the existing `Hunter_Submissions` mapping.
- Added `HunterSubmissionStatusService.GetMySubmissionAsync`. It accepts only a canonical nonempty GUID actor ID, compares the owner ID loaded from storage, conceals both missing and foreign submissions as `null`, and returns only the existing `HunterSubmissionResponseDto` fields.
- Added tests for owner access, field projection, owner concealment, malformed/empty actors, cancellation, and the query projection.

### Approved Admin approval path

- Added `IHunterReviewStore`, `HunterReviewService`, and `SqlHunterReviewStore` using the existing `HunterReviewDecision` and EF mappings.
- The application service requires a canonical server-supplied Admin user ID and obtains `ReviewedAtUtc` from `TimeProvider`. It is not an Admin authorization boundary; a future controller/caller must authenticate and authorize the principal before supplying the actor ID.
- The SQL adapter uses a serializable transaction and row lock. It conditionally moves only `PendingReview` to `Approved`, writes the review record, revokes unused capture sessions, and commits those changes together. A missing/stale/non-pending submission returns no result; the application service reports a conflict-like `InvalidOperationException`.
- The service deliberately accepts only `Approved`. `Held` and `Rejected` semantics, including required reasons and whether a held item can be resubmitted, remain owner decisions and are not enabled here.
- The service does not credit Coins or activate an M6 reward writer. M6 owns wallet mutation; reward delivery needs its approved durable boundary and acceptance verification.

### Submission transition and validation

- Added `TrySubmitDraftAndRevokeCaptureSessionsAsync` to the transition store contract. `HunterSubmissionStatusTransitionService` uses server UTC to stamp `SubmittedAtUtc`; both the SQL and in-memory adapters update status, timestamp, and unused capture-session revocation as one operation/synchronization boundary.
- The generic Draft-to-PendingReview transition now fails closed when used without the submission operation, preventing a status-only transition that omits the timestamp.
- Added SQL-gated acceptance cases for atomic submission timestamp/status/session changes, Admin approval and replay, and rollback when session revocation fails. They were compiled but not executed because SQL access was prohibited and all M4 SQL opt-in configuration was unset.
- Added request string length validation matching the existing mapped column limits: place name 200, description 2,000, district 100. Evidence ownership, capture validity, and district verification are not inferred by these text checks.

## Changed files

Application:

- `src/Wayhunter.Application/Modules/HunterPlaces/Dtos/HunterCaptureSubmissionAccessSnapshot.cs`
- `src/Wayhunter.Application/Modules/HunterPlaces/Dtos/HunterSubmissionStatusSnapshot.cs` (new)
- `src/Wayhunter.Application/Modules/HunterPlaces/Interfaces/IHunterReviewStore.cs` (new)
- `src/Wayhunter.Application/Modules/HunterPlaces/Interfaces/IHunterSubmissionStatusReader.cs` (new)
- `src/Wayhunter.Application/Modules/HunterPlaces/Interfaces/IHunterSubmissionStatusTransitionStore.cs`
- `src/Wayhunter.Application/Modules/HunterPlaces/Services/HunterReviewService.cs` (new)
- `src/Wayhunter.Application/Modules/HunterPlaces/Services/HunterSubmissionStatusService.cs` (new)
- `src/Wayhunter.Application/Modules/HunterPlaces/Services/HunterSubmissionStatusTransitionService.cs`
- `src/Wayhunter.Application/Modules/HunterPlaces/Validators/HunterSubmitRequestValidator.cs`

Infrastructure:

- `src/Wayhunter.Infrastructure/Persistence/Repositories/HunterPlaces/InMemoryHunterSubmissionStatusTransitionStore.cs`
- `src/Wayhunter.Infrastructure/Persistence/Repositories/HunterPlaces/SqlHunterReviewStore.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/HunterPlaces/SqlHunterSubmissionStatusReader.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/HunterPlaces/SqlHunterSubmissionStatusTransitionStore.cs`

Tests:

- `tests/Wayhunter.IntegrationTests/Modules/HunterPlaces/M4SqlIntegrationVerificationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/HunterConsumeAuthorizedCaptureSessionHandlerTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/HunterReviewServiceTests.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/HunterSubmissionStatusServiceTests.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/HunterSubmissionStatusTransitionServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/HunterSubmitRequestValidatorTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/InMemoryHunterSubmissionStatusTransitionStoreTests.cs`
- `tests/Wayhunter.UnitTests/Modules/HunterPlaces/SqlHunterSubmissionStatusReaderTests.cs` (new)

## API and integration status

- **Public M4 routes added: none.** No M4 controller existed in the inspected API tree, and route/DTO/error/pagination contracts remain unapproved in the M4 readiness documents. The application and SQL adapters are not registered for production runtime use.
- Capture-session start/consume handlers and the proposed `TrustedHunterCaptureSessionIdentityResolver` already exist. Production M4 identity DI and endpoint authentication wiring remain disabled.
- M4 submission creation and evidence attachment remain unimplemented. `HunterSubmitRequestDto` supplies evidence IDs described as already uploaded, but `HunterEvidence` is keyed to an existing `HunterSubmission`; capture-session handlers also require an existing Draft submission. There is no approved initial Draft creation/binding sequence, no concrete media ownership verifier, and no trusted media upload/capture adapter. A submission service cannot safely establish that the evidence IDs belong to this Hunter or that required capture sessions were consumed.
- Admin queue/list/detail, approved-place discovery, district authority, media delivery/signing, and mobile-safe status/error mapping remain unimplemented or contract-blocked.

## Batch 2 — Submission creation and capture/media boundary review

### Contract-backed behavior verified in source

- Capture-session start and consume require a server-supplied authenticated account ID; the handlers reject missing identity and use `HunterCaptureSessionIdentityResolutionService` to resolve the trusted Hunter ID. Client-supplied Hunter identity is not accepted by these handlers.
- Capture operations require an existing submission owned by that account and in `Draft` state. The ownership service conceals missing, foreign, and ineligible submissions behind the same access-denied result.
- Capture-session creation uses backend `TimeProvider` time and a ten-minute lifetime. The store contract requires the session, Hunter, submission, validity, and unconsumed state checks to be atomic.
- Consumption is single-use. Existing in-memory store and authorized-handler tests cover replay, wrong Hunter, wrong submission, expiry, revocation, and rejection after a submission transition revokes unused sessions. SQL concurrency/rollback cases remain opt-in and were not executed.
- The existing Draft-to-PendingReview operation is atomic with the `SubmittedAtUtc` timestamp and revocation of unconsumed sessions in the SQL adapter; the in-memory adapter synchronizes the corresponding state change. The ordinary status-only path fails closed for this transition.

### Submission creation remains blocked; no speculative service added

- The M4 scope document is explicitly a structure placeholder and points to an unavailable Obsidian milestone brief; the repository capture-readiness documents mark identity/persistence/schema details pending.
- `IHunterSubmissionService` is only an interface. `HunterSubmitRequestDto` takes evidence IDs that are described as already received by the backend, but no media ownership verifier/provider interface or concrete evidence-ingestion adapter exists in the inspected M4 source.
- `HunterEvidence` has a required FK to `HunterSubmission`. Capture-session start also requires that submission already exist. The available contracts do not define how an initial Draft is created, how uploaded media is bound to it, which consumed capture session proves which evidence, or whether a failed submit may retry/reuse evidence.
- Implementing a create/submit service without those answers would either accept unverified evidence IDs, permit session/evidence substitution, or require an unapproved persistence model. No new code was added for that flow, and no public HTTP route is mapped.
- There are no approved M4 public routes in the inspected repository contract documents, so M4 HTTP integration tests are not applicable yet. Existing security coverage is application/unit-level; non-SQL HTTP tests elsewhere in the solution do not constitute M4 endpoint acceptance.

### Exact approvals needed to unblock the next slice

1. **M4 owner + Team Leader:** approve the initial Draft lifecycle (creation point and state), required evidence types/order, whether each evidence item must reference a consumed capture session, submit/replay semantics, and allowed status transitions.
2. **Media/storage owner:** define the trusted backend media identity/ownership verification contract, authoritative owner and capture metadata source, and the failure behavior when verification is unavailable. This does not authorize activation of a production media provider.
3. **M4/API owner + Team Leader:** approve exact route paths, DTOs, authorization/error mapping, and whether routes may be exposed in a development-only host before persistence approval.
4. **DB Coordinator:** approve the durable relationship and transaction boundary for Draft/evidence/session binding, the isolated test database target, and execution of the opt-in M4 SQL acceptance tests. No schema or SQL was changed/run.
5. **M1 owner + M4 owner:** approve runtime identity composition and authoritative claim/account-state behavior before production DI or authenticated controller wiring.

No Team Leader decision in the current task grants permission to infer media-provider trust, alter the M4 FK model, or expose unapproved public routes.

## PR #288 overlap

PR #288 is open and unmerged, targets `develop`, and carries M1 identity, M2 redirect protection, M5 Games, and related documentation from `task/backend-m1-m7-completion`. Its changed-file list contains no M4 HunterPlaces application, persistence, controller, or test paths modified in this batch. No code was copied or cherry-picked from the PR. Integrate the M1 reader/DI through normal review before enabling the existing M4 resolver at runtime.

## Verification

- `dotnet restore Wayhunter.slnx` — passed in the selected M4 worktree.
- `dotnet build Wayhunter.slnx --no-restore` — passed, 0 warnings and 0 errors. Initial sandbox attempt could not write generated test artifacts; the approved rerun succeeded.
- Full unit tests — **3,279 passed, 0 failed, 0 skipped**.
- Filtered non-SQL integration suite — first sandbox attempt failed in test-host exception logging due Windows Event Log access and timed out one SignalR flow; the same run with approved elevated test execution passed **212/212**, 0 failed, 0 skipped.
- `git diff --check` — passed.
- Before integration testing, all checked SQL opt-in environment variables were absent: M3 standard/GPS/session/safe-check-in, M4, M5, and M6. The SQL test classes `M3GpsConsentSqlTests`, `M3LiveTripSessionSqlTests`, `M3MembershipRevocationSqlConcurrencyTests`, `M3MembershipRevocationSqlRollbackTests`, `M3InvitationDecisionSqlTests`, `M3SqlExecutionStrategyTests`, `M3SafeCheckInSqlTests`, `M4SqlIntegrationVerificationTests`, `M5SqlGameRepositoryTests`, `M5SqlGameAttemptRepositoryTests`, `M5SqlGameProofSubmissionRepositoryTests`, `M5SqlGameProofReviewRepositoryTests`, and `SqlCoinRewardConcurrencyTests` were explicitly excluded from the non-SQL run. The suite also has no provider-credential opt-in gates in its source; no external provider was activated.
- No SQL server connection, migration, live provider, reward writer, or production feature flag was used. The new SQL acceptance cases are source/build verified only.

## Remaining decisions and blockers

| Owner | Decision/evidence needed | Work blocked |
|---|---|---|
| Team Leader + M4/API owner | Approve exact mobile submission, owner status, Admin review and approved-place route families, DTOs, error envelope, visibility, pagination and status-transition behavior. | No public M4 HTTP routes or endpoint acceptance claims. |
| M4 owner | Approve initial Draft creation and the sequence binding uploaded media/evidence to a submission and capture session; confirm required evidence types, capture order/replay, district validation, and evidence retention/access. | Concrete submission creation service, media verification, evidence binding, capture and submission endpoints. |
| M4 owner + Team Leader | Decide `Held`/`Rejected` transitions, reason requirements, whether Held can return to review, repeated decisions, and conflict behavior. | Those decisions are rejected by the new review service; only Approved is executable. |
| M1 owner + M4 owner | Approve production identity resolver composition and confirm validated principal/session source and active/deletion behavior in this branch. PR #288 supplies a candidate M1 reader/registration but remains unmerged. | M4 runtime identity DI and authenticated HTTP actions. |
| DB Coordinator | Approve shared schema ownership and deployed mapping verification, isolated SQL target/instance, and execution of the new M4 SQL acceptance tests. | SQL adapter acceptance and production SQL registration. No migration changes were made. |
| M6 owner + DB Coordinator | Approve/review atomic reward event or outbox behavior after committed Hunter approval, using submission ID idempotency; verify reward source/reconciliation. | Exactly-once +200 integration acceptance. This M4 service never writes wallet balances. |

## Next safe implementation batch

1. Obtain M4/API owner sign-off on the draft/evidence/capture lifecycle and exact route contract; resolve Held/Rejected and error mappings.
2. With the media owner’s contract, implement a submission persistence boundary that verifies backend-owned media and consumed capture sessions before moving Draft to PendingReview; add owner/replay tests.
3. Integrate the M1 trusted reader and M4 resolver through reviewed PR/DI composition, then implement only the signed-off controllers and authorization policies.
4. Obtain DB Coordinator approval, run the isolated M4 SQL concurrency/rollback/review acceptance tests, and verify the deployed schema separately.
5. Integrate the M6-approved +200 reward event/outbox only after commit-boundary and idempotency review.

**Completion status:** Partial internal application/persistence foundation. M4 HTTP APIs, media-backed submission creation, production DI, SQL acceptance, identity runtime integration, and reward delivery are not complete or verified.
