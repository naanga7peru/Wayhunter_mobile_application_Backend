# WAYHUNTER M6 CoinsWallet Completion Report

**Classification:** PARTIAL — wallet/reward/voucher workflows and their unit coverage are present and compile, but durable SQL behavior is not verified against an isolated database and the shared migration remains on hold.

## 1. Branch and baseline

- Branch: `task/m6-coinswallet-final-completion`
- Baseline/HEAD at inspection: `e3b0e9336e9f19003811f01649491c27fa3730a5` (`origin/develop`)
- Working tree was clean at start. This pass added anonymous-route HTTP coverage and this completion report; it did not change production code.
- The earlier `task/m6-backend-completion` commit is an ancestor of this baseline, confirming its M6 workflow implementation is already merged.
- No commit, push, PR, merge, migration generation, or database access was performed.

## 2. Existing M6 functionality

Code verified in the current checkout includes:

- Authenticated owner wallet reads, claim-derived ownership checks, balance projection and paginated ledger history.
- Fixed server-side welcome (+100), approved Hunter (+200), approved Game creator (+100), and verified Game winner (+100) reward handlers, with source readers and idempotent grant storage.
- SQL wallet/history readers and opt-in SQL reward grant, outbox, and voucher runtime registrations.
- Pending voucher requests reserve the approved 10,000 Coins; the 10,100 spendable threshold is enforced. Admin approval is the final debit trigger; rejection/cancellation releases the reservation, and issuance does not debit again.
- Admin voucher queue, offers, stock, approve/reject, issue/retry, use, expiry, ledger correction, rule-version, operations report, and reconciliation services.
- Protected voucher secret handling and fail-closed admin authorization policy.
- EF mappings and SQL repository implementations exist, but their production database compatibility is not proven without the reviewed migration and SQL verification.

The checked-in legacy M6 completion notes and migration-readiness review contain stale/conflicting status statements. Current `ACTIVE_RULES.md` controls the voucher debit rule; the legacy statements do not supersede it.

## 3. API endpoint inventory

| Method and route | Contract | Authentication / authorization | Service and persistence | Tests / readiness |
|---|---|---|---|---|
| `GET /api/mobile/coins-wallet/me` | `MyCoinWalletResponse` | `[Authorize]`; owner derived from claims | `GetMyCoinWalletService` → SQL wallet reader | Unit coverage; SQL gated; PARTIAL |
| `GET /api/mobile/coins-wallet/me/transactions?page=&pageSize=&kind=&rewardType=` | `CoinTransactionHistoryResponse` | `[Authorize]`; owner derived from claims | `GetMyCoinTransactionHistoryService` → SQL history reader | Unit coverage; SQL gated; PARTIAL |
| `GET /api/mobile/coins-wallet/vouchers/offers` | list of `SampleVoucherOfferResponse` | `[Authorize]` | `SampleVoucherRequestService` → SQL voucher request store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/mobile/coins-wallet/vouchers/requests` | `CreateSampleVoucherRequest` → `SampleVoucherRequestResponse` | `[Authorize]`; owner derived from claims | `SampleVoucherRequestService` → SQL store | Unit coverage; SQL gated; PARTIAL |
| `GET /api/mobile/coins-wallet/vouchers/requests/{requestId}` | `SampleVoucherRequestResponse` | `[Authorize]`; owner checked in service | Request service → SQL store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/mobile/coins-wallet/vouchers/requests/{requestId}/cancel` | `CancelSampleVoucherRequest` → `SampleVoucherRequestResponse` | `[Authorize]`; owner checked in service | Request service → SQL store | Unit coverage; SQL gated; PARTIAL |
| `GET /api/mobile/coins-wallet/vouchers/requests/{requestId}/issued-voucher` | `MyIssuedSampleVoucherResponse` | `[Authorize]`; owner checked in service | Request service → SQL store/protected voucher data | Unit coverage; SQL gated; PARTIAL |
| `GET /api/admin/coins-wallet/vouchers/requests?page=&pageSize=` | `SampleVoucherAdminQueueResponse` | `CoinsWalletAdministratorPolicy` | `SampleVoucherAdminService` → SQL admin store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/offers` | `CreateSampleVoucherOfferRequest` → no content | Admin policy | Admin service → SQL admin store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/inventory` | `AddSampleVoucherInventoryRequest` → `SampleVoucherInventoryResponse` | Admin policy | Admin service → SQL admin store/protector | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/requests/{requestId}/approve` | `SampleVoucherDecisionRequest` → `SampleVoucherRequestResponse` | Admin policy | Admin service + administration store; approval finalizes one debit | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/requests/{requestId}/reject` | `SampleVoucherDecisionRequest` → `SampleVoucherRequestResponse` | Admin policy | Admin service + administration store; releases hold | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/requests/{requestId}/issue` | no body → `SampleVoucherIssuanceResponse` | Admin policy | Admin service → SQL admin store; retry does not debit | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/requests/{requestId}/use` | `ConsumeSampleVoucherRequest` → `SampleVoucherIssuanceResponse` | Admin policy | Admin service → SQL admin store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/vouchers/expiry/run?maxCount=` | no body → `ExpireSampleVouchersResponse` | Admin policy | Admin service → SQL admin store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/ledger/{ledgerEntryId}/reverse` | `ReverseCoinLedgerEntryRequest` → `CoinLedgerCorrectionResponse` | Admin policy | Admin service → SQL administration store | Unit coverage; SQL gated; PARTIAL |
| `POST /api/admin/coins-wallet/rule-versions` | `CreateCoinRuleVersionRequest` → `CoinRuleVersionResponse` | Admin policy | Admin service → SQL administration store | Unit coverage; SQL gated; PARTIAL |
| `GET /api/admin/coins-wallet/rule-versions` | list of `CoinRuleVersionResponse` | Admin policy | Admin service → SQL administration store | Unit coverage; SQL gated; PARTIAL |
| `GET /api/admin/coins-wallet/reports/operations` | `CoinOperationsReport` | Admin policy | Admin service → SQL administration store | Unit coverage; SQL gated; PARTIAL |
| `GET /api/admin/coins-wallet/reports/reconciliation` | `CoinReconciliationReport` | Admin policy | Admin service → SQL administration store | Unit coverage; SQL gated; PARTIAL |

No M6 public route was invented or changed in this pass. Added HTTP integration coverage confirms anonymous requests to key mobile wallet/voucher and Admin voucher/ledger routes receive 401 responses.

## 4. Newly implemented production code and API changes

No new production code. Inspection found the prior M6 implementation already merged into `develop`, so no duplicate implementation was added. No API route or DTO was changed. Added anonymous-access HTTP tests for representative mobile and Admin routes.

## 5. Files modified

- Added `tests/Wayhunter.IntegrationTests/Modules/CoinsWallet/CoinsWalletAnonymousAccessHttpTests.cs` (seven HTTP cases covering anonymous wallet, transaction, offer, request, admin queue, approval, and ledger-correction access).
- Added `WAYHUNTER_M6_FINAL_COMPLETION_REPORT.md` (this report).

## 6. Tests and exact verification counts

- `dotnet restore Wayhunter.slnx` — completed for all solution projects. NuGet emitted `NU1900` because `https://api.nuget.org/v3/index.json` was unavailable for vulnerability metadata.
- `dotnet build Wayhunter.slnx --no-restore` — succeeded, 0 errors, 6 warnings (`NU1900` plus two existing xUnit analyzer warnings).
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore` — passed: 3,256 passed, 0 failed, 0 skipped.
- `dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CoinsWalletAnonymousAccessHttpTests` — passed: 7 passed, 0 failed, 0 skipped.
- Final full `dotnet test tests/Wayhunter.IntegrationTests/Wayhunter.IntegrationTests.csproj --no-restore` — passed: 219 passed, 0 failed, 85 skipped. SQL suites were skipped behind their configured opt-in gates; no database was contacted.
- `git diff --check` and final `git status -sb` are pending after adding this report.

## 7. SQL and external provider readiness

- SQL schema/migration: **BLOCKED**. EF mappings and durable adapters exist, but the shared M6 migration is not approved or applied. The repository migration-readiness document says HOLD because coordinated schema review and M3 baseline isolation are still required.
- SQL verification: **BLOCKED**. `WAYHUNTER_M6_TEST_SQL_CONNECTION` was not configured. The opt-in tests constrain use to `(localdb)\MSSQLLocalDB` and the dedicated `Wayhunter_M6_IntegrationTest` database; those cases were not run.
- Providers: **PARTIAL / fail-closed**. No production voucher fulfillment provider was activated. Voucher runtime remains explicitly opt-in and protected demo inventory code/image data are not evidence of external provider readiness.

## 8. M1/M4/M5 dependencies

- M1: verified-account reward handler is registered and the OTP activation service has a conditional call site. Runtime reward persistence still depends on the explicit SQL writer configuration and migration.
- M4: approved Hunter reward handler/reader exist, but no M4 post-commit call site was found in the current API/Infrastructure source.
- M5: creator and winner handlers/readers exist, but no M5 post-commit call site was found in the current API/Infrastructure source. Do not enable unfinished Phase B/C reward flows.
- M7: no new Ads or analytics dependency was added.

## 9. Remaining blockers and decisions

- Obtain coordinated approval for the M6 schema and migration baseline, including M3 schema isolation and DB Coordinator review.
- Configure and run the isolated M6 LocalDB suite after the migration is available; verify reward concurrency, transaction rollback, voucher reserve/approve/reject, and issue retry against actual SQL state.
- Extend HTTP integration coverage to authenticated owner isolation and Admin role authorization with dedicated fake-identity/runtime dependencies.
- Coordinate and add trusted, post-commit reward dispatch from M4 approved Hunter decisions and M5 approved creator/winner decisions, preserving existing reward boundaries.
- Confirm deployed identity/admin claims configuration and opt-in runtime settings before enabling wallet writes.
- External PR duplicate-work check was inconclusive through the available GitHub search result; the local M6 completion commit is already merged into this baseline.

## 10. Recommended next batch

1. Resolve the shared migration review and isolate the agreed SQL baseline.
2. Extend focused M6 HTTP authorization/owner-isolation integration tests using the existing `WebApplicationFactory` test setup.
3. Wire M4/M5 post-commit event delivery only after their owners confirm trusted event contracts.
4. Run the dedicated isolated SQL tests; then reissue a completion report with SQL evidence.

## 11. Safe Git plan

Keep this change local for review. After review, commit only the completion report and any approved M6 source/test changes, push the requested task branch, and open a PR targeting `develop`. Do not push directly to `main`; include the BRD wallet/voucher rules, baseline/branch, files, test counts, SQL/provider risks, and remaining decision items.
