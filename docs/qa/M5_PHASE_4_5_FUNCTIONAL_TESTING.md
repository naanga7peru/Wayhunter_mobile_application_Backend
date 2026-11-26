# M5 Phase 4.5 functional testing

M5 Games scope, from verified M4 commit `48b6d9cab99e922dc637f8c6e0b7e7b4e50fb737`. The current controllers match the 13 original audit operations. Five are mapped in the baseline profile. Eight are mapped when their feature gates are enabled.

Both profiles verified the actual runtime EF factory, `IsLocalDB = 1`, version 17.0.4025.3 and the intended empty catalog on `(localdb)\WayhunterTest2026` before applying all nine existing migrations. The SQL-enabled run used `Wayhunter_M5_Functional_da7a1a8a7b6a410f821f8a60fbcea86a`, and the default-off run used `Wayhunter_M5_Functional_37f104f440b34215b3529710a4de3523`. Email was captured in memory. Workers and reward delivery were off.

## HTTP results

13 PASS in authorized isolated profiles, 0 FAIL, 0 BLOCKED within the 13 tested operations, 0 NOT TESTED. Live activation remains a separate decision. All 13 enabled routes rejected missing and invalid bearer tokens with 401. With SQL and the Development draft flag off, the five baseline routes remained mapped and returned 503 to an authenticated caller. The eight gated operations were absent from Swagger. Draft creation returned 405 and the other gated requests returned 404.

| Operation | Profile | SQL-enabled expected and actual | Evidence |
| --- | --- | --- | --- |
| GET /api/mobile/games | Baseline | 200 | Draft hidden, published game listed, rejected game excluded. |
| GET /api/mobile/games/{gameId} | Baseline | 200 / 404 | Published detail available. Draft, rejected, and expired detail withheld. |
| GET /api/mobile/games/me | Baseline | 200 | Creator history is owner-scoped and retains archived games. |
| GET /api/mobile/games/me/attempts | Baseline | 200 | Player sees persisted attempt; unrelated player sees empty history. |
| POST /api/mobile/games/{gameId}/attempts | Baseline | 200 / 201 / 404 / 409 | Concurrent start produced one attempt/progress pair. Hidden or missing game 404. Creator or expired game conflict 409. |
| POST /api/mobile/games/drafts | Gated | 201 / 400 / 409 | Duplicate same-key requests returned one draft and durable request row. Invalid input, changed payload, and injected rollback were covered. |
| GET /api/mobile/games/drafts/{gameId} | Gated | 200 / 404 | Owner reads draft and revision. Foreign request is concealed. |
| PUT /api/mobile/games/drafts/{gameId} | Gated | 200 / 404 / 409 | Edit persisted and changed the revision. Foreign and stale writes denied. |
| DELETE /api/mobile/games/drafts/{gameId} | Gated | 204 / 404 / 409 | Eligible draft removed. Foreign or submitted draft protected. |
| POST /api/mobile/games/drafts/{gameId}/submit | Gated | 200 / 404 | Draft became PendingApproval. Foreign and missing drafts return the same 404. |
| GET /api/admin/games/reviews/pending | Gated | 200 / 403 | Admin sees the pending queue. Traveler denied. |
| POST /api/admin/games/reviews/{gameId}/approve | Gated | 200 / 403 / 404 / 409 | Concurrent approve had one winner and one conflict, one audit row and exactly 30 days between approval and expiry. |
| POST /api/admin/games/reviews/{gameId}/reject | Gated | 200 / 400 / 403 / 409 | Required reason, rejected state and persisted moderation decision checked. |

## Defects and limits

- The API build passed with zero warnings and errors. Both isolated Games profiles, 599 focused Games and M6 reward-boundary unit tests, and 23 focused HTTP and JWT regressions passed. The existing Swagger regression now counts the 13 Games operations independently of other modules and disables hosted workers.
- The shared draft controller mapped a foreign submit attempt to the 400 used for media validation, while an unknown draft returned 404. Submit now uses the same 404 for both. SQL attempt start similarly conceals Draft, PendingApproval, and Rejected games from non-creators. The regression checks the private and missing responses while preserving 409 for a creator trying to play their published game or a player trying to start an expired game.
- The age-off contract was checked by running the archive store explicitly against a historical fixture. The expired game was hidden from public reads and play and retained in SQL and the creator's My Games. No worker was activated.
- The draft feature provider requires shared authentication and Games SQL or the explicit Development draft flag. Creation review requires shared authentication and Games SQL. Durable attempt writing requires Games SQL, Development and the separate attempt-writer flag. These tests use only the existing local gates. [Draft API approval](../../WAYHUNTER_M5_DRAFT_API_V1_FINAL_APPROVAL.md) · [Games SQL wiring](../../src/Wayhunter.Api/Extensions/GamesServiceCollectionExtensions.cs)
- Successful drafts used text clues. The existing unavailable verifier rejects unverified media. No audited HTTP operation accepts player coordinates, answers, proof, score, or game completion. Those paths have no HTTP result here. The M5/DB Coordinator, capture-provider owner and Project Lead need to approve live media, trusted capture binding, durable proof recovery and release configuration. [Proof readiness](../architecture/M5_PROOF_CONSISTENCY_REPLAY_READINESS.md)
- SQL recorded exactly one creator approval event reference and no winner, reward outbox or coin ledger row. The client-supplied amount was not used. Existing Games and M6 boundary unit tests cover server-owned +100 amounts, winner eligibility and duplicate protection. M5, M6, the DB Coordinator and Project Lead own production event handoff, delivery and reconciliation approval. No live reward activation was performed.
