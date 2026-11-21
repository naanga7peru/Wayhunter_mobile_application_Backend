# M5 test matrix — strict execution-authorization separation

Nothing in this matrix authorizes running SQL. **Unit-only** cases already reported 20/20 at commit `d854a76`; these do **not** count toward registry SQL verification.

## A. Allowed now: code-only, no DB activity

| ID | Scope | Purpose | State |
|---|---|---|---|
| U01 | Pure C# unit | Immutable binding + Game/Attempt/Step/player/proof owner rules | 20 tests reported passed, earlier commit; rerun for current SHA |
| U02 | Pure C# unit | Claim states 0/1/2/3/4, timestamp/terminal tombstone rules | Within same focused suite; report exact test names |
| U03 | EF metadata isolated model only | 16 columns, lengths, CHECK metadata, filtered and nonfiltered unique indexes, principal FK metadata | Proposal only; tests may be added; do not call migration, EnsureCreated, SQL |
| U04 | Fake-provider unit | Unknown, Consumed, Rejected, stable receipt, no blind retry | Authorized code-only; report separate results |
| U05 | Read-only source review | Read `GameRow` lock order, check registry not in shared DbContext | Authorized |

## B. Proposed synthetic registry SQL ONLY — EXECUTION ON HOLD

| ID | Layer | Scenario | Assertion |
|---|---|---|---|
| M01–M06 | SQL physical metadata | 16 cols/5 CHECKs/PK & indexes/3 FKs/guard/role | Exact audited metadata and effective permissions |
| A01 | TX-A | Register operation + registry reservation | Both present in same commit |
| A02 | TX-A failure | Throw after op INSERT | Both absent on rollback; fake M4 calls zero |
| A03/A04 | Idempotency | Exact repeat / changed binding | Same claim / reject conflict |
| A05 | Concurrency | Same media, distinct Games, 2 connections | Exactly one owner; other unique conflict |
| A06/A07 | Crash / invariant | Missing COMMIT ACK or partial rows | New connection reconcile or fail closed |
| A08 | Locks | Concurrent Game-level writer | Game-first order, bounded retry; no broken proof path |
| B01 | TX-B | Confirm fake consumed receipt | Op receipt/state + claim State 1 atomic |
| B02/B03 | TX-B failure | Mid-update exception / lost COMMIT ACK | Both rollback or fresh coherent read |
| B04 | Fake provider | Unknown response | No claim confirmation or M5 evidence/progress/reward |
| C01 | TX-C | Evidence/progress/op/registry | Atomic completion, same EvidenceId |
| C02/C03 | TX-C failure | Crash or lost COMMIT ACK | Rollback or authoritative reconcile; no double advance |
| C04/C05 | Fencing | Concurrent finalizers / retry completed op | One winner, same receipt/evidence |
| C06/C07 | Integrity | Wrong game/attempt/step / final stage | FK deny mismatch; no premature rewards |
| D01–D04 | DB guard/security | Change immutable key, rebind, delete tombstone, old bypass | All forbidden by effective DB enforcement |
| P01 | Stress | Deadlock victim / race / timeout | Safe retry only after reconciliation |
| H01–H04 | Synthetic legacy simulation | Unknown provider, duplicates, alias ambiguity, rollback | Cutover remains fenced, no inferred historical provenance |

**Required additional review:** implementation of controlled TX-A/B/C SQL procedures does not exist as approved production code today. These SQL cases cannot be claimed passing simply because DDL is prepared or the C# tests pass. A dry review is not execution.

## C. M4-dependent provider/capture — NOT PART OF REQUESTED REGISTRY SQL TEST GRANT

| ID | Owner | Dependency | Current status |
|---|---|---|---|
| P-M4-01 | M4 | Authoritative provider namespace and equivalence vectors | HOLD |
| P-M4-02 | M4 | Operation-bound Game-purpose capture session contract; HunterSubmission compatibility | HOLD |
| P-M4-03 | M4 | Real durable consume-once receipt recovery, cross-process races | HOLD; **do not call real provider** |
| P-M4-04 | M4+M5 | Real provider ↔ SQL TX-P/TX-B crash recovery | Separate integration and SQL authorization required |
| P-M4-05 | DB Coord | Historical audit/backfill and H0–H3 production cutover | HOLD; historical DML forbidden |

## Test evidence requirements

Per case capture test ID, SHA, target instance/database, real transaction scope, expected/actual result, affected synthetic row counts, rollback/commit-ack disposition, cleanup evidence, SQL version, skipped status and M4 fake consume-call counts. Explicitly distinguish `NOT RUN`, `PASS`, `FAIL` and `BLOCKED`. Test output containing 0 matching cases is **NOT** evidence of success.


**Execution status at submission:** Every synthetic SQL test above = `NOT RUN (authorization HOLD)`, every real M4-dependent test = `BLOCKED BY M4 + separate SQL/integration approval`. `20/20` refers solely to code-only xUnit validator tests.
