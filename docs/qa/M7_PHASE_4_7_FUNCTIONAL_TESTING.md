# M7 Phase 4.7 functional testing

Branch `task/m7-api-functional-testing`, based on M6 `1c5a6a1309fc08b8966a824d2ab3b8d7b9eec2d6`.

## LocalDB fixture and run

The fixture skip condition checks `WAYHUNTER_M7_TEST_SQL_CONNECTION`. The prior seven-pass, one-skip run omitted it, so the SQL fact was skipped. The final run set that variable in the same PowerShell process that invoked `dotnet test`, alongside the runtime startup override `ConnectionStrings__WayhunterSharedDb` and SQL feature flag.

The fixture permits only `(localdb)\WayhunterTest2026` and `(localdb)\MSSQLLocalDB`, integrated authentication, and `Wayhunter_Test_M7_<32 hex>`. It rejects credentials, attach-file connections, arbitrary server names, and `db82600`. Seven allowlist regression cases passed.

Fresh catalog `Wayhunter_Test_M7_6bbc02e964ed4e9fb4f738e23479b3bc` was created after checking the named instance and was confirmed empty with `SERVERPROPERTY('IsLocalDB') = 1` and matching `DB_NAME()`. The test host received the same process-level connection string before startup. EF CLI applied all nine existing migrations. The SQL test then passed, without skip. TRX: `artifacts/m7-test-results/M7SqlHttp.trx`.

## Eight audited operations

| Operation | Expected and observed | Result |
| --- | --- | --- |
| GET /api/admin/advertisements | 200; anonymous/invalid JWT 401 | PASS |
| POST /api/admin/advertisements | 201; invalid payload 400 | PASS |
| GET /api/admin/advertisements/{advertisementId} | 200; empty ID 400; missing ID 404 | PASS |
| PUT /api/admin/advertisements/{advertisementId} | 200; invalid body 400; missing ID 404 | PASS |
| POST /api/admin/advertisements/{advertisementId}/image | 503 with valid multipart PNG because image upload/storage gates are disabled | BLOCKED for positive upload; fail-closed behavior PASS |
| PATCH /api/admin/advertisements/{advertisementId}/status | 200 for activation; invalid enum 400 | PASS |
| GET /api/mobile/advertisements/active | 200; filters placement and excludes future scheduled ad | PASS |
| GET /api/mobile/home | 200; persisted active banner composed; anonymous 401 | PASS |

The test persists two synthetic ads in SQL, updates one title, activates current and future ads, and verifies filtered active reads and Home composition. No real content or storage provider was used. All 8 route-guard regression cases (7 connection guard facts and SQL HTTP fact) passed. M7 core status: 7 PASS, 0 FAIL, 1 BLOCKED, 0 NOT TESTED. The blocked positive upload depends on approved storage and lifecycle activation.

## Defect and contracts

The connection guard was expanded to the requested LocalDB instance while retaining strict catalog and integrated-authentication checks. Regression coverage rejects remote SQL, unexpected LocalDB, unsafe catalog, db82600, and SQL credentials. The multipart test setup was corrected to include a PNG file field. No production code, schema or configuration changed.

The audited controllers expose no campaign-management, impression-ingestion, click-ingestion, or analytics-report HTTP routes. Campaign domain lifecycle has unit coverage, but HTTP, durable campaign SQL runtime, event acceptance/deduplication/rate limits, and report aggregation are not implemented as audited routes. Campaign attribution and delivery approvals remain pending in `docs/task-evidence/M7-MS07_T01_T03_OWNER_DECISION_REGISTER.md`. Privacy, retention, consent linkage, replay and reporting decisions remain pending with M1, M7, DB Coordinator, campaign owner and designated governance approvers in `docs/task-evidence/M7-MS07_T01_T03_PRIVACY_OWNER_DECISIONS.md`.

## Verification

API build passed. The previously run 439 HomeAdsAnalytics unit tests remain applicable because domain/application code did not change. The final targeted SQL integration invocation passed 8/8 with 0 skipped. EF model consistency passed in a separately executed user-provided verification. The user reports NuGet TCP/HTTP checks passed, dotnet restore passed, Infrastructure and API builds passed, and dotnet ef migrations has-pending-model-changes with context WayhunterSharedDbContext and no-build returned “No changes have been made to the model since the last migration.” Codex did not execute these restore/build/model checks during this finalization turn, so they are recorded as manually supplied results. This check did not apply or generate a migration.

Changed files are the M7 SQL integration test and this report. The consolidated report records the cumulative route tally and previous member limitations.
