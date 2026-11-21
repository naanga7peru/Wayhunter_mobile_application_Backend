# M5 v2 synthetic verification matrix — all SQL execution HOLD

These cases are **implemented as proposed SQL test dispatcher filters**; they have not run against SQL Server. No test is counted passed until separate exact E authorization and captured result.

| Filter | Test | Pass condition |
|---|---|---|
| S01 | Physical registry inventory | Table, five CHECKs, fake ledger and finalizer proc exist |
| S02 | TX-A success | 1 operation, 1 permanently reserved claim |
| S03 | TX-A injected fault | Operation and registry both roll back |
| S04 | TX-B altered media binding | Fake receipt rejected, no receipt committed |
| S05 | TX-B injected fault | Operation receipt and claim both remain unconfirmed |
| S06 | TX-C stale token | No evidence written |
| S07 | TX-A + fake TX-B + finalizer + TX-C | Evidence, progress, operation and registry commit coherently |
| S08 | Registry multi-row illegal self-update | Rejected; no state drift |
| S09 | Unknown fake receipt | Rejected; cannot infer consumption |
| S10 | TX-C injected fault after evidence insert | Evidence rolled back, progress and op/claim unchanged |
| S11 | Expired finalizer lease | 5s lease, 6s delay, TX-C rejects (no evidence) |
| S12 | Competing finalizer token | Active holder cannot be overwritten |
| S13 | Operation multi-row immutable rewrite | Rejected for both rows |
| S14 | Registry two-tombstone attempted release | Rejected; ownership remains permanent |
| S15 | Evidence two-row UPDATE and DELETE | Both rejected and both original rows preserved |

**Implemented runner:** `12_SYNTHETIC_SQL_TEST_RUNNER.ps1`, with exact `-TestFilter` values above. Without `-Execute` it only prints the catalog. Running it with `-Execute` requires a separate E1 grant, approved commit SHA, baseline SHA-256 and exact authorized identity. The SQL dispatcher creates *synthetic* test users, games, attempts, progress and fake receipts; no history.

**Deliberately not implemented/verified:** actual two-connection simultaneous finalization, deadlock victim, SQL client lost-COMMIT-ack transport fault, real external provider/receipt and full physical trigger positive/negative exercise under external least-privileged token. A synthetic `THROW` inside a SQL transaction is **not** evidence of a lost COMMIT acknowledgement. Before requesting E2 transactional/concurrency execution authorization, submit the distinct two-connection runner, result redaction contract, fault-injection proof and independent Phase E2 grant request.

**Safety caveat:** Some tests use a privileged fixture owner to seed synthetic `Users/Games/Attempts`. They are not a substitute for the external test-principal effective-permission test in `05C`. Test fixtures may persist after execution; no test currently performs cleanup, and separate cleanup approval is necessary before F.
