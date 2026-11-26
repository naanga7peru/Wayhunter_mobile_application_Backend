# Wayhunter backend consolidated testing report

## Scope and totals

The original audit CSV has 119 business operations across M1–M7. Prior evidence baseline was 108 PASS, 3 BLOCKED, 0 FAIL and 8 NOT TESTED. M7 exercised its eight routes. Seven passed their tested contracts. Image upload returned its configured fail-closed 503, so positive upload remains blocked. Cumulative status is **115 PASS, 4 BLOCKED, 0 FAIL, 0 NOT TESTED**. Route exercise coverage is **119/119 (100%)**. Functional pass rate is **115/119 (96.6%)**. A route returning an expected unavailable response is not counted as positive functional completion.

| Member | Audited ops | PASS | BLOCKED | FAIL | NOT TESTED |
| --- | ---: | ---: | ---: | ---: | ---: |
| M1 | 11 | 11 | 0 | 0 | 0 |
| M2 | 18 | 18 | 0 | 0 | 0 |
| M3 | 36 | 35 | 1 | 0 | 0 |
| M4 | 12 | 10 | 2 | 0 | 0 |
| M5 | 13 | 13 | 0 | 0 | 0 |
| M6 | 21 | 21 | 0 | 0 | 0 |
| M7 | 8 | 7 | 1 | 0 | 0 |
| Total | 119 | 115 | 4 | 0 | 0 |

M3 count follows its M3 evidence report, which records 36 API operations, while the cumulative original audit table’s assigned counts sum to 119. Related M2 consent, auth scenarios, health endpoint and SignalR hub are tracked outside the original business route count.

## M7 result

M7 fixture now allows only the named LocalDB instances and M7 GUID catalogs, with integrated auth. Fresh catalog `Wayhunter_Test_M7_6bbc02e964ed4e9fb4f738e23479b3bc` was verified empty on `(localdb)\WayhunterTest2026`, LocalDB property 1, before nine existing migrations. Actual SQL HTTP run passed, zero skipped, with TRX at `artifacts/m7-test-results/M7SqlHttp.trx`. Seven HomeAdsAnalytics unit tests had previously passed 439 cases. API build passed. Active ads, future scheduling exclusion, admin CRUD/status, Home composition and SQL writes were exercised. Image upload positive path is blocked by disabled storage and lifecycle flags, verified 503 fail-closed.

Campaign and analytics ingestion/report HTTP contracts are absent from the controllers. Campaign domain lifecycle unit coverage exists. Do not infer live campaign delivery, impression/click persistence, deduplication, rate limits or report accuracy from those domain tests.

## Previous member evidence and dependencies

- M1: 11/11 pass. M1 trusted administrator role integration remains a prerequisite for production Admin activation. See `docs/task-evidence/M7_M1_TRUSTED_ADMIN_RBAC_INTEGRATION_READINESS.md`.
- M2: 18/18 pass per supplied cumulative record. Live Gemini, Maps and weather provider verification and route geometry persistence/unresolved requirements remain outstanding. M2 consent API, auth scenarios, health and SignalR tracked separately.
- M3: 35 pass, 1 blocked. Admin Safety Summary, monitoring scope grants and projections, durable audit, live media/push remain outstanding.
- M4: 10 pass, 2 blocked. Trusted private-media verification, evidence/submission verification and M6 +200 Coins handoff remain approval dependent.
- M5: 13 pass. Live capture/media trust, proof recovery, absent gameplay HTTP contracts, production activation, creator/winner +100 Coins handoff remain outstanding.
- M6: 21 pass. M4/M5 live reward delivery, lost commit acknowledgement recovery, two-wallet final-stock race and due-unused issuance expiry remain unverified.
- M7: seven routes pass, image positive path blocked. Campaign and analytics HTTP operations absent. Image storage/runtime lifecycle disabled. Campaign attribution, privacy, retention and reporting scopes await decisions.

Approval-specific items and owners are detailed in `WAYHUNTER_BACKEND_PENDING_APPROVALS.md`. Existing migrations are the nine shared migrations used by LocalDB test profiles. M7 testing made no schema change. No evidence here establishes production readiness. Provider simulation and LocalDB do not establish live provider behavior.

## Defects and changed files

M7 test-only guard was expanded to permit `(localdb)\WayhunterTest2026`, retaining strict catalog and auth restrictions. The multipart fixture now submits a PNG part. No production defect or schema adjustment was made. Phase 4.7 changed `tests/Wayhunter.IntegrationTests/Modules/HomeAdsAnalytics/M7SqlAdvertisementHttpE2ETests.cs`, this report, and the pending approvals and M7 QA documents.


## Final EF verification

The user supplied final verification that NuGet connectivity and restore succeeded, Infrastructure and API builds passed, and the EF model check with context WayhunterSharedDbContext and no-build reported no model changes since the last migration. Codex did not independently rerun these commands in this finalization turn. No migrations were applied or generated for this check.
