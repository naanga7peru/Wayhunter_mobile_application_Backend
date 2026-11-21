# WAYHUNTER M5 — Physical Registry SQL Pre-Authorization Review Pack v2

**Status:** *STATIC REVIEW ONLY; NO DB CREATE/DDL/DML, SQL REGISTRY TEST EXECUTION, MIGRATION, BACKFILL, M4 PROVIDER ACTIVATION OR PR #256 MERGE.*

**Owner:** Thuvarathan — M5 Games. **Reviewer:** Kaviyan — Team Leader / DB Coordinator. M4 Capture Owner is authority for provider identity, canonicalization and Game-purpose capture compatibility.

**Repo/branch:** `naanga7peru/Wayhunter_mobile_application_Backend` / `task/m5-idempotent-proof-recovery`; PR #256. Accepted Design v3 SHA `411e5c6fcd8559312e732c7039355ca1a5ab5b91`; code-only model/tests reviewed at short SHA `d854a76`.

This pack answers Kaviyan's **ten physical-schema preauthorization requirements** with proposed test-only SQL text, exact source baseline, static compatibility evidence, Windows-integrated connection safety, read-only LocalDB findings, test/cleanup plan, command runbook, and honest status report. See `13_REVIEW_SUBMISSION_CHECKLIST.md` for numbered evidence map.

## Read-first safety

- The mutating SQL files `01`, `03`, `04`, `04B`, `08` are entirely **inside top-level block comments and inert as distributed**; they are review text, **not authorized executable scripts**. A future approved release requires distinct executable scripts, including SQL Server separate trigger batches, and separate written authorization for each SQL operation and eventual database disposal. The `06*` read-only scripts are not mutating.
- `InitialSharedSchema.idempotent.sql` already exists in repo and is **executable** — do not run now. `02_BASELINE_INITIALIZATION.md` lists exact proposed invocation after separate approval. This initialization is only for a *new disposable test DB* and does not authorize shared migration use.
- `Wayhunter_M5_Registry_IsolatedTest` was **not present** in the read-only `sys.databases` output on 2026-11-21. Nothing in this pack creates it. Existing shared/production SQL remains off limits.
- SQL Server 17 key width is a static candidate analysis, not actual physical/FK validation. Operation current EF proposal lacks BIN2 on provider/media columns, while this isolated candidate explicitly uses matching BIN2. Canonical media contract remains M4 HOLD.
- The controlled TX-A/TX-B/TX-C stored-procedure bodies and SQL registry test runner are **not implemented/approved** here; do not claim the end-to-end SQL scenario matrix is runnable or passed.

## Files

1. `01_CREATE_DATABASE_REVIEW_ONLY.sql` — candidate guarded disposable DB creation (inert)
2. `02_BASELINE_INITIALIZATION.md` — existing exact baseline migration script and future conditional invocation
3. `03_REGISTRY_SCHEMA_REVIEW_ONLY.sql` — proposed operation dependency, evidence AK, 16-column registry, 5 CHECKs, all indexes/FKs (inert)
4. `04_REGISTRY_GUARD_AND_PERMISSIONS_REVIEW_ONLY.sql` — proposed role/DENY and registry multi-row trigger (inert)
5. `04B_OPERATION_AND_EVIDENCE_GUARDS_REVIEW_ONLY.sql` — additional immutability guards (inert)
6. `05_SQL_COMPATIBILITY_AND_OPEN_GATES.md` — 1,204-byte index width, SQL version, collation/FK decisions
7. `06_READ_ONLY_METADATA.sql` — safe master inspection, no modifications
8. `06B_TEST_DATABASE_METADATA_READ_ONLY.sql` — future target metadata SELECT, only if authorized target exists
9. `07_ACTUAL_READ_ONLY_BASELINE.md` — user's actual 17.0.4025.3 / collation / DB absent and build/Git evidence
10. `08_TEARDOWN_REVIEW_ONLY.sql` — exact proposed database disposal with safety guards (inert)
11. `09_SETUP_ROLLBACK_CLEANUP_PLAN.md` — explicit operations, stop conditions and cleanup after failures
12. `10_TEST_MATRIX.md` — synthetic SQL versus fake-provider versus M4-required cases
13. `11_PROPOSED_COMMANDS_DO_NOT_RUN.md` — exact proposed SQLCMD commands, clearly held
14. `12_SANITIZED_RESULTS_TEMPLATE.md` — actual format with NOT RUN cases
15. `13_REVIEW_SUBMISSION_CHECKLIST.md` — full ten-item reviewer map

**Decision requested:** REVIEW / CORRECTIONS REQUIRED / HOLD for physical-schema proposal; independently scope future DB creation, baseline, guard/procedure bodies, SQL tests and eventual database teardown. This submission does **not** request immediate SQL execution and does **not** authorize it.
