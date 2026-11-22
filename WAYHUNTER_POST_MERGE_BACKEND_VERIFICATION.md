# Wayhunter post-merge M1–M7 backend verification

**Verification date:** 2026-11-22
**Worktree:** `WAYHUNTER_POST_MERGE`
**Branch:** `task/backend-post-merge-verification` (expected)
**Baseline / latest `origin/develop`:** `a0ee0145008a80250c16f0247b7dc6870d170e98`
**Initial status:** clean; branch pointed to `origin/develop`.
**Verification changes:** one integration test assertion corrected; no application source, provider settings, schema, migration, or database was changed.

## Baseline and PR inventory

First-parent Git history at this SHA contains every requested merge:

| PR | Merge commit | Result |
|---|---|---|
| #280 | `384ba2a` | Included |
| #287 | `a0ee014` | Included; this is HEAD |
| #288 | `16abe96` | Included |
| #289 | `f3dc485` | Included |
| #290 | `9f3f916` | Included |
| #291 | `0c8fac1` | Included |
| #292 | `e4f59c9` | Included |
| #293 | `8948479` | Included |
| #294 | `03491f3` | Included |

The six M2 follow-up PRs are **open and unmerged** in GitHub metadata. Their actual base chain is:

| PR | State | Head branch | Actual base branch |
|---|---|---|---|
| #281 | Open | `review/m2-ms04-t03-20261121` | `review/m2-ms02-t06-20261121` (the #280 stack root) |
| #282 | Open | `review/m2-ms05-t06-20261121` | `review/m2-ms04-t03-20261121` (#281) |
| #283 | Open | `review/m2-ms07-t07-20261121` | `review/m2-ms05-t06-20261121` (#282) |
| #284 | Open | `review/m2-ms08-t07-20261121` | `review/m2-ms07-t07-20261121` (#283) |
| #285 | Open | `review/m2-ms10-t02-20261121` | `review/m2-ms08-t07-20261121` (#284) |
| #286 | Open | `review/m2-ms10-t05-20261121` | `review/m2-ms10-t02-20261121` (#285) |

Review #282 and #284 together because both change `NearbyPlacesHttpTests.cs`. Do not describe this stack as merged or as part of the current baseline. Existing worktrees and branches were preserved.

## Restore, build, and tests

| Check | Result |
|---|---|
| `dotnet build Wayhunter.slnx --verbosity minimal` (includes restore) | **Passed**, 0 errors, 10 warnings |
| Full unit suite | **3,311 passed**, 0 failed, 0 skipped |
| Full integration suite, SQL/provider opt-ins disabled | **226 passed**, 0 failed, **87 skipped**, 313 total |
| Final combined test result | **3,537 passed**, 0 failed, **87 skipped** |
| `git diff --check` | Passed |

Warnings: NuGet vulnerability-feed metadata could not be fetched from `api.nuget.org` in this environment (`NU1900`); the projects restored from available package state. Two existing xUnit analyzer warnings (`xUnit2031`) are in `tests/Wayhunter.UnitTests/Modules/TravelPlanning/TravelPlanningSqlServiceCollectionExtensionsTests.cs` lines 152 and 154. Build reported 10 warning instances and no compiler errors.

The Windows Event Log override (`Logging__EventLog__LogLevel__Default=None`) and removal of any `WAYHUNTER_*SQL*`, shared DB connection, and persistence-enable environment variables were applied only in each test process. SQL-named suites skipped on their opt-in gates. No SQL connection or migration was executed. The 87 skips are therefore outstanding durable-SQL evidence, not passing SQL verification.

### Integration regression corrected

`M5GamesRuntimeDiStartupTests.ActualProgram_SqlDisabled_DoesNotRegisterGameSql` failed because it asserted that `IGameRepository` and `IGameDraftDeletionRepository` were absent when SQL was disabled. The application registration explicitly preserves the in-memory M5 workflow in this mode. The test now verifies that both contracts resolve to the same `InMemoryGameSubmissionStore` and `SqlGameRepository` is absent. The rerun passed. This is a test expectation correction; no runtime behavior was changed.

## API and runtime readiness

The inspected HTTP surface has 35 controller source files and 94 HTTP method attributes, plus `/hubs/trip-location`. These routes and DTOs are code evidence only; this worktree does not contain the Flutter client contract to compare them against.

| Module | Code-verified readiness | Remaining integration/runtime gap |
|---|---|---|
| **M1 IdentitySocial** | Anonymous registration/OTP/login/password-reset endpoints; authenticated profile/logout; shared signed-session authentication and trusted `UserId` resolution are wired when configured. Authorization middleware is ordered before controller mapping. | Production email/OTP issuer, shared SQL persistence, migration acceptance and Flutter DTO/error-envelope review remain. Never treat Development identity mode as production auth. |
| **M2 TravelPlanning** | Authenticated trip, stop, suggested-plan, copy, route-preview, place, nearby, weather and AI endpoints. Ownership is resolved from authenticated identity in application services. Provider calls are separately configured. | SQL concurrency/retry acceptance and #281–#286 review/merge chain remain. Live map/AI/weather credentials, quota and fallback acceptance remain. |
| **M3 TripSafety** | Authenticated session, invitation, consent, hazard, safe-check-in, memory and notification routes. Admin routes use role/policy checks. SignalR requires authentication and rechecks publisher and recipient membership/consent before each location fanout; a connection must join the exact trip/session first. | Many stores default to in-memory or gated SQL. Durable multi-instance GPS revocation/fanout, route revision and 1 km hazard matching, notification delivery acknowledgement/provider, and isolated SQL race tests remain. |
| **M4 HunterPlaces** | Capture-session identity/ownership primitives and persistence mappings exist. | **No M4 HTTP controller/routes** and no concrete submission/review service are present. This blocks end-to-end Flutter Hunter integration pending approved routes, DTOs, status transitions, and trusted evidence/media boundary. |
| **M5 Games** | Draft controller is discoverable only in Development with explicit draft API + shared-auth flags and SQL disabled; default/Production route absence is tested. Draft identity comes from the signed session. Expiry logic archives at approval + 30 days and retains creator history in its store contract. | Draft workflow is in-memory and draft HTTP API is not production-enabled. Durable expiry, proof/review/winner flow and exactly-one reward lack isolated SQL acceptance; expiry worker is opt-in. |
| **M6 CoinsWallet** | Authenticated owner wallet/voucher routes and named Admin policy for administrative ledger/voucher actions. Service rules implement request reservation and approval/reject/issue separation; VCH-DEC-01 remains the governing contract (>=10,100 spendable, reserve 10,000, debit once at Admin approval, release on reject/cancel, no second debit at issue/retry). | SQL ledger/reservation/voucher runtime is explicitly gated. Durable atomic debit, duplicate/concurrent approval and issue-retry behavior, reconciliation and role source need isolated verification. QR is demo issuance output; OCR intake is optional. |
| **M7 HomeAdsAnalytics** | Authenticated active-ad read route, Admin policy on management/image routes, and `GetActiveAsync` filters enabled + Active + UTC window + optional placement in the EF query before `ToListAsync`. | SQL Server translation/schema is unverified; shared migration is stale. Analytics persistence/privacy and production image provider remain unapproved/unconfigured. |

Startup registration and middleware wiring compile and startup/HTTP tests pass. Most SQL and external providers are intentionally opt-in/fail-closed; successful build/tests do **not** establish production API readiness. No whole-client Flutter compatibility result is possible without the Flutter contract/repository. Existing route inventory: `WAYHUNTER_FINAL_API_ENDPOINT_MATRIX.md` (its older baseline references should not be mistaken for this verification SHA).

## Shared SQL model and migration readiness

`WayhunterSharedDbContext` has one checked-in migration, `20261117093023_InitialSharedSchema`, with its designer, SQL script and `WayhunterSharedDbContextModelSnapshot`. `dotnet ef migrations has-pending-model-changes` was run as a read-only design-time check and **reports pending changes**.

The merged SQL readiness review records and categorizes a model-to-snapshot difference of **94 EF operations**:

- 18 table additions
- 25 column additions
- 1 nullability alteration
- 2 alternate keys
- 42 index additions/replacements
- 4 check-constraint additions/replacements
- 1 dropped index and 1 dropped check constraint

The category sum is 94. The reviewed scope is primarily M1 identity/reset/roles, M2 copy attribution and composite trip key, M3 safety/outbox/memory/notification tables and hazard columns, and M6 wallet/voucher metadata and constraints. M7 query changes do not add model operations in this recorded diff. The detailed table/column/index list and risks are in [WAYHUNTER_SHARED_SQL_MIGRATION_REVIEW.md](WAYHUNTER_SHARED_SQL_MIGRATION_REVIEW.md) and [WAYHUNTER_M1_M7_FINAL_INTEGRATION_READINESS.md](WAYHUNTER_M1_M7_FINAL_INTEGRATION_READINESS.md).

This is **Git model evidence**, not proof that any migration was applied. Migration generation/application remains prohibited until approved. Required decisions include:

1. Fresh database versus upgrade of a populated catalog; authoritative baseline and backfills for required columns/defaults.
2. Seven module-owner and DB Coordinator schema sign-off, FK principal types, uniqueness/filtered-index duplicate remediation, delete/retention behavior and hazard coordinate policy.
3. Voucher status mapping, nullable inventory issuance policy, transaction/idempotency and approved backfill semantics consistent with VCH-DEC-01.
4. SQL Server version/collation, deployment/locking window, backup/restore and recovery ownership.
5. Exact disposable server/catalog, least-privilege identity, per-profile operation approval and fixture cleanup authorization before any isolated SQL verification.

### Existing local database evidence (kept separate)

Historical `M1-MS05` evidence names `Wayhunter_M1_RuntimeTest` and describes an isolated development SQL verification at that time. It is not current M1–M7 acceptance and does not prove the present database contents or migration state. M3's offline LocalDB inspection found the LocalDB instance registration stopped; it did not start LocalDB, enumerate catalogs, or open a connection. No local catalog was inspected during this task. Thus no current database schema/data evidence is asserted here.

## Flutter integration readiness

**Status: partial; contract mapping may begin, full M1–M7 integration is not ready.** Existing authenticated routes and DTOs can be mapped module by module, but Flutter must review M1 error/session contracts and M2–M3 request/response semantics against the actual client. M4 currently has no routes. M5 drafts are Development-only and in-memory. M6 state-changing flows lack durable SQL verification. M7 active-ad query translation and shared schema remain unverified. No client build or live-provider check was run.

## Exact next actions

1. **M2 owners:** review the open chain #281 → #286 cumulatively on the current `develop` head; reconcile shared-file edits in #282/#284; obtain review/checks in dependency order and rerun the integrated suite.
2. **Flutter + API owners:** produce a route/DTO/error/auth matrix against the actual Flutter models for the routes that exist, then resolve versioning, pagination, idempotency and conflict behavior before wiring irreversible flows.
3. **M4 owners:** approve submission/review route, DTO and lifecycle contracts plus identity/media trust rules; implement and verify the missing M4 HTTP boundary before Hunter client integration.
4. **M5/M6 owners:** finish durable workflow wiring and isolated SQL acceptance for archive/winner and voucher reservation/debit/issue idempotency. Keep draft API, SQL stores and workers behind existing gates until accepted.
5. **M7 owners:** run approved SQL translation/schema acceptance for active advertisement filtering; finalize analytics privacy/retention and image-provider decisions.
6. **DB Coordinator + all schema owners:** resolve the decisions above; only then prepare and review one shared migration candidate offline. Obtain separate written authorization naming the disposable catalog and permitted test operations before isolated SQL execution. Never use shared or production SQL as verification.
7. After these contracts and gates are accepted, run a Flutter integration build/test against the reviewed API and record the exact client commit and backend SHA.

## Evidence and risk summary

- **Branch/commit:** `task/backend-post-merge-verification`, baseline `a0ee0145008a80250c16f0247b7dc6870d170e98`.
- **Actual changed file:** `tests/Wayhunter.IntegrationTests/Modules/Games/M5GamesRuntimeDiStartupTests.cs` (corrected SQL-disabled DI expectation).
- **Tests run:** full solution restore/build, all 3,311 unit tests, all 313 integration tests with 87 SQL-gated skips, EF pending-model-change check, `git diff --check`.
- **Risks/blockers:** no SQL/provider or Flutter compatibility evidence; M4 HTTP layer absent; M5 remains Development/in-memory; SQL model/snapshot drift and outstanding schema approvals block migration and durable cross-module acceptance.
- **Decision required:** populated-vs-fresh schema baseline, backfill/FK/index/retention choices, DB ownership and SQL target authorization, M4 API/media contract, and client DTO/error compatibility.

No commit, push, merge, SQL connection, migration generation/application, provider activation, or other-worktree modification was performed.
