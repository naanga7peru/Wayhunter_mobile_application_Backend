# WAYHUNTER Backend — Final Implementation Handoff

**Date:** 2026-11-22
**Branch:** `task/backend-m1-m7-completion`
**HEAD:** `e3b0e9336e9f19003811f01649491c27fa3730a5`
**Release state:** not fully verified; no completion percentage is available without the canonical evidence-classified 217-task register and SQL/provider acceptance.

**Latest status note:** Read “Latest approved implementation checkpoint” at the end of this file as the current M1/M5 state. It supersedes the earlier historical verification and next-action text where those sections say the reader is unregistered or Draft routes are unmapped.

## Protected working tree

Pre-existing M2 redirect protection and tests, M5 DI/archive/expiry/fail-closed media changes and tests, reports, audits, and `tmp/` were preserved. Other worktrees were only inventoried, not modified. No reset, clean, stash, commit, push, merge, SQL, migration, or production provider action occurred. The branch remains the expected branch. The new M1 reader is also uncommitted/unregistered.

## New implementation in this sprint

### M1 trusted identity account reader — scoped DI registered

- Added `src/Wayhunter.Api/Services/IdentitySocial/SqlTrustedIdentityAccountReader.cs` as a read-only lookup from canonical GUID `UserId` to `WayhunterSharedDbContext.Users`, using `AsNoTracking`, `SingleOrDefaultAsync`, cancellation and fail-closed null on query failure.
- Added `tests/Wayhunter.UnitTests/Modules/IdentitySocial/SqlTrustedIdentityAccountReaderTests.cs` for active and suspended account resolution, missing/malformed IDs, cancellation, data-access failure, no tracking and principal-derived identity.
- The Project Lead approved the canonical Users.UserId source and validated signed-token sub mapping for this implementation. `ITrustedIdentityAccountReader` and `TrustedIdentityResolutionService` are now registered scoped in `AddWayhunterIdentityPersistence`; session revocation and account-state checks remain enforced. No SQL connection was opened.
- The new M1 paths do not overlap PR #287; PR #287 remains open/unmerged and changes Identity API contracts, controllers, handlers and related tests.

### M5 draft API boundary

- `GameDraftsController` now implements the four approved routes, but MVC discovery is guarded by Development + explicit `Games:EnableDevelopmentDraftHttpApi=true` + shared auth enabled + Games SQL disabled. The response DTO and safe error mapping are in place; production/default route discovery stays off.
- `M5DraftRoutesApprovalGateHttpTests` confirms the routes remain 404 under the default configuration. `M5DraftDevelopmentApiHttpTests` confirms the explicitly enabled Development host can exercise the approved slice and that Production remains unmapped.

## Verification evidence

- `dotnet restore Wayhunter.slnx`: passed; projects were up to date.
- `dotnet build Wayhunter.slnx --no-restore`: passed, 0 warnings/errors.
- Focused M1 reader unit tests: 11 passed.
- Full unit tests: 3,277 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration tests: 200 passed, 0 failed, 0 skipped (`FullyQualifiedName!~Sql`). Dedicated M3/M4/M5/M6 SQL variables were inspected and unset. Process-level Windows Event Log override used for test-host permissions; no code/config change for it.
- `git diff --check`: passed.
- Tests do not validate SQL schema/locking, production DI, external identity issuer, providers, migrations, or shared database behavior.

## Current M1–M7 release posture

| Module | Verified state in this handoff | Remaining critical gaps |
|---|---|---|
| M1 IdentitySocial | Existing register/login/OTP/reset/logout/profile routes plus new unregistered account-reader candidate. | M1 owner approval/DI; PR #287 Flutter/error compatibility review and required GitHub checks; production SQL/provider acceptance. |
| M2 TravelPlanning | Existing APIs; local Nearby Places/Gemini redirect hardening retained. | PR #280–#286 cumulative stack review, #280 isolated SQL retry/concurrency acceptance, provider acceptance. No PR was merged/copied. |
| M3 TripSafety | Existing API/application coverage documented in the endpoint matrix. | SQL race/persistence acceptance, route revision coordination, notification/provider runtime and owner decisions. |
| M4 HunterPlaces | Capture/session foundations and test coverage only; no new route/service in this handoff. | Submission/review implementations, trusted identity, media ownership and approved route/status contract. |
| M5 Games | Draft adapter, DI, archive/expiry and fail-closed media work preserved. | Draft route approval/identity reader integration/durable persistence; proof recovery remains Phase B/C HOLD; expiry worker remains disabled. |
| M6 CoinsWallet | Existing endpoints and wallet/voucher/reward code documented in endpoint matrix. | Durable SQL ledger/reservation/reward/reconciliation and isolated concurrency acceptance; trusted Admin/provider checks. |
| M7 HomeAdsAnalytics | Existing advertisement APIs and foundation documented in endpoint matrix. | Campaign/analytics contract and schema approvals, trusted Admin mapping and persistence/provider acceptance. |

These are module readiness notes, not a canonical task count. The overall denominator and strict completion percentages remain UNKNOWN/not computable.

## PR integration state checked

Current GitHub metadata on 2026-11-22 showed #256, #274, #279, #280–#287 open and unmerged. M2 dependency order remains #280 → #281 → #282 → #283 → #284 → #285 → #286. PR #282 and #284 both edit `NearbyPlacesHttpTests.cs`; cumulative review must reconcile them. PR #287 needs Flutter/mobile compatibility and required approval. PR #274 changes migration/snapshot artifacts; #256 has M5 proof-recovery SQL/schema content under HOLD; #279 is M7 persistence proposal. No changes were cherry-picked or duplicated into this branch.

## Exact next actions

1. **M1 owner:** approve or reject `SqlTrustedIdentityAccountReader` as the canonical M1 account source and confirm issuer/claim/account lifecycle rules. After approval, add `TryAddScoped<ITrustedIdentityAccountReader, SqlTrustedIdentityAccountReader>()` to `AddWayhunterIdentityPersistence` and test resolution through the production composition root. Keep registration off until then.
2. **Team Leader + M5 + API/mobile owners:** approve the precise draft route/DTO/error/replay/ETag contract in `WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md`; only then implement public controller routes.
3. **DB Coordinator:** decide durable M5 draft persistence and approve a named disposable SQL target/test plan. Do not restore the backup, run migrations or SQL without that authorization.
4. **M2 owner/DB Coordinator:** review #280 lost-acknowledgement semantics and authorize isolated SQL verification; then review the dependent #281–#286 stack cumulatively without duplicate files.
5. **M4 owners:** supply trusted identity/media ownership, status-transition and exact route contracts; implement submission/review services only from those decisions.
6. Continue M3/M6/M7 only where the approved task register and owner contracts identify an independent code change; do not invent campaigns, rewards, route geometry or persistence schema.

## Safe resume commands

```powershell
git status -sb
git branch --show-current
dotnet restore Wayhunter.slnx
dotnet build Wayhunter.slnx --no-restore
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore
```

For integration, inspect the current SQL/provider opt-in variables again, leave all `WAYHUNTER_*_SQL_CONNECTION` variables unset, exclude SQL-specific fixtures using verified rules, apply the process-only Event Log logging override if running on this Windows host, then run `git diff --check`. Do not enable feature flags or use the backup as an implicit test database.

## Latest approved implementation checkpoint (2026-11-22)

This checkpoint supersedes the earlier handoff sections describing M1 reader DI as unregistered and M5 draft routes as unmapped. The current branch now registers the read-only SQL account reader and trusted identity resolver scoped with the shared DbContext. The four Draft routes are implemented but their MVC controller is discoverable only in Development when `Games:EnableDevelopmentDraftHttpApi=true`, shared authentication is enabled, and Games SQL persistence is disabled. Production and default configurations leave these routes unmapped.

### API behavior

- POST and PUT use existing `CreateGameDraftDto`; PUT is full replacement.
- Create/read/update responses use a dedicated `GameDraftResponseDto` with ordered stages. No creator ID or persistence token is returned.
- Session authentication uses the signed-token `sub`; M1 checks account/session active, verified, deletion, revocation and expiry rules. Actor identity is never taken from body or route.
- 400 invalid input/media; 401 missing/invalid/inactive authentication; 404 missing or cross-owner draft; 409 state/SQL concurrency exception. Repeat DELETE follows current service behavior and returns 404. POST idempotency and client ETag are not implemented; stale in-memory PUT can overwrite an earlier representation.
- Text-only drafts work; unverified media fails closed. No proof, review, reward, expiry worker, or SQL runtime was activated.

### Latest verification

- Restore: passed, up to date.
- Full solution build: passed, 0 errors. The final clean build reported two existing xUnit2031 warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs` lines 152/154.
- Focused M1 DI/session and M5 HTTP suites: passed (39 focused unit checks before two additional deletion-state tests; 27 identity-focused tests after; M5 HTTP gate suite 4/4).
- Full unit suite: 3,282 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration suite: 203 passed, 0 failed, 0 skipped; SQL opt-in variables unset and SQL-named tests excluded.
- No SQL/migrations/providers/Git history operations were performed. Durable SQL acceptance and production route release remain blocked on DB Coordinator approval and isolated verification.

### Next action

Prepare a DB Coordinator review package for durable M5 draft persistence (schema ownership, rowversion, delete/child behavior, create replay policy and disposable target). Keep production routes disabled until SQL and release approvals are complete.
