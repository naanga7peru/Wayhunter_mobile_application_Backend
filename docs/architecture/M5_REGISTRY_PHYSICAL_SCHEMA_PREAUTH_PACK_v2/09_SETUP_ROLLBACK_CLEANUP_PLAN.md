# Exact operation inventory, setup/rollback/teardown — proposed only

**Only inspection + static review are authorized.** None of the `CREATE/ALTER/DROP`, SQL procedure/trigger, DML or integration-test operations below have been performed or approved.

| Phase | Proposed operation (after separate scope approval) | Failure / cleanup rule |
|---|---|---|
| 00 | Independent operator validates host/instance/login, approved disposable DB name and master read-only baseline | STOP if ownership unclear or target already exists |
| 01 | `01_CREATE_DATABASE_REVIEW_ONLY.sql`: exact `CREATE DATABASE` with explicit `SQL_Latin1_General_CP1_CI_AS` collation | Failed/ambiguous create: requery `sys.databases`; NEVER retry blindly |
| 02 | Exact existing `InitialSharedSchema.idempotent.sql` on approved empty test DB only | Stop if baseline doesn't give expected GameAttempts/GameProofEvidence & AK; no shared migration |
| 03 | Capture sys.tables/columns/indexes/keys baseline and exact zero/synthetic row-count expectations; confirm collations | Fail closed on type/index/FK mismatch |
| 04 | `03_REGISTRY_SCHEMA_REVIEW_ONLY.sql`: TX-contained proposed operation table, evidence AK, registry, 5 CHECKs, unique filtered/nonfiltered indexes, 3 composite FKs | `XACT_ABORT` + `TRY/CATCH`, rollback SQL DDL transaction on failure; verify using new connection |
| 05 | Candidate controlled role, 3 triggers, and **later** coordinator-reviewed TX-A/TX-B/TX-C/reconcile procedures; test least privilege | No GRANT if approved procs absent; STOP if bypass possible |
| 06 | Test-only metadata and row-count inspection; two physical SQL connections for concurrency tests | Save sanitized inventories; zero SQL tests until explicit allowlist approved |
| 07 | Test matrix positive/negative cases; fake provider only, deterministic operation IDs | Each test has isolation, fault injection, counts, rollback and no real provider |
| 08 | Reconcile per-ClaimState row counts, receipts, operation and evidence; verify shared DB untouched via approved independent baseline | Missing receipt or ambiguous commits → HOLD; never clear tombstone |
| 09 | Only after distinct written DROP authorization: approved whole-database disposal; verify DB_ID NULL | No DROP approval → retain fenced test DB, report CLEANUP PENDING |

## Rollback matrix

- **Database creation** is not an ordinary transactional DDL step; uncertain result → requery exact DB ID before considering next action. Never drop an existing pre-run database.
- **Registry schema delta** candidate `BEGIN TRAN / SET XACT_ABORT ON / TRY...CATCH` rolls back on failure; if a COMMIT ACK is ambiguous, fresh connection inspects all objects and accepted baseline before any retry.
- **TX-A** register operation + claim = one SQL transaction; injected exception after first insert → rollback both; do not call M4.
- **TX-P** no M4 transaction here; fake outcomes only.
- **TX-B** operation receipt + registry consumed = one M5 transaction; commit ambiguity → reconcile fresh connection; no new consume call.
- **TX-C** evidence + progress + operation completed + claim evidence link = one M5 transaction; rollback all on error, retain upstream M4 consumption receipt; safe idempotent recovery only.
- **Permanent claims/tombstones** cannot be individually deleted or made reusable after a test; only explicitly approved disposal of the entire isolated database may remove synthetic fixture database.
- **Never** use `TRUNCATE`, `DELETE FROM`, `DROP TABLE`, migration `Down()` or unsafe cleanup against shared data.

## Before / after report requirements

Record sanitized target database ID/create time, connection identity, selected fixture IDs (non-sensitive synthetic IDs only), counts by operation state and claim state, operation/claim/evidence linkage, test IDs, DDL object inventory, expected vs actual rollback, test process exit code, and `DB_ID` after disposal. Never log raw canonical media IDs from real M4 or credentials. If disposal is not allowed, mark `CLEANUP PENDING` with residual rows, not zero.
