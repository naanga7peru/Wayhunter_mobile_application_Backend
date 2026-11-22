# WAYHUNTER Backend M1–M7 Completion Report

**Status: PARTIAL — backend completion is not verified.**

## Baseline and working branch

- Branch: `task/backend-m1-m7-completion`
- Starting commit: `e3b0e9336e9f19003811f01649491c27fa3730a5`
- Changes are uncommitted. No commits, pushes, merges, migrations, live database operations, or production providers were activated.
- Existing untracked audit files and `tmp/` were preserved without modification.
- Required product rules were read from `docs/second-brain/ACTIVE_RULES.md` and `docs/second-brain/CONTEXT.md`. M6 voucher behavior follows the current approved rule: eligibility at 10,100 spendable Coins, reserve 10,000, debit once on Admin approval, and release on rejection.

## Implemented in this session

### M5 dependency injection batch

- Added `AddGamesApplicationServices` to register the existing `InMemoryGameSubmissionStore` as a single shared instance across the M5 repository/workflow interfaces it implements.
- Registered the M5 game draft, draft deletion, submission, approval, moderation, and pending stage-proof query application services.
- Wired the registration into API startup before the existing configured SQL CRUD registration. SQL stays opt-in and replaces only the two CRUD interfaces it currently implements; remaining workflows remain in-memory and are not represented as durable SQL behavior.
- Added a unit test that resolves these services and verifies the store bindings share the same instance.
- No new HTTP routes were added because M4/M5 route and trusted-auth contracts are not code-verified as approved. This follows the repository rule not to invent endpoints.

### M5 approved expiry archive batch

- **Task alignment:** partial implementation foundation for `M5-MS08-T06` / `M5-MS08-T09`; acceptance remains blocked by schema approval, SQL end-to-end read/start verification, and explicit worker activation gates in `docs/architecture/M5_GAMES_SCHEMA_READINESS.md`.
- Added `IExpiredGameArchiveStore` and `ArchiveExpiredGamesService`. The service uses `TimeProvider` for authoritative UTC time and validates positive batch sizes.
- Implemented repeatable expiry archival in the in-memory M5 store and the existing SQL Game repository. The SQL implementation queries only published games whose expiry is due, then uses the repository's row-version guarded update path to set the domain-approved `Expired` state. It does not physically delete games or update reward records.
- Registered the archive store/service with the existing M5 DI setup. SQL-enabled registration replaces the in-memory archive store with the SQL repository implementation.
- Added tests for archive idempotency, retained record visibility by ID, expiry play denial, batch argument validation, and DI resolution.
- No new HTTP routes were added. Production SQL activation remains opt-in and unverified.

### M5 opt-in expiry worker batch

- Added `GameExpiryHostedService`, which runs an archive pass and then polls on a configurable interval. It logs counts and failures without logging game/player data.
- Registered the worker only when both `Games:EnableSqlPersistence` and `Games:EnableExpiryWorker` are true. It remains off by default. Interval and batch size use bounded technical configuration values; neither flag was enabled in the running application.
- Added DI tests for default-off behavior and explicit registration when both flags are set. The test composes service descriptors only; it does not start the worker, open a SQL connection, or run SQL.
- `M5-MS08-T06` / `T09` remain partial and unaccepted: live SQL reads/starts, schema approvals, and isolated SQL verification are still blocked.

## Current API surface and known remaining API gaps

The inventory below is source inspection, not a declaration that every path is production-ready.

| Module | Present in this checkout | Remaining work / limits |
|---|---|---|
| M1 IdentitySocial | Registration/OTP/login/password-reset controller, profile, logout, trusted-session auth | Social/follow/block/settings/account deletion surfaces remain incomplete or decision/schema dependent. PR #287 has overlapping identity error-contract changes; do not duplicate before review/merge. Production secret, mail delivery, and role provisioning are not verified. |
| M2 TravelPlanning | Trip-plan and stop CRUD, suggested plans, copy, route preview, place search/picker, weather, AI endpoints | ADM-07 rankings and eligible completion evidence remain pending. Provider credentials/live calls, SQL persistence and cross-module schema approval are not verified. |
| M3 TripSafety | Live trip actions, invitation and consent, hazards, memories, safe check-in, notifications, Admin safety/notification APIs, SignalR | SQL/runtime worker activation and live push/media integrations are unverified. Route-scoped hazard delivery, expiry, privacy and consent/revocation need end-to-end durable verification. Do not activate SQL or providers in this session. |
| M4 HunterPlaces | Application/domain capture-session, ownership and status foundations | No Hunter HTTP controller is present. Production M1-to-Hunter identity mapping, submission/review endpoint contracts, durable submission workflow and protected media remain pending. |
| M5 Games | Application/domain game draft, review, attempt, proof and moderation workflows; M5 DI batch added here | No Games HTTP controller is present. Approved route/auth contracts, durable end-to-end attempt/proof/review integration, one-winner enforcement across durable transactions, and 30-day archive processing still require verification/approval. Expiry must retain creator My Games history and DB rows. |
| M6 CoinsWallet | Wallet/transaction reads, voucher request and Admin voucher/coin controllers | SQL voucher runtime and reward writer/outbox are gated. Approve-time debit, reservation concurrency, QR issuance/retry, and end-to-end durability require isolated approved SQL verification; no database was used. OCR intake remains optional. |
| M7 HomeAdsAnalytics | Active ads, Admin advertisement CRUD/status, image upload lifecycle | Campaign targeting/relationships and analytics ingestion/reporting are partial or proposed. Admin role storage, SQL/restart behavior and live Cloudinary/provider integration are not production-verified. |

## Open PR overlap review

GitHub connector inventory showed these open PRs during the session:

- **#287** — M1 identity API error and route contracts; overlaps M1 identity files, not the M5 DI files changed here. Kept its work out of this patch.
- **#280–#286** — an M2 task series, including provider configuration and QA; no M2 files changed here.
- **#274** — M1 password-reset migration; migration work was not duplicated or run.
- **#279** — M7 SQL persistence design proposal; no M7 files changed.
- **#256** — M5 isolated proof-recovery foundation; its listed changed files do not overlap the files changed here. Its SQL test/database instructions were not executed.

PR review/merge status is external to this local branch; no PR was modified.

## Files changed

- `src/Wayhunter.Api/Program.cs`
- `src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs` (M2 Nearby client now disables redirects)
- `tests/Wayhunter.UnitTests/Modules/Games/M5GamesApplicationDiRegistrationTests.cs` (new)
- `src/Wayhunter.Application/Modules/Games/Interfaces/IExpiredGameArchiveStore.cs` (new)
- `src/Wayhunter.Application/Modules/Games/Services/ArchiveExpiredGamesService.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/InMemoryGameSubmissionStore.Expiry.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameRepository.cs`
- `src/Wayhunter.Api/BackgroundServices/GameExpiryHostedService.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/Games/ArchiveExpiredGamesServiceTests.cs` (new)
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GoogleNearbyPlacesRedirectHttpTests.cs` (new)
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GeminiRedirectHttpTests.cs` (new)
- `WAYHUNTER_BACKEND_COMPLETION_REPORT.md` (this report)

## Test evidence

- Baseline `dotnet build Wayhunter.slnx --no-restore` — passed before implementation: 0 warnings, 0 errors.
- Final `dotnet build Wayhunter.slnx --no-restore` — passed, 0 warnings and 0 errors.
- `ArchiveExpiredGamesServiceTests` and `M5GamesApplicationDiRegistrationTests` — focused tests passed, 6 total.
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --no-restore` — passed, 3,262/3,262.
- `dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName!~Sql"` — passed, 197/197 with `Logging__LogLevel__Default=None` and `Logging__EventLog__LogLevel__Default=None`. SQL/provider execution opt-ins remain excluded. Environment-gated SQL suites can call `EnsureCreated`; those suites were not run. Provider tests in the non-SQL set use test doubles/configuration; no live provider was activated.
- The first integration attempt without logging overrides had 16 test failures where restricted Windows Event Log writes masked HTTP test errors, plus a SignalR timeout. Re-running the same 197 tests with `Logging__LogLevel__Default=None` and `Logging__EventLog__LogLevel__Default=None` passed all tests. No source changes were made for this environment logging issue.
- `git diff --check` — passed on tracked modifications; new files were reviewed for whitespace issues.

## SQL/provider blockers and required approvals

- Shared schema and migration readiness documents still mark approvals pending/hold. No SQL migration, isolated SQL test database, or live database operation was run.
- M2–M7 durable multi-module behavior, SQL concurrency/retry, migration apply/rollback and restart persistence are not verified by the passing in-memory/non-SQL tests.
- M4 trusted identity mapping and submission eligibility must be approved before registering the proposed resolver for production or exposing capture routes.
- M5 route/auth contracts, durable completion and archive behavior need module/API owner review; ensure 30-day expiry archives and retains records rather than deleting them.
- M6 needs an approved isolated SQL verification setup for request reservation, approve-time debit exactly once, rejection release, and controlled voucher issue retry. No automatic refund behavior is introduced.
- M7 needs DB Coordinator/schema sign-off and explicit approval before activating SQL or external image storage.
- Flutter/mobile compatibility review and normal repository approval/checks remain required for PR #287.

## Next implementation batch

1. **M2 (first candidate):** Reconcile PRs #280–#286 after their review/merge state changes, then implement the highest-priority independent approved task from the remaining ADM-07/ranking or eligible completion evidence work. Those contracts and durable dependencies are currently pending; do not add routes until their exact API contract is approved.
2. **M1:** Continue only after the overlapping PR #287 and #274 are resolved and any remaining social/settings/deletion behavior has approved contracts and schema. Do not invent identity or error routes.
3. **M3:** Keep SQL and provider execution opt-in. Hazard durable route-scoped processing and Admin verification require approved M2/M3 geometry, thresholds, role claims, schema and consent/revocation behavior before additional activation work.
4. **M4:** Obtain approved trusted identity mapping, eligibility/status matrix, reader and HTTP submission/review contract before adding API wiring.
5. **M6/M7:** No independent contract-approved API gap was identified in this continuation pass. Proceed with isolated durable SQL verification only after DB Coordinator/schema approval; PR #279 overlaps M7 SQL work.
6. **M5:** Existing implementation remains partial and disabled by default. Continue only after the applicable M5 schema/route/auth approvals and isolated SQL verification authorization; do not activate the expiry worker.

## Continuation pass (2026-11-21)

- Reviewed this report, the existing working-tree changes, active SecondBrain rules, module readiness/evidence documents, and open PR overlap for M1, M2, M3, M4, M6 and M7. No new source/API batch was applied: every remaining candidate found either lacks an approved contract/schema/runtime gate or overlaps an open PR. Adding implementation now would invent behavior or duplicate pending work.
- Explicitly did not repeat the M5 DI wiring, archive service/repositories, disabled-by-default expiry worker, or their regression tests. Existing M5 working-tree changes were preserved.
- M3 hazard Admin routes currently rely on Admin role authorization while trusted production Admin-role resolution remains gated; do not weaken authorization or register an unapproved role reader to make them callable.
- Current verification: `dotnet build Wayhunter.slnx --no-restore` passed with 0 warnings and 0 errors; full unit suite passed 3,262/3,262; filtered integration command `dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName!~Sql"` with both logging overrides passed 197/197. The `Sql`-named suites were excluded; provider opt-in suites were not activated. `git diff --check` passed.
- No additional files were changed in this continuation pass beyond this report update. Existing untracked audit files and `tmp/` were left untouched. No migrations, live SQL, commits, pushes or merges were performed.
- Backend remains **PARTIAL**: M1 social/identity extensions, M2 pending ranking/completion evidence, M3 durable hazard/runtime behavior, M4 capture HTTP/auth integration, M6 durable SQL/reward integration, and M7 campaign/analytics and SQL/provider behavior still need their listed approvals and acceptance evidence.

## Integration unblock review (2026-11-21)

- Added [WAYHUNTER_BACKEND_INTEGRATION_UNBLOCKING.md](WAYHUNTER_BACKEND_INTEGRATION_UNBLOCKING.md), documenting current open PR metadata, exact M2 stack order, changed-file intersections, M1 mobile compatibility checks, readiness, required decision owners, and the next integration sequence.
- Verified PR #280–#286 form a dependent stack in order `#280 → #281 → #282 → #283 → #284 → #285 → #286`; #280 is draft, the rest are open/unmerged. #282 and #284 both touch `NearbyPlacesHttpTests.cs`. The PR descriptions report a build, 3,269 unit tests and 46 TravelPlanning integration tests on the final stack; SQL retry/concurrency and live-provider acceptance are still separate.
- PR #287 remains open/unmerged with Flutter/mobile compatibility explicitly pending. It formalizes structured error bodies and correlation IDs and confirms `/api/mobile/auth/logout`; no exact source/test file overlap was found with current local M5 changes. PRs #274, #279 and #256 remain open. #274 has shared snapshot/migration concerns; #279 is a design proposal; #256 explicitly says do not merge pending shared-schema and cross-module decisions.
- Exact path intersection review found no overlap between current local source/test files and PRs #256, #274, #279–#287. This does not remove conceptual M5 proof/schema coupling with #256 or shared migration concerns with #274.
- The unblocking document separates existing approved product decisions from unresolved runtime/schema decisions. In particular, the canonical `ACTIVE_RULES.md` voucher debit-at-Admin-approval rule prevails over conflicting older draft fulfillment text; no business contract was changed here.
- Existing M5 implementation/tests remain unchanged. No SQL, providers, merges, or production activation were performed. Backend remains **PARTIAL**.

## M2 implementation continuation (2026-11-22)

- Read both completion/integration reports and re-inspected the preserved M5 diffs/untracked implementation files. No M5 files were changed.
- Re-fetched PR #280–#286 metadata and patches. All remain open/unmerged; #280 remains draft. Patch comparison confirms #280 is the sole production-code change in the stack (`EfTripPlanRepository.RemoveStopWithConcurrencyAsync` plus its unit test). #281–#286 add tests/QA evidence or documentation and no application/infrastructure feature implementation. No PR was merged, cherry-picked, or copied into this branch.
- #280 wraps the existing stop-delete/reorder SQL transaction in the SQL Server execution strategy while retaining the parent RowVersion guard. Its new test injects a transient exception during first connection opening and stops before any SQL connection opens; it verifies strategy retry is active, not SQL transaction/reorder correctness. If commit acknowledgement is ambiguous after a successful commit, retrying with the original RowVersion returns concurrency conflict. This avoids duplicate deletion, but owner/reviewer must confirm that conflict-and-client-refresh behavior is the intended safe recovery result. SQL transaction/retry acceptance remains unverified.
- #281–#286 patches match behavior already implemented in this checkout and add regression/QA evidence (route fallback; copy privacy/immutability; AI output safety; nearby/weather fallback; provider failure matrix; provider configuration guide). Their test/code delta is not duplicated. #282 and #284 both modify `NearbyPlacesHttpTests.cs`; integrate cumulatively in the established stack order.
- Inspected the adjacent M6 voucher approval implementation while checking for an independent safe fallback. `SqlSampleVoucherAdminStore.ExecuteDecisionAsync` debits posted Coins and consumes the reservation in the Admin approval transaction; rejection releases the reservation; issue remains a separate flow. This matches current `docs/second-brain/ACTIVE_RULES.md`; the conflicting older draft wording is documentation drift, not a reason to alter behavior in this batch. Other previously identified M1/M4/M5/M7 work remains subject to its PR or contract/schema gates in the integration-unblocking report.
- **No new source implementation was made:** the only M2 runtime fix is already in open PR #280, and the other M2 stack changes are their authors’ tests/docs. Applying equivalent changes here would duplicate pending PR work; modifying the same repository method independently would conflict with #280. The safe next action is owner/reviewer resolution of #280’s ambiguous-commit/conflict response and completion of approved SQL retry/concurrency validation in the authorized isolated environment, followed by review/integration of #281–#286 in order. No new independent approved API behavior is available until then.
- Verification on this branch: `dotnet build Wayhunter.slnx --no-restore` passed (0 warnings, 0 errors); full unit suite passed 3,262/3,262; filtered non-SQL integration suite passed 197/197 with `Logging__LogLevel__Default=None`, `Logging__EventLog__LogLevel__Default=None`, and `FullyQualifiedName!~Sql`. SQL opt-in test fixtures were inspected; they are named `*Sql*`, gated on test SQL connection settings, and were excluded. Provider tests in the included suite use test doubles/configuration; no external provider was activated. `git diff --check` and whitespace checks on both reports passed.
- Working tree is preserved on `task/backend-m1-m7-completion`; no migration, live SQL, production provider, worker activation, commit, push, merge, stash, reset, cleanup, or worktree modification occurred. Backend completion remains **PARTIAL**.

## Master completion continuation (2026-11-22)

### Implemented safe M2 code batch

- **M2-MS08 Nearby and M2-MS07 Gemini provider credential redirect protection — PARTIAL pending live-provider acceptance.** `GoogleNearbyPlacesProvider` and `GeminiTripGenerationProvider` send server-only keys in `X-Goog-Api-Key` / `x-goog-api-key`, but their typed clients used default redirect-following handlers. This was inconsistent with the Routes and Place Picker clients and repository secret-safety rules. Updated `src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs` to configure `AllowAutoRedirect = false` for both clients. Redirect responses now follow each provider's existing failure path without forwarding the key to the redirect target.
- Added `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GoogleNearbyPlacesRedirectHttpTests.cs` and `GeminiRedirectHttpTests.cs`. Each uses a local loopback server and dummy key, verifies the initial credential-bearing request, then asserts that the redirect destination receives no request/key. No real Google/Gemini endpoint or credential was used. No route, DTO or business rule changed; the new file paths do not overlap PRs #280–#286.
- The matching Routes redirect guard/test already existed, and Place Picker and Weather clients also already disable redirects. The Nearby/Gemini fixes were independent of the open M2 stack and did not alter existing M5 work.

### PR #280–#287 status and readiness

Live PR metadata checked 2026-11-22:

| PR / task | Status | Current decision |
|---|---|---|
| #280 / M2-MS02-T06 Retry-aware stop removal | **BLOCKED — review/acceptance**; open, draft; base `develop` at `e3b0e93` | The code wraps the atomic delete/reorder in SQL Server's execution strategy and retains the row-version guard. Its new test simulates a first connection-open timeout and deliberately stops before connecting; no transaction/reorder SQL acceptance. On ambiguous committed success, retry with the old row version becomes a 409 conflict, which prevents duplicate deletion. Confirm this refresh/conflict result with the task owner and validate on the authorized isolated SQL target before acceptance. |
| #281 / M2-MS04-T03 Route fallback QA | **BLOCKED BY STACK**; open; based on #280 head | Tests/evidence only; integrate after #280 and preserve authored QA semantics. |
| #282 / M2-MS05-T06 Copy privacy QA | **BLOCKED BY STACK**; open; based on #281 head | Tests/evidence only; touches `NearbyPlacesHttpTests.cs`. |
| #283 / M2-MS07-T07 AI output/draft safety QA | **BLOCKED BY STACK**; open; based on #282 head | Tests/evidence only. |
| #284 / M2-MS08-T07 Nearby/weather QA | **BLOCKED BY STACK**; open; based on #283 head | Tests/evidence only; also touches `NearbyPlacesHttpTests.cs`. Integrate cumulatively with #282. |
| #285 / M2-MS10-T02 Provider failure matrix | **BLOCKED BY STACK**; open; based on #284 head | QA evidence only; live providers not tested. |
| #286 / M2-MS10-T05 Provider guide/handover | **BLOCKED BY STACK**; open; based on #285 head | Documentation only; guide explicitly does not activate providers. |
| #287 / M1-MS01-T08 identity errors/routes | **NEEDS CLIENT REVIEW**; open, non-draft; base `develop` at `e3b0e93` | Backend diff/test scope is clear; obtain Flutter confirmation for structured error shape, correlation header, 401/403 behavior and logout path before final acceptance. |

Recommended stack sequence remains #280 → #281 → #282 → #283 → #284 → #285 → #286; no PR was merged. #282 and #284 both change `NearbyPlacesHttpTests.cs`. No exact changed-file overlap exists between #280–#287 and the newly changed M2 files. Existing PR reports' 3,269-unit/46-TravelPlanning-test results are PR-reported, not rerun on their combined head.

### Module/task readiness update

| Module/task slice | Status | Evidence / blocker |
|---|---|---|
| M1-MS01-T08 identity error contract (#287) | **PARTIAL** | Open PR and backend tests; Flutter compatibility review/checks pending. |
| M2-MS02-T06 stop-removal retry (#280) | **PARTIAL / BLOCKED acceptance** | PR supplies the implementation; no SQL execution allowed here and no actual SQL transaction/retry verification in its added test. |
| M2-MS04-T03, MS05-T06, MS07-T07, MS08-T07, MS10-T02, MS10-T05 (#281–#286) | **PARTIAL / BLOCKED BY STACK** | Open dependent tests/QA/docs; review and integrate in order, no provider activation. |
| M2-MS08 Nearby credential redirect behavior | **PARTIAL** | Runtime handler fix and local HTTP regression verified; live Google verification remains disabled. Exact leaf task ID is not available in the local task register. |
| M2-MS07 Gemini credential redirect behavior | **PARTIAL** | Runtime handler fix and local HTTP regression verified; live Gemini verification remains disabled. |
| M3 TripStop/TripMemory delete race | **BLOCKED** | Local M3 coordination docs require M2 delete-side review, approved physical FK/migration, and both SQL race orders. No SQL run or constraint-specific behavior was guessed. |
| M4–M7 remaining integrations | **PARTIAL/BLOCKED** | Existing M4 trusted account reader/HTTP contracts, M5 Phase B/C/schema, M6 outbox/shared schema, and M7 campaign/analytics contracts remain gated as described above. Existing M5 expiry worker remains disabled. |

The local SecondBrain overlay contains `ACTIVE_RULES.md` and `CONTEXT.md` but not the full canonical API/Data task register. The configured `wayhunter_vault` MCP server was not available in the tool inventory/resource listing this session, so no exhaustive canonical-task denominator or completion percentage is claimed. Use the local task evidence as a partial inventory until the authoritative register is supplied/accessible.

### Verification for this batch

- Focused redirect integration tests: passed 2/2.
- `dotnet build Wayhunter.slnx --no-restore`: passed, 0 errors; two existing `xUnit2031` analyzer warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs` lines 152 and 154 surfaced during rebuild. They are unrelated to this M2 change.
- Full unit suite: passed 3,262/3,262.
- Filtered integration suite: passed 199/199 with logging overrides and `FullyQualifiedName!~Sql`. SQL fixtures were inspected; SQL-named, environment-gated suites were excluded. The included tests use local fakes/configuration; no external provider was activated.
- `git diff --check` and whitespace checks passed.
- Phase 0: branch remains `task/backend-m1-m7-completion`; `HEAD` and cached `origin/develop` are both `e3b0e93`. Existing M5 modifications, audit documents, `tmp/`, and all listed worktrees were preserved. No branch switch or worktree modification occurred. The separate remote branch `task/m2-ms02-t06-sql-retry-fix-20261121` has a large unrelated diff against cached `origin/develop`; do not use it as a clean integration base without an explicit history/rebase review.

### Exact next actionable task

1. M2 owner/reviewer: accept or request changes to #280's ambiguous-commit response (old RowVersion retry returns 409) and review its strategy-level test limitations. DB Coordinator: authorize the disposable SQL target and exact schema only if SQL acceptance is to proceed. Do not run SQL before that authorization.
2. Meanwhile, continue safe non-SQL work by selecting an independently approved task from the authoritative API/Data register once available; do not duplicate #281–#286 or invent M4/M5 routes. The M2 Nearby and Gemini redirect fixes are complete as code/test slices, pending live-provider acceptance.

## Change preservation and M2 stop-removal decision review (2026-11-22)

### Clean file separation and commit plan — not staged or committed

| Review group | Exact source/test paths | Current state |
|---|---|---|
| M2 provider credential redirect fix | `src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs`; `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GoogleNearbyPlacesRedirectHttpTests.cs`; `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GeminiRedirectHttpTests.cs` | Nearby/Gemini set `AllowAutoRedirect=false`; two local loopback regression tests. |
| M5 DI/archive/expiry work (pre-existing; preserved) | `src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs`; `src/Wayhunter.Api/Program.cs`; `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameRepository.cs`; `src/Wayhunter.Api/BackgroundServices/GameExpiryHostedService.cs`; `src/Wayhunter.Application/Modules/Games/Interfaces/IExpiredGameArchiveStore.cs`; `src/Wayhunter.Application/Modules/Games/Services/ArchiveExpiredGamesService.cs`; `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/InMemoryGameSubmissionStore.Expiry.cs`; `tests/Wayhunter.UnitTests/Modules/Games/ArchiveExpiredGamesServiceTests.cs`; `tests/Wayhunter.UnitTests/Modules/Games/M5GamesApplicationDiRegistrationTests.cs` | Existing M5 work untouched; expiry worker remains dual-flag opt-in and off. |
| Reports | `WAYHUNTER_BACKEND_COMPLETION_REPORT.md`; `WAYHUNTER_BACKEND_INTEGRATION_UNBLOCKING.md` | Cross-cutting docs updated; keep separate from code commits. |
| Existing audit/untracked material | `WAYHUNTER_LOCAL_BACKEND_MASTER_AUDIT.md`; `WAYHUNTER_LOCAL_SOURCE_COVERAGE.md`; `demiltan-m6-backend-review.txt`; `tmp/` | Preserved, unstaged and unmodified. No worktree was opened for writes. |

Recommended future commit split after review: (1) `fix(m2): prevent provider key forwarding across redirects` containing only the M2 extension and two test files; (2) a separate M5 archive/expiry/DI commit containing only the M5 paths above; (3) report update as a documentation-only commit. **Nothing is staged or committed.** Existing `Program.cs` change is M5 DI only. `GamesServiceCollectionExtensions.cs` and `SqlGameRepository.cs` are M5 only.

### PR #280–#287 duplicate-path check

Current GitHub status checked 2026-11-22: #280 is open/draft; #281–#286 are open dependent branches; #287 is open. Changed-file list intersections against every current M2/M5 source and test path above are empty. #280 changes `EfTripPlanRepository.cs` and its unit test; #281–#286 change QA tests/docs (including the shared `NearbyPlacesHttpTests.cs` in #282/#284); #287 changes IdentitySocial only. The new M2 tests are in separate files and no open PR patch is copied. The existing M5 paths are preserved and are not in those PRs.

### #280 and approved TripStop concurrency contract

Evidence: local `RemoveTripStopController` maps `TripPlanConcurrencyException` to HTTP 409 with `TRIP_PLAN_CONCURRENCY_CONFLICT`; the M2 endpoint inventory lists 409 for `DELETE /{tripPlanId}/stops/{stopId}` and `If-Match`; `RemoveTripStopService` requires an 8-byte expected row version in SQL mode; `EfTripPlanRepository` conditionally updates the parent row by Id, OwnerUserId, RowVersion and Draft status before deleting/reordering. The response on success is an updated plan with its new `RowVersion`. No idempotency key or durable operation-result record is part of this endpoint contract.

PR #280 moves the existing transaction into SQL Server's execution strategy and keeps that parent row-version check. Its added test injects a first connection-opening timeout and stops before SQL opens; it proves execution-strategy retry activation only. The PR comment explicitly expects a replay with the old row version to fail the guard.

| Scenario | Recommended/existing observable behavior | State guarantee / evidence |
|---|---|---|
| Successful delete, response lost between server/client; client retries same request and old `If-Match` | Retry returns HTTP 409 `TRIP_PLAN_CONCURRENCY_CONFLICT`; client GETs the plan and sees the committed deletion and latest `RowVersion`. Do not return a second 200 based only on the stop being absent: without a request key, absence could be another operation. | Logical state is idempotent (one stop removal); response is not replay-idempotent. Follows current stale-row-version contract. |
| SQL commit succeeded but commit acknowledgement to the API was lost; execution strategy retries internally | Retry with the original row version fails the guarded parent update and surfaces the same 409; caller reloads to determine whether the first attempt committed. | No duplicate delete/reorder. The state can be committed even though the API reports conflict. This is the safe current contract; no stronger success replay is supported without a new idempotency contract and durable request record. |
| Concurrent request changed parent row version before this delete's guard | HTTP 409 `TRIP_PLAN_CONCURRENCY_CONFLICT`; do not delete or reorder anything. Client reloads and may submit a fresh request with the new token. | Conditional parent update is the serialization point. |
| Genuine command failure before commit (delete/order update, constraint or exhausted provider failure) | Roll back the full transaction and return a server failure; never map a provider/constraint failure to concurrency 409 unless it is the approved exact TripMemory FK conflict. Error-body shape is not standardized by this M2 endpoint. | Parent row version, target stop and all stop order indexes remain as before. A 5xx sanitized envelope is recommended but requires API-owner approval/implementation; current controller does not catch raw `SqlException`. |

Do not add an idempotency key or convert the post-commit 409 into 200 without owner approval, schema, and tests. A true replay-success contract needs a durable operation ID/result bound to the authenticated owner, trip, stop and request; none is approved here.

### Test evidence boundaries and SQL acceptance plan (prepared only; not executed)

| Case | Can verify without SQL | Required isolated SQL acceptance and assertions |
|---|---|---|
| Input/owner/stop checks and stale 8-byte token handling | Existing `RemoveTripStopServiceTests`; can assert `TripPlanConcurrencyException`. Add/retain HTTP test mapping exception to 409/code if controller coverage is needed. | Not applicable for pure service mapping. |
| Execution strategy retries a transient connection-open failure | Unit test with `SqlServerRetryingExecutionStrategy` + `DbConnectionInterceptor`, throwing before a real connection opens (as #280 does). This does not prove transaction outcome. | Execute same scenario against disposable SQL: one success, exactly one stop removed, continuous indexes, refreshed version. |
| Successful removal and reordered output | In-memory service test can verify basic order; cannot prove raw SQL/repository atomicity or SQL rowversion. | Seed trip with 3 stops; remove middle stop using current RowVersion; assert success DTO has 2 ordered stops `[0,1]`, deleted stop absent, database matches DTO, parent RowVersion changed. |
| Client loses HTTP response after successful operation and retries stale `If-Match` | A controller fake can test stale-token 409, but not that prior operation committed. | Make first API result succeed but discard it; repeat exact request/token. Assert 409/code, then GET proves one deletion, contiguous order and changed version. |
| Ambiguous database commit acknowledgement | No in-memory/unit test can prove database committed before acknowledgement loss. | Use an authorized disposable SQL fault-injection proxy/harness that drops the commit response after server commit. Assert first call's internal retry ends in expected 409 (or other explicitly approved result), exactly one stop removal, correct indexes, and a fresh GET reveals committed state. Record whether injected failure reached the commit-ack boundary. |
| Concurrent RowVersion winner/loser | Unit test can verify service/controller maps a simulated concurrency exception to 409 only. | Two SQL contexts read the same plan/token and race competing edits/removals. Assert one winner, one 409 loser, no lost update, one deletion only, contiguous order, and no partial state. Repeat with concurrent TripMemory creation/deletion only after M3 schema/lock approval. |
| Genuine failure during delete/reorder before commit | A fake can assert exception propagation but cannot prove SQL rollback. | Inject a failure after delete or between reorder phases, before commit. Assert transaction rollback restores target stop, original indexes and original RowVersion; assert response is 5xx, not concurrency 409, and response/log contain no SQL details/secrets. |
| TripMemory FK conflict on deleting referenced stop | Can test an application exception/controller mapping once owners approve its stable type/code. | With approved FK and tombstoned/active memory fixture, delete referenced stop; assert only exact FK maps to stable sanitized 409, transaction rollback preserves stop/order/ETag. Verify both M2 delete-first and M3 memory-create-first race orders. This remains DECISION_REQUIRED in `M3_POST_MERGE_SQL_COORDINATION.md`; do not implement translation or execute SQL yet. |

SQL test run remains unauthorized and has not occurred. Required approvals before execution: DB Coordinator confirms disposable DB/connection, bootstrap schema and cleanup scope; M2 owner accepts retry/replay contract and test result; M3 owner + M2 owner + DB Coordinator approve the TripMemory FK, exact constraint mapping, compatible lock order and two race cases. No shared migration is authorized by this plan.

### Current blockers and next executable task

1. **M2 owner/reviewer decision:** record that same-keyless retries with the old `If-Match` return 409 after an ambiguous committed delete and require client refresh. This matches the existing concurrency contract; the owner must confirm before #280 is accepted.
2. **DB Coordinator approval:** provide disposable SQL test target/schema and authorize the isolated cases above. Until then, execute no SQL.
3. **M3/M2 decision:** approve or defer exact referenced-TripStop FK conflict handling and lock order. This is not part of #280's retry change and is explicitly still a decision gate.
4. After those approvals, run the prepared SQL suite on the isolated target, verify #280's final head, and review the remaining stack #281–#286 in order. Do not merge automatically.

No new source change was required by this decision review. M2 redirect fixes and M5 work remain separated and unchanged; backend is not declared complete.

## M5 verified-draft DI and fail-closed media batch (2026-11-22)

- Review found the existing `VerifiedGameDraftService` and `GameClueMediaOwnershipService` were not registered by `AddGamesApplicationServices`, and no `IGameClueMediaAccessVerifier` default existed. Added `UnavailableGameClueMediaAccessVerifier`, which returns false for media assets and honors cancellation, and registered it with the two application services using `TryAddScoped`. Text-only drafts use the existing in-memory `IGameRepository`; a media-bearing draft cannot pass ownership validation without an explicitly supplied verifier/provider.
- Added DI regression coverage that resolves both services, confirms the default verifier denies an unknown media ID, allows a text-only draft through the verified-draft service, and rejects a media-bearing draft. No API routes, reward events, provider activation, SQL, or schema changes were added.
- Focused test command `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore --filter FullyQualifiedName~M5GamesApplicationDiRegistrationTests` passed 4/4. The test build surfaced two existing xUnit2031 analyzer warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs`; no production compiler errors.
- M4 HTTP implementation remains blocked: the checkout has capture-session handler foundations, but no submission/review service implementations, no concrete production `ITrustedIdentityAccountReader`, and no approved public route/DTO/status matrix. The MS04 identity mapping principle is documented, while final claim/error/FK/production DI decisions remain pending. No speculative M4 routes or resolver activation were added.

## Final implementation sprint checkpoint (2026-11-22)

- Added two required release-review artifacts: `WAYHUNTER_FINAL_API_ENDPOINT_MATRIX.md` (all discovered controller routes and controller-level auth/service/persistence/test state) and `WAYHUNTER_FINAL_BACKEND_COMPLETION_STATUS.md` (module status, implementation, PRs, SQL/provider gates, test evidence and next actions). Canonical 217-task percentages remain not computable because the authoritative task register is in the unavailable SecondBrain vault.
- Full solution build: PASS, 0 warnings/errors (`dotnet build Wayhunter.slnx --no-restore`). Full unit suite: PASS, **3,263/3,263**. Filtered non-SQL integration suite: PASS, **199/199** with `FullyQualifiedName!~Sql` and logging overrides. SQL test configuration was inspected: M3/M4/M5/M6 SQL fixtures use dedicated `WAYHUNTER_*_SQL_CONNECTION` opt-ins; no SQL variables were enabled, no SQL/provider suites ran, and no live provider was activated. Focused M5 DI suite: PASS, 4/4.
- Focused test compilation showed two pre-existing xUnit2031 warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs`; the standalone solution build completed with 0 warnings after builds were current. No compiler/test regressions remain.
- M4 route work remains blocked on the missing approved route/DTO/status matrix, missing concrete submission/review implementations and absent trusted account reader. M5 route work remains blocked on the missing approved public/admin API contract. No routes were invented. M5 Phase B/C HOLD remains and the expiry worker remains disabled.
- Exact new source/test change in this batch: `src/Wayhunter.Infrastructure/Integrations/Games/UnavailableGameClueMediaAccessVerifier.cs`; `src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs`; `tests/Wayhunter.UnitTests/Modules/Games/M5GamesApplicationDiRegistrationTests.cs`. No M2/M5 existing uncommitted implementation was removed or overwritten. No commit, push, merge, migration, live SQL, production provider activation or worktree modification occurred.

## M4/M5 route-contract and safe integration checkpoint (2026-11-22)

### Preservation and commit readiness

- Re-inspected branch, HEAD, tracked modifications and untracked paths. Branch remains `task/backend-m1-m7-completion`; HEAD and cached `origin/develop` remain `e3b0e9336e9f19003811f01649491c27fa3730a5`. No staging, commit, branch change, SQL, or provider activation occurred.
- M2 redirect protection is isolated to `TravelPlanningServiceCollectionExtensions.cs` and two new loopback redirect integration tests (`GoogleNearbyPlacesRedirectHttpTests.cs`, `GeminiRedirectHttpTests.cs`).
- Existing M5 DI/archive/expiry/media verification work remains isolated to Games DI, `Program.cs`, SQL/in-memory archive adapters, archive service/interface, disabled-by-default hosted worker, fail-closed media verifier, and two unit test files. Exact paths and future separate commit groupings are in `WAYHUNTER_M2_M5_SAFE_COMMIT_PLAN.md`.
- Existing audits, status exports, `tmp/`, and contributor worktrees were left untouched. No exact changed-file overlap was found with PRs #280–#287; #256 remains a proof/schema integration dependency, not code to duplicate.

### M4/M5 API contract checkpoint

- No M4 HunterPlaces or M5 Games HTTP controller exists in the current API controller tree. Neither has an approved public method/path/DTO/error matrix in available local rules. **No new M4/M5 HTTP routes are currently cleared for implementation.** This is a contract gate, not a claim that no application code exists.
- M4 contains `IHunterSubmissionService` and `IHunterReviewService` interfaces, DTOs, and capture/session/transition handlers; concrete submission/review services and production trusted-account reader registration were not found. Identity claim issuer, lifecycle lookup, media ownership/capture binding, status/error mapping and persistence/FK decisions remain unresolved. Do not trust client identity or expose guessed routes.
- M5 contains draft, submission, approval, moderation, attempt, proof, query and expiry/archive services/DTOs. No Games controller exists. Active rules approve Admin creation approval, publication, approval+30-day expiry, archive/retention, creator history, rejection of post-expiry play/completion/win, and once-only reward events. They do not approve route shapes, public response DTOs, role/error mappings or pagination. Proof routes remain Phase B/C HOLD under the replay/capture/SQL draft.
- Full proposed endpoints, DTO/service mappings, authorization, validation/test requirements and owner-specific decisions are in `WAYHUNTER_M4_M5_API_CONTRACT_APPROVAL.md`. No source code or tests changed in this checkpoint because no route contract was fully approved.

### Decisions required and next task

1. **Team Leader + M4/M5 owners:** approve or revise the proposed route/DTO/status/HTTP error matrices in the contract package.
2. **Team Leader + M1 owner:** finalize trusted principal issuer/claim and active/deleted/suspended account reader/DI contract before M4/M5 actor IDs are accepted.
3. **M4/media owner:** approve evidence ownership, capture eligibility/session binding, replay and retention rules.
4. **Team Leader + M4/M5/M6 owners:** resolve proof recovery/replay and reward event boundaries; keep Phase B/C HOLD until accepted.
5. **DB Coordinator:** separately approve M4/M5 schema/FK/transaction/outbox design and isolated SQL test target/fixtures. No database action was performed.
6. **Media/provider owner:** approve trusted media verifier/storage behavior. M5 default verifier remains fail-closed.

Next implementation task after those decisions: implement the approved M4 Hunter submission service and owner-scoped status query over a defined persistence boundary, then wire only approved routes with 401/403, cross-owner denial, evidence ownership, replay and transition-conflict tests. If route approval arrives before DB approval, use a test-only/fake store only if the owner accepts that interim scope; do not claim durable completion. M5 draft HTTP routes can follow approval via `VerifiedGameDraftService`, keeping the expiry worker disabled and proof routes held.

The previous build/test counts in this report are historical until the full suites are rerun against the current tree. Backend completion is not claimed.

### Verification rerun for this checkpoint

- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 warnings and 0 errors.
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-build --no-restore`: PASS, 3,263 passed, 0 failed, 0 skipped.
- SQL/provider-filtered integration: PASS, 199 passed, 0 failed, 0 skipped using `FullyQualifiedName!~Sql`. A first run without logging override failed due to test-host attempts to write to the Windows `.NET Runtime` Event Log; rerun with process-level `Logging__EventLog__LogLevel__Default=None` passed. No project logging config or test source was changed.
- SQL/provider opt-in review: integration sources show SQL suites gated on dedicated `WAYHUNTER_M*_TEST_SQL_CONNECTION` variables. They were excluded by the test filter; no SQL connection was set. No provider credentials or live providers were used.
- `git diff --check`: PASS after the report and planning documents were written.

## M5 draft adapter readiness batch (2026-11-22)

### Approval and exposure decision

- Reviewed the contract package, project rules, trusted identity services, and existing controller/error patterns. The prompt labels the four methods/paths proposed, and no separate Team Leader/M5 owner approval evidence for exact routes, DTO responses, error/status mapping, or DELETE idempotency was present. The routes remain unapproved.
- `TrustedIdentityResolutionService` already validates authenticated principal identity through `TrustedIdentityAccessContract`, resolves the account via `ITrustedIdentityAccountReader`, and requires an active verified account. It is suitable as a boundary in principle, but no concrete `ITrustedIdentityAccountReader` implementation is registered in production DI, so it is not currently usable as a production M5 resolver.
- `ApiErrorResponse` and `X-Correlation-ID` middleware exist, but existing controller behavior includes bare `Unauthorized()`/`Forbid()` responses and is not one confirmed M5 HTTP error contract. No status/error mapping was selected.
- Accordingly no controller/routes or DI registration were added. Added an internal, route-free adapter only. All four HTTP paths are verified 404/unmapped by an integration guard, and no public endpoint completion is claimed.

### Implemented internal adapter and tests

- `GameDraftApplicationAdapter` accepts a `ClaimsPrincipal`, derives actor identity only via `TrustedIdentityResolutionService`, and delegates create/update to `VerifiedGameDraftService`, owner read to `GameService`, and draft-only deletion to `GameDraftDeletionService`. It adds no API response DTO/status mapping and has no MVC attributes or DI registration.
- Existing validation continues to enforce title, positive duration, nonempty stages, positive unique step numbers, and a text or photo hint per stage. Text-only drafts pass; the existing default media verifier rejects unverified media. Existing application services enforce creator ownership and draft status; deletion semantics remain those of the service (missing resource raises `KeyNotFoundException`, only draft can delete). These are internal outcomes and do not imply approved HTTP error or idempotency semantics.
- Added `tests/Wayhunter.UnitTests/Modules/Games/GameDraftApplicationAdapterTests.cs` for active identity, missing/inactive identity, owner read/update/delete, cross-owner denial, valid lifecycle, invalid draft, and fail-closed media. Added `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftRoutesApprovalGateHttpTests.cs` to assert proposed POST/GET/PUT/DELETE paths all remain 404/unmapped.
- Added `src/Wayhunter.Api/Properties/AssemblyInfo.cs` granting unit-test access to the internal adapter; the adapter itself remains unregistered.

### Changed files for this batch

- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApplicationAdapter.cs`
- `src/Wayhunter.Api/Properties/AssemblyInfo.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/GameDraftApplicationAdapterTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftRoutesApprovalGateHttpTests.cs`
- `WAYHUNTER_M4_M5_API_CONTRACT_APPROVAL.md`
- `WAYHUNTER_FINAL_API_ENDPOINT_MATRIX.md`
- `WAYHUNTER_BACKEND_COMPLETION_REPORT.md`

### Decisions and next task

Team Leader + M5 owner must approve the four exact methods/paths, version, request/response DTOs, status/error behavior, cross-owner missing-vs-forbidden semantics, and DELETE retry/idempotency. Team Leader + M1 owner must wire a concrete trusted active-account reader and confirm claim/lifecycle contracts. API owner must resolve the consistent error envelope. DB Coordinator must decide whether this first public slice can be in-memory-only or requires SQL-backed durable drafts. No SQL/provider/proof/reward work was performed; Phase B/C remains HOLD and expiry worker remains disabled.

Next safe task: implement/register the approved `ITrustedIdentityAccountReader` only after M1 owner confirms its persistence/source contract, then implement the approved public route/controller and map identity/validation/ownership/missing-resource outcomes using the agreed error contract. Until then the adapter remains internal-only.

### Verification

- Focused unit suite: `GameDraftApplicationAdapterTests` passed 3/3.
- HTTP exposure guard: `M5DraftRoutesApprovalGateHttpTests` passed 1/1.
- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 warnings/errors.
- Full unit suite: PASS, 3,266 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration suite: PASS, 200 passed, 0 failed, 0 skipped using `FullyQualifiedName!~Sql` and process-level `Logging__EventLog__LogLevel__Default=None` to avoid the Windows Event Log permission issue. SQL connection variables were not set; SQL-named tests were excluded. No external providers were activated.
- `git diff --check`: PASS after the code, matrix and report edits.

## M1 trusted account reader candidate batch (2026-11-22)

### Implementation

- Confirmed `WAYHUNTER_M1_TRUSTED_ACCOUNT_READER_CANDIDATE.zip` was not present in the workspace/search locations. Rather than fabricate an archive, implemented the same small candidate directly against the checked-in M1 contracts.
- Added `src/Wayhunter.Api/Services/IdentitySocial/SqlTrustedIdentityAccountReader.cs`. It implements the existing `ITrustedIdentityAccountReader` using `WayhunterSharedDbContext.Users.AsNoTracking()`, accepts only a nonempty exact-D GUID representation (trimmed, then canonicalized), uses `SingleOrDefaultAsync` and the supplied cancellation token, propagates requested cancellation, and returns null on query/data-access/ambiguous-read failure. It does not decide account activity; `TrustedIdentityResolutionService` + `TrustedIdentityAccessContract` enforce active, verified, persisted identity match and no deletion request.
- The implementation is not registered in `IdentityPersistenceServiceCollectionExtensions`; no production DI activation or schema/migration change was made pending M1 owner approval.
- Added `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SqlTrustedIdentityAccountReaderTests.cs`: active account resolves; suspended account is rejected by the trusted resolver; missing and malformed/empty IDs return null; cancelled lookup throws cancellation; disposed/unavailable context fails closed; result is not tracked; principal identity, not an independent caller-provided ID, determines the resolver lookup.
- Local PR #287 metadata and changed filenames were checked on 2026-11-22. It remains open/unmerged and changes identity API contracts/controllers/session handlers and their tests; it does not overlap the new reader or test paths. No PR code was copied.

### Verification for this batch

- `dotnet restore Wayhunter.slnx`: PASS (all projects up to date).
- Focused `FullyQualifiedName~SqlTrustedIdentityAccountReaderTests`: PASS, 11/11; uses EF Core InMemory, not SQL Server.
- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 warnings/errors.
- Full unit suite: PASS, 3,277 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration suite: PASS, 200 passed, 0 failed, 0 skipped with `FullyQualifiedName!~Sql`; all dedicated M3/M4/M5/M6 SQL connection variables were verified unset before execution. Process-only Event Log logging override was used due Windows test-host permissions. No SQL/provider suite ran.
- `git diff --check`: PASS.

### Remaining M1 and M1–M7 blockers

- M1 owner approval is still required for this candidate’s authoritative `UserAccount.UserId` source, issuer/claim mapping and registration lifetime. Consequently the reader is not wired into production; M4/M5 trusted identity dependencies remain blocked.
- PR #287 remains open pending Flutter/mobile compatibility, required review and checks. The candidate has no file overlap with #287.
- PR #256 remains open and M5 proof-recovery/schema work remains Phase B/C HOLD. PR #274 remains open with identity migration/snapshot changes needing review; no migration was applied. PR #279 remains an M7 persistence proposal. PRs #280–#286 remain open in their dependent M2 chain; #282/#284 still overlap in `NearbyPlacesHttpTests.cs`, and #280 still needs owner/isolated SQL retry acceptance. None were merged, cherry-picked, or duplicated.
- M5 draft CRUD routes remain unmapped pending the documented Team Leader/M5/API/M1/DB decisions. M4 route/submission/media decisions, M3 SQL/provider validation, M6 durable SQL acceptance, and M7 persistence/business approvals remain as recorded in this report and the endpoint matrix. The canonical 217-task register remains unavailable, so overall exact completion percentage is UNKNOWN/not computable.
- No further production code was changed in other modules during this batch because the remaining identified work depends on unmerged PR review, missing route/business contracts, DB Coordinator approval, or external provider acceptance. This is not a completion claim.

### Exact next actions

1. M1 owner: review this read-only candidate and explicitly approve `Users` as the canonical account source plus claim mapping; only then register it scoped in `AddWayhunterIdentityPersistence` and add DI resolution/production composition tests.
2. Team Leader/M1/M5/API-mobile owners: approve the M5 draft route/error/replay/ETag contract in `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md`; do not map routes before sign-off.
3. DB Coordinator: decide durable Games draft storage and authorize only a specifically isolated SQL acceptance target; do not enable SQL flags or execute migrations here.
4. Continue M2 only after cumulative PR #280–#286 review/acceptance; do not duplicate their tests or run SQL.

## M4/M5 internal service correctness batch (2026-11-22)

### M1 reader preservation review

- Re-read `SqlTrustedIdentityAccountReader` and its focused tests. The reader remains read-only, `AsNoTracking`, strict canonical GUID keyed, cancellation-aware, and fail-closed on account-query failures. Active/verified/deletion checks remain delegated to `TrustedIdentityResolutionService` / `TrustedIdentityAccessContract`.
- Confirmed it remains absent from `IdentityPersistenceServiceCollectionExtensions`; no production registration was added. No reader code change was needed in this batch.

### M4 internal-service review

- Reviewed `HunterSubmissionStatusTransitionService`, `HunterCaptureSessionService`, authorized start/consume handlers, `HunterCaptureSubmissionOwnershipService`, the transition/session store contracts, and current in-memory/SQL stores.
- Existing internal checks align with the available rules: start/consume require authenticated trusted identity, owner access and Draft eligibility; session consumption uses backend time and enforces one-time use/expiry/ownership; status transitions use expected states and revoke unused sessions in the store transaction. Unit tests cover ownership, replay, state transitions and concurrency at the in-memory/test boundaries.
- No independent M4 production change was safe: public submission/review services remain unimplemented, and trusted identity registration, capture/media contract and durable production persistence require M1/M4/DB owner decisions. No M4 route was added.

### M5 approved application-service hardening

- `GameSubmissionService` previously gated submission on `UserAccount.Status == Active` only, despite its contract/comment requiring verified active users. Added a fail-closed eligibility check for Active status, non-null/non-default UTC `VerifiedAtUtc` not earlier than valid UTC `CreatedAtUtc`, and absent `DeletionRequestedAtUtc`. Invalid account snapshots are rejected before submission is sent to the store; error remains an internal `InvalidOperationException` and does not define an HTTP status.
- Added two `GameSubmissionServiceTests` regression cases using EF InMemory to model malformed persisted state: an Active row without verification timestamp and an Active row with a deletion-request timestamp. Both assert rejection and zero store calls. Existing M5 draft adapter/routes and proof/approval/reward boundaries were not changed. The new paths do not overlap the inspected PR #256 proof-recovery files.

### Changed code/test files

- `src/Wayhunter.Application/Modules/Games/Services/GameSubmissionService.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/GameSubmissionServiceTests.cs`
- `WAYHUNTER_BACKEND_COMPLETION_REPORT.md`

### Verification

- Focused `GameSubmissionServiceTests`: PASS, 14/14.
- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 warnings and 0 errors.
- Full unit suite: PASS, 3,279 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration suite: PASS, 200 passed, 0 failed, 0 skipped with `FullyQualifiedName!~Sql`. All dedicated M3/M4/M5/M6 SQL connection variables were checked unset. Windows Event Log logging was disabled for the test process only; no application configuration changed.
- `git diff --check`: PASS after report update.

### Remaining exact blockers and next actions

1. **M1 owner:** approve the account source/claim mapping for `SqlTrustedIdentityAccountReader`; only then add scoped identity DI registration and composition-root tests. Until then, no M4/M5 actor integration is production-ready.
2. **Team Leader + M4 owner + M1 owner:** approve trusted identity, media ownership/capture binding, submission/review transitions and exact M4 routes before implementing/registering M4 HTTP APIs.
3. **Team Leader + M5/API-mobile owner:** approve the four M5 draft routes, DTO/error/replay/ETag/deletion contract in `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md`; routes remain unmapped.
4. **DB Coordinator:** decide durable M5 draft storage and approve isolated SQL acceptance separately. No SQL/provider work was performed.
5. M5 proof recovery stays Phase B/C HOLD; expiry worker remains disabled. M2 #280–#286 remain a dependent unmerged stack with separate owner/SQL acceptance gates. No duplicate code was added.

No other M4/M5 changes were independently approved and safely implementable from the inspected contracts. M1–M7 completion percentages remain unavailable because the canonical evidence-classified task register is not present and SQL/provider acceptance is incomplete; no completion claim is made.

## M1 trusted identity and M5 Draft API integration checkpoint (2026-11-22)

### M1 identity verification

- Re-verified `SqlTrustedIdentityAccountReader` against `WayhunterSharedDbContext.Users` and the existing `ITrustedIdentityAccountReader` contract. It performs a read-only `AsNoTracking` lookup keyed by canonical, non-empty `UserAccount.UserId`; it does not infer account eligibility or accept caller-supplied actor identity.
- `TrustedIdentityAccessContract` validates an authenticated principal and rejects missing, duplicate, malformed, or conflicting `sub` / `NameIdentifier` identity claims before lookup. The authenticated bearer path validates signature, issuer, audience, expiry and required claims, then reloads persisted account/session state and checks active/verified status, ownership, deletion request, revocation and expiry before creating the server principal.
- Existing focused tests cover active verified resolution, anonymous/malformed/conflicting claims, missing/wrong-owner/pending/suspended accounts, cancellation, and persisted revoked/missing/mismatched sessions. No new identity test was required by this review.
- **Production reader remains unregistered.** The M1–M4 contract review is explicitly `PROPOSED — CROSS-MODULE REVIEW REQUIRED`; it does not authorize DI. Required M1 owner sign-off: confirm `Users.UserId` as the canonical lookup source, `sub` as authoritative issuer claim with server-generated `NameIdentifier` compatibility mapping, active/verified/deletion-state semantics, and scoped registration in `AddWayhunterIdentityPersistence`. Until then M4/M5 consumers must remain fail-closed/unavailable.

### M5 Draft API boundary

- Re-read `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md` and the internal `GameDraftApplicationAdapter`. The adapter is application-level only (no MVC route attributes and no public DI/controller mapping). It delegates to existing verified create/read/update/delete services and resolves account identity through the trusted resolver.
- The four proposed routes (POST/GET/PUT/DELETE `/api/mobile/games/drafts[/{gameId:guid}]`) still have no recorded Team Leader/M5/API-mobile approval. DTO wire casing/response DTO, status/error mapping, owner-resource concealment, DELETE retry behavior, and concurrency/revision semantics remain decisions, not approved behavior. Draft persistence also defaults to in-memory; durable storage requirements need DB Coordinator decision.
- No public route or new HTTP behavior was added. Existing fail-closed media verification and M5 submission verification remain unchanged. No Phase B/C proof, expiry worker, reward, SQL, or provider activation occurred.

### Verification for this checkpoint

- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 warnings, 0 errors.
- Full unit suite: PASS, 3,279 passed, 0 failed, 0 skipped.
- Non-SQL integration suite: PASS, 200 passed, 0 failed, using `FullyQualifiedName!~Sql`. Inspected opt-in connection variables (`WAYHUNTER_M4_TEST_SQL_CONNECTION`, M3 session/base/GPS/SafeCheckIn, M5, M6) were all unset before execution. Process-only Event Log suppression was applied for Windows test-host permissions; no provider or SQL test ran. Data Protection emitted DPAPI decrypt warnings from the local user key store, but all 200 tests passed.
- `git diff --check`: verified after report update.

### Remaining exact decisions

1. **M1 owner:** approve the authoritative reader source, JWT claim issuer/mapping, account eligibility rules, and scoped production DI registration.
2. **Team Leader + M5 owner + API/mobile owner:** approve exact four route paths, request/response DTOs, HTTP status/error envelope, missing-vs-forbidden behavior, deletion/retry semantics, and concurrency/revision behavior.
3. **DB Coordinator:** decide whether public drafts require durable SQL persistence and separately authorize isolated SQL acceptance; no SQL was run.

Next executable work after those approvals: add the scoped reader registration with composition-root resolution tests; then map and HTTP-test only the signed-off M5 Draft routes. Until approvals are recorded, the reader remains a candidate and M5 routes remain unmapped.

## M1/M5 integration decision register (2026-11-22)

### M1 production DI readiness

**Decision: NOT READY — do not register `SqlTrustedIdentityAccountReader` yet.** Source verifies `UserAccount.UserId` is the account key and M1-issued JWT `sub` is the server principal identity; `SessionBearerAuthenticationHandler` only creates the authenticated principal after token signature/issuer/audience/lifetime checks and `AuthenticatedSessionAuthorizationService` rechecks persisted account/session state including deletion request, revocation, and expiry. `TrustedIdentityResolutionService` adds persisted active/verified/non-deleted checks. However, `docs/task-evidence/M1_M4_TRUSTED_IDENTITY_OWNER_CONTRACT_REVIEW.md` explicitly leaves M1 owner confirmation and production registration unchecked. The gap is approval evidence, not an implementation/test failure.

**Exact M1 owner decision requested:** Confirm (1) `WayhunterSharedDbContext.Users.UserId` is the canonical reader source and exact-D nonempty GUID representation is required; (2) `sub` is authoritative and `NameIdentifier` is generated only by the validated session handler as a compatibility claim; (3) Active + verified + not deletion-requested is the eligibility rule, with pending/suspended/missing/revoked/expired rejected; and (4) `SqlTrustedIdentityAccountReader` may be registered scoped by `AddWayhunterIdentityPersistence`. After approval, add service-resolution and fail-closed composition tests. DB Coordinator approval is needed only if schema/FK/migration changes are proposed; this reader itself requires none.

### M5 Draft API v1 final decision table

All route and wire-level entries below remain **PROPOSALS** in `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md`; the approved M5 rules relevant here are server-side authorization, creator ownership, Admin approval before public play, and expiry archive/retention. No public routes are mapped.

| Area | Current proposal / implementation evidence | Approval needed before route mapping |
|---|---|---|
| Routes | `POST /api/mobile/games/drafts`; `GET /api/mobile/games/drafts/{gameId:guid}`; `PUT /api/mobile/games/drafts/{gameId:guid}`; `DELETE /api/mobile/games/drafts/{gameId:guid}` | Team Leader + M5/API-mobile owners approve exact paths/version |
| Request | Existing `CreateGameDraftDto`: `Title`, `Description`, `DurationMinutes`, `Stages`; stage fields `StepNumber`, `TextHint`, `PhotoMediaId`; PUT proposed as full replacement | Approve JSON naming/serializer policy and PUT replacement semantics |
| Response | Proposed dedicated DTO: `GameId`, title, description, duration, Draft status, ordered stage hints; no creator/internal IDs | Approve response fields and wire casing |
| Authentication | Proposed authenticated active verified M1 account; actor exclusively from validated principal through trusted resolver; no client actor ID | M1 owner approves reader and DI; M5 owner confirms Hunter/creator requirement |
| Ownership concealment | Internal services enforce creator ownership; proposal maps another owner's object to 404 | Team Leader/API owner chooses 404 concealment vs 403 |
| Errors | Proposed 400 invalid input/media, 401 unauthenticated/inactive, 403 reserved only if chosen policy exposes forbidden, 404 absent/concealed, 409 state/revision conflict; platform envelope is not uniform | API/mobile owner approves exact status/code/envelope mapping |
| Update | Draft-only complete replacement; media verifier fails closed; current SQL repository has rowversion but adapter has no revision token | Choose opaque ETag/If-Match + conflict behavior or explicit last-write-wins; define missing precondition behavior |
| Delete/retry | Draft-only physical removal proposed; repeated delete after success currently resolves missing resource (KeyNotFound) absent an idempotency layer | Approve delete of drafts vs soft-delete and whether retry returns 204 or 404; must not affect approved-game archive rule |
| Idempotency | Create has no idempotency key; update/delete have no public retry token | Decide create retry/deduplication and conditional update/delete behavior |
| Persistence | Default application store is singleton in-memory; optional SQL adapter is feature-gated; no SQL run | DB Coordinator + Team Leader decide whether in-memory is development-only or acceptable for initial release; authorize isolated SQL acceptance separately |

**Next safe action:** after M1 identity approval, register the reader scoped and test DI composition. In parallel, obtain the listed M5 route/API/mobile and DB decisions. Then implement and HTTP-test only the signed-off subset; keep route registration blocked until the decision record is updated.

## Approved M1 DI + M5 development HTTP API batch (2026-11-22)

This is the current M1/M5 status and supersedes the earlier checkpoint and decision register above where they state that the trusted reader is unregistered or M5 Draft routes remain unmapped in every environment.

### Implemented

- Registered `ITrustedIdentityAccountReader -> SqlTrustedIdentityAccountReader` and `TrustedIdentityResolutionService` as scoped services in `AddWayhunterIdentityPersistence`, matching the scoped `WayhunterSharedDbContext`. No migration or schema was changed. Production session authentication continues to validate the signed `sub`, account status, verified state, deletion request, session owner, revocation and expiry before producing an authenticated principal.
- Added `GameDraftsController` for the approved development routes: POST/GET/PUT/DELETE `/api/mobile/games/drafts[/{gameId:guid}]`. POST/PUT reuse `CreateGameDraftDto`; responses use `GameDraftResponseDto` and ordered stage projections, excluding creator and persistence internals. PUT is complete replacement per current service behavior.
- Added owner concealment in `GameDraftApplicationAdapter`: another creator's draft is normalized to not-found on read/update/delete. Active identity still comes exclusively from the trusted account resolver. Text-only drafts remain supported; existing media verifier still rejects unverified media.
- Added stable `ApiErrorResponse` errors for the draft API: 400 invalid input/media, 401 unauthenticated or rejected identity, 404 absent/non-owned draft, and 409 lifecycle or repository concurrency conflict. The M5 controller has a scoped authorization filter and does not change shared M1 challenge behavior. 403 is not emitted by these routes: account/session rejection is 401 and creator-resource concealment is 404.
- Added an MVC feature provider. Routes are discovered only when environment is Development, `Games:EnableDevelopmentDraftHttpApi=true`, `Identity:EnableSharedAuthentication=true`, and `Games:EnableSqlPersistence=false`. Default and Production configurations keep the routes unmapped even when the draft flag is set. No production SQL, expiry worker, proof, moderation, or rewards are activated.
- Retry/revision behavior follows current service capabilities and does not invent protocol: a repeated delete after success returns 404; POST has no idempotency key; PUT has no client ETag/revision and can overwrite a stale full representation in in-memory mode; SQL repository concurrency exceptions map to 409 if reached. These are release gaps for durable/public use.

### Changed implementation and test files

- `src/Wayhunter.Api/Extensions/IdentityPersistenceServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Program.cs`
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApplicationAdapter.cs`
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftsController.cs` (new)
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApiControllerFeatureProvider.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityPersistenceRegistrationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/AuthenticatedSessionAuthorizationServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/TrustedIdentityResolutionServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/GameDraftApplicationAdapterTests.cs`
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftDevelopmentApiHttpTests.cs` (new; includes non-Draft state 409 coverage)
- `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md` (records Project Lead development-scope approval and remaining production gates)
- `WAYHUNTER_BACKEND_COMPLETION_REPORT.md`
- `WAYHUNTER_FINAL_API_ENDPOINT_MATRIX.md`
- `WAYHUNTER_BACKEND_FINAL_IMPLEMENTATION_HANDOFF.md`

Pre-existing M2 redirect, M5 archive/expiry/worker, and unrelated audit/tmp changes were preserved. `Program.cs` also contains existing M5 DI edits in this working tree; they were not removed.

### Verification

- `dotnet restore Wayhunter.slnx`: PASS, all projects up to date.
- `dotnet build Wayhunter.slnx --no-restore`: PASS, 0 errors. The final clean build reported two existing xUnit2031 warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs` lines 152 and 154; no new warning is attributable to this batch.
- Focused M1 trust/session/DI and M5 adapter unit tests: PASS, 39 before the two added deletion-state regressions; focused identity regression rerun: PASS, 27.
- Focused M5 route-gate and HTTP tests: PASS, 4/4.
- Full unit suite: PASS, 3,282 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration suite: PASS, 203 passed, 0 failed, 0 skipped using `FullyQualifiedName!~Sql`; M3/M4/M5/M6 SQL opt-in variables were inspected and unset. Process-only Event Log suppression was used for the Windows test host. No SQL/provider tests ran.
- `git diff --check`: final result recorded after all report changes.

### Remaining blockers

- M5 SQL-backed draft persistence, disposable SQL acceptance, atomic concurrency, durable create idempotency, and client revision/ETag semantics remain unverified/unimplemented; production route exposure is not enabled.
- Existing default media verifier remains fail-closed, so photo clues are not available until an approved provider is implemented and accepted.
- M5 proof/reward/review HTTP APIs remain out of scope; Phase B/C HOLD remains; expiry worker remains disabled.
- M1 database query behavior was unit-tested with EF InMemory and scoped SQL provider registration was composition-tested without opening a connection; no live SQL/schema acceptance is claimed.

Next safe implementation task: get DB Coordinator approval for a named disposable SQL target and durable draft schema/rowversion/delete requirements; then add isolated SQL CRUD/concurrency/idempotency tests before considering production route activation. Continue unrelated approved M2/M3/M4/M6/M7 work independently.

## Preservation and commit preparation checkpoint (2026-11-22)

### Git inventory

- Branch: `task/backend-m1-m7-completion`; HEAD: `e3b0e9336e9f19003811f01649491c27fa3730a5`.
- No files are staged. Ten tracked application/test files have local modifications; 30 untracked non-`tmp/` files are present (implementation/tests plus reports). `git diff --check` passes.
- `tmp/` contains 613 untracked files, including generated PDF/ERD tooling and `tmp/worktrees/m6-backend-completion`. The M6 and other contributor worktrees were only inventoried. Exclude all `tmp/` paths and do not modify them.
- A filename scan found no database backup/archive/key-file extensions among untracked files outside `tmp/`. A focused credential-pattern scan of intended M1/M2/M5 source/tests/reports found no API keys or private keys. Test-only JWT strings are explicitly test fixtures. There are no staged files to inspect for accidental inclusion.
- Exclude unrelated audit/report files, `demiltan-m6-backend-review.txt`, and all worktree/generated artifacts from the commits below. Relevant completion docs are isolated into a final documentation commit.

### Independent implementation groups

**M1 trusted identity**

- `src/Wayhunter.Api/Extensions/IdentityPersistenceServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Services/IdentitySocial/SqlTrustedIdentityAccountReader.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityPersistenceRegistrationTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SqlTrustedIdentityAccountReaderTests.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/AuthenticatedSessionAuthorizationServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/IdentitySocial/TrustedIdentityResolutionServiceTests.cs`

**M2 redirect credential protection**

- `src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs`
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GeminiRedirectHttpTests.cs` (new)
- `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GoogleNearbyPlacesRedirectHttpTests.cs` (new)

**M5 Games** (keep together because `Program.cs`, Games DI, route guard, archive wiring and draft HTTP integration are related)

- `src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs`
- `src/Wayhunter.Api/Program.cs`
- `src/Wayhunter.Api/BackgroundServices/GameExpiryHostedService.cs` (new; remains disabled by default)
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApiControllerFeatureProvider.cs` (new)
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApplicationAdapter.cs` (new)
- `src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftsController.cs` (new; Development-only route gate)
- `src/Wayhunter.Api/Properties/AssemblyInfo.cs` (new)
- `src/Wayhunter.Application/Modules/Games/Interfaces/IExpiredGameArchiveStore.cs` (new)
- `src/Wayhunter.Application/Modules/Games/Services/ArchiveExpiredGamesService.cs` (new)
- `src/Wayhunter.Application/Modules/Games/Services/GameSubmissionService.cs`
- `src/Wayhunter.Infrastructure/Integrations/Games/UnavailableGameClueMediaAccessVerifier.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/InMemoryGameSubmissionStore.Expiry.cs` (new)
- `src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameRepository.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/ArchiveExpiredGamesServiceTests.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/Games/GameDraftApplicationAdapterTests.cs` (new)
- `tests/Wayhunter.UnitTests/Modules/Games/GameSubmissionServiceTests.cs`
- `tests/Wayhunter.UnitTests/Modules/Games/M5GamesApplicationDiRegistrationTests.cs` (new)
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftDevelopmentApiHttpTests.cs` (new)
- `tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftRoutesApprovalGateHttpTests.cs` (new)

### PR overlap check (#280–#287)

All eight PRs remain open and unmerged in the fetched GitHub metadata. #280 is draft; #281–#286 are stacked in order on their predecessor; #287 targets `develop`. Local M1/M2/M5 implementation/test paths have no exact filename overlap with their changed-file lists. #282 and #284 overlap each other in `tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/NearbyPlacesHttpTests.cs`; the local redirect tests use separate filenames and do not copy those PR tests. #287 changes `SessionBearerAuthenticationHandler.cs`, but that file is not modified in the current working tree. Preserve the PR stack and do not cherry-pick it as part of these local commits.

### Safe staging / commit order (commands prepared, not executed)

M1 must precede M5 because the M5 Draft adapter uses the registered trusted identity resolver. M2 is independent and may be committed before or after M1.

```powershell
# M1
 git add -- src/Wayhunter.Api/Extensions/IdentityPersistenceServiceCollectionExtensions.cs src/Wayhunter.Api/Services/IdentitySocial/SqlTrustedIdentityAccountReader.cs tests/Wayhunter.UnitTests/Modules/IdentitySocial/IdentityPersistenceRegistrationTests.cs tests/Wayhunter.UnitTests/Modules/IdentitySocial/SqlTrustedIdentityAccountReaderTests.cs tests/Wayhunter.UnitTests/Modules/IdentitySocial/AuthenticatedSessionAuthorizationServiceTests.cs tests/Wayhunter.UnitTests/Modules/IdentitySocial/TrustedIdentityResolutionServiceTests.cs
 git diff --cached --check
 git diff --cached --stat
 git commit -m "feat(m1): register trusted identity account reader"

# M2 (independent; may be done before M5)
 git add -- src/Wayhunter.Api/Extensions/TravelPlanningServiceCollectionExtensions.cs tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GeminiRedirectHttpTests.cs tests/Wayhunter.IntegrationTests/Modules/TravelPlanning/GoogleNearbyPlacesRedirectHttpTests.cs
 git diff --cached --check
 git diff --cached --stat
 git commit -m "fix(m2): block credential forwarding across redirects"

# M5 (after M1)
 git add -- src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs src/Wayhunter.Api/Program.cs src/Wayhunter.Api/BackgroundServices/GameExpiryHostedService.cs src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApiControllerFeatureProvider.cs src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftApplicationAdapter.cs src/Wayhunter.Api/Controllers/Mobile/Games/GameDraftsController.cs src/Wayhunter.Api/Properties/AssemblyInfo.cs src/Wayhunter.Application/Modules/Games/Interfaces/IExpiredGameArchiveStore.cs src/Wayhunter.Application/Modules/Games/Services/ArchiveExpiredGamesService.cs src/Wayhunter.Application/Modules/Games/Services/GameSubmissionService.cs src/Wayhunter.Infrastructure/Integrations/Games/UnavailableGameClueMediaAccessVerifier.cs src/Wayhunter.Infrastructure/Persistence/Repositories/Games/InMemoryGameSubmissionStore.Expiry.cs src/Wayhunter.Infrastructure/Persistence/Repositories/Games/SqlGameRepository.cs tests/Wayhunter.UnitTests/Modules/Games/ArchiveExpiredGamesServiceTests.cs tests/Wayhunter.UnitTests/Modules/Games/GameDraftApplicationAdapterTests.cs tests/Wayhunter.UnitTests/Modules/Games/GameSubmissionServiceTests.cs tests/Wayhunter.UnitTests/Modules/Games/M5GamesApplicationDiRegistrationTests.cs tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftDevelopmentApiHttpTests.cs tests/Wayhunter.IntegrationTests/Modules/Games/M5DraftRoutesApprovalGateHttpTests.cs
 git diff --cached --check
 git diff --cached --stat
 git commit -m "feat(m5): preserve Games draft and archive workflows"

# Relevant documentation only; exclude all other untracked audit reports.
 git add -- WAYHUNTER_BACKEND_COMPLETION_REPORT.md WAYHUNTER_FINAL_API_ENDPOINT_MATRIX.md WAYHUNTER_BACKEND_FINAL_IMPLEMENTATION_HANDOFF.md WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md
 git diff --cached --check
 git diff --cached --stat
 git commit -m "docs: record backend implementation and release status"
```

Staging and commits completed in the planned order. M1: `19b272bf81837bcabb1c5ca4cb70095ed7051d27`; M2: `ff4ce4d7f661975a8cc206a88b6612b0eea26eef`; M5: `f467cf434f8122c9ce2e424069b8a44305a07fe7`. Each staged path list matched its approved group and each cached diff passed `git diff --cached --check`. The documentation commit remains pending.

### Verification evidence carried forward

No source or test changes were made during this inventory. Latest completed verification on this branch: restore passed; full solution build passed with 0 errors and two existing xUnit2031 warnings; unit tests 3,282 passed; filtered non-SQL integration tests 203 passed with SQL opt-in variables unset; `git diff --check` passed. No SQL/provider tests, migrations, provider activation, or production route activation occurred.
