# M6 Phase 4.6 functional testing

The 21 operations below come from the preserved Swagger audit and the five CoinsWallet controllers. The branch starts at M5 SHA `2646579ab6c168737723782dc3a4d2cccb87a8e9`, verified directly against GitHub. Routes in the table are relative to `/api/mobile/coins-wallet` or `/api/admin/coins-wallet`.

## HTTP results

The final isolated run exercised all 21 operations and passed. All 21 returned 401 for both absent and invalid bearer credentials. All 13 Admin operations returned 403 for a signed-in traveler. The table shows expected and observed authenticated results from the final run.

| Scope | Operation | Expected and actual | Result |
| --- | --- | --- | --- |
| Mobile | GET /me | 200 | PASS |
| Mobile | GET /me/transactions | 200, 400 for invalid page | PASS |
| Mobile | GET /vouchers/offers | 200 | PASS |
| Mobile | GET /vouchers/offers/{offerId} | 200, 404 for missing or expired offer | PASS |
| Mobile | POST /vouchers/requests | 200 including concurrent same-ID replay, 400 for empty offer ID, 409 for insufficient spendable balance | PASS |
| Mobile | GET /vouchers/requests/{requestId} | 200 owner, 404 foreign traveler | PASS |
| Mobile | POST /vouchers/requests/{requestId}/cancel | 200 including replay | PASS |
| Mobile | GET /vouchers/requests/{requestId}/issued-voucher | 200 owner, 404 foreign traveler | PASS |
| Admin | POST /ledger/{ledgerEntryId}/reverse | 200 including replay | PASS |
| Admin | GET /reports/operations | 200 | PASS |
| Admin | GET /reports/reconciliation | 200 | PASS |
| Admin | GET /rule-versions | 200 | PASS |
| Admin | POST /rule-versions | 200 | PASS |
| Admin | POST /vouchers/expiry/run | 200 | PASS |
| Admin | POST /vouchers/inventory | 200 | PASS |
| Admin | POST /vouchers/offers | 204 | PASS |
| Admin | GET /vouchers/requests | 200 | PASS |
| Admin | POST /vouchers/requests/{requestId}/approve | 200 including replay, 409 for injected pre-commit failure | PASS |
| Admin | POST /vouchers/requests/{requestId}/issue | 200 including replay | PASS |
| Admin | POST /vouchers/requests/{requestId}/reject | 200 | PASS |
| Admin | POST /vouchers/requests/{requestId}/use | 200 | PASS |

## SQL and financial integrity

- The runtime scoped DbContext and factory were checked against the disposable catalog on `(localdb)\WayhunterTest2026`. The actual factory connection reported `IsLocalDB = 1`, the intended empty catalog and SQL version `17.0.4025.3` before migration. All nine existing migrations were applied and compared with the EF migration list. EF reported no pending model changes. The final catalog was `Wayhunter_M6_Functional_d905ab17b25b4b108c8903fde0602544`.
- SQL started at 10,500 fixture Coins. Concurrent identical requests created one 10,000-Coin hold. Cancel and reject each released their hold. Approval consumed the reservation and posted one 10,000-Coin debit. Issuance replay retained a single issuance and did not debit again. Reversal replay posted one compensating 100-Coin debit. The ending wallet had 400 posted Coins and zero reserved, with the posted balance checked against the ledger sum throughout.
- An injected pre-commit failure returned 409 and left the request Pending, the hold intact, and zero debit entries. Retrying with concurrent identical approvals and issuances succeeded with a single debit and issuance. A lost commit acknowledgement and a two-wallet single-stock race remain unverified by the new suite.
- The owner saw the fixture voucher code and a QR payload. Responses outside owner retrieval were checked for the fixture code. OTP and reset email used in-memory capture. Workers and reward delivery were off. The run used a test-only protection key and synthetic data, with no external provider calls.
- The audited contracts do not include a traveler balance write, a direct earning route, or voucher update/activation/deactivation. The existing expiry operation returned 200. Offer expiry was independently verified by advancing the fixture clock and observing 404. The expiry sweep was called without a due, unused issuance. Live private image retrieval requires a separately approved storage path.

## Reward boundaries and approvals

- **M4 Hunter +200 delivery is BLOCKED for live activation.** The focused tests cover approved eligibility and server-owned amount, deterministic event identity, duplicate handling and retry contracts. M4 review SQL was verified in phase 4.4. The approved-decision handoff to M6 and live worker activation still require M4, M6, DB Coordinator and Project Lead approval. This phase left both the writer and worker disabled. [M4 evidence](M4_PHASE_4_4_FUNCTIONAL_TESTING.md) · [M6 handoff requirements](../task-evidence/M6_BACKEND_COMPLETION_REPORT.md)
- **M5 creator +100 delivery is BLOCKED for live activation.** Phase 4.5 verified the SQL creator approval reference. The committed approval still needs a production M6 enqueue or post-commit adapter. **M5 verified winner +100 delivery is separately BLOCKED** by trusted proof capture, completion and result approval, durable winner handoff, and reward activation. The focused unit tests cover server-owned amounts, eligibility, single-winner constraints and retry behavior. The responsible owners are M5, M6, DB Coordinator and Project Lead, with the capture-provider owner for proof binding. No M4 or M5 end-to-end SQL reward delivery was performed. [M5 evidence](M5_PHASE_4_5_FUNCTIONAL_TESTING.md) · [M6 handoff requirements](../task-evidence/M6_BACKEND_COMPLETION_REPORT.md)
- **Live voucher activation requires coordinated release approval.** M6, Identity/Admin, DB Coordinator, private storage owner and Project Lead own trusted Admin provisioning, voucher protection key configuration, private asset access and runtime rollout. The local profile used the existing SQL voucher runtime flag and trusted synthetic Admin assignment. [Runtime gates](../../src/Wayhunter.Api/Program.cs) · [M6 completion evidence](../../WAYHUNTER_M6_FINAL_COMPLETION_REPORT.md)

## Verification and changed files

- API build passed with zero warnings and errors. The focused CoinsWallet, SQL model, reward-boundary and JWT unit filter passed 382 tests. All 12 focused legacy HTTP and JWT regressions passed. The isolated 21-operation functional test passed. Git whitespace checks passed for both added files.
- The authenticated rule-version request initially returned 500 because the isolated host clears configuration sources and required the synthetic voucher key in the replacement settings. The fixture now supplies that key. The cancellation fixture also now supplies the required body. These were test fixture corrections. This phase makes no application or schema changes.
- Added `tests/Wayhunter.IntegrationTests/Modules/CoinsWallet/LocalDbM6FunctionalTests.cs` and this evidence file. The three untracked Swagger audit artifacts were preserved. The draft Master BRD is referenced by the repo context and was unavailable in this checkout.

M6 tally is **21 PASS, 0 FAIL, 0 BLOCKED, 0 NOT TESTED**. The original backend inventory is now **108 PASS, 3 BLOCKED, 0 FAIL, 8 NOT TESTED out of 119**. Activation dependencies above are outside the 21 LocalDB operation results.
