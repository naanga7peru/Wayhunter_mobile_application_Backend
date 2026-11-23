# WAYHUNTER M5 Registry — Executable test-only correction candidate v2

**Review only; all SQL phases A/B/C/D/E/F HOLD; PR #256 DO NOT MERGE.** This is a corrected proposal responding to the DB Coordinator's eight required corrections for the v1 release at commit `b02a6ee443f3c4144db7731546933e4507eab26c`. No files were executed against a database during preparation. SQL Server compilation, physical compatibility, transactional integrity, privileges and recovery behavior remain unverified.

## Changes from v1

1. **A/F destructive batch fix:** exact hard-coded CREATE/DROP wrapped in `EXEC(N'...')`, after phase/login/LocalDB/master/manifest safeguards **within one batch**, with no `GO` separating guard from destructive statement. F also requires explicit disposal consent, recorded ID/create time, zero residual rows and no other sessions.
2. **TX-C fencing:** `04F_CLAIM_FINALIZER` acquires a current state-3 lease/token; TX-C requires the matching token and `LeaseExpiresAtUtc > serverNow`, checks atomically and consumes token. Trigger allows only tightly bounded 3→3 finalizer lease acquisition, then 3→4 completion. Stale/expired/other token tests S06/S11/S12.
3. **TX-B synthetic receipt binding:** `02B` separate fake ledger; TX-B validates durable synthetic attestation bound to exact operation, game, attempt, step, player, capture, namespace and canonical asset. S04/S09 cover negative binding. Real M4 consumption remains unverified/unauthorized.
4. **Trigger positive/negative transitions:** operation guard revised for state 3→3 and 3→4, registry guard retains 0→1→2/0→3, evidence append-only unchanged; S07 positive lifecycle, S08/S13/S14 multirow forbidden updates. Actual SQL execution pending.
5. **External test identity:** `05B` proposes an approved external Windows login as a database user and restricted role member. `05C` must run under the **actual external login session** to verify effective permissions (not `EXECUTE AS`), and stops on sysadmin/db_owner privileges. Actual external login not yet approved or connected.
6. **Baseline SHA:** `14_BASELINE_MIGRATION_IMMUTABLE_SHA_REVIEW.md` records exact source commit and Git blob SHA-1; local SHA-256 intentionally remains PENDING until checked out and approved.
7. **Develop + build evidence:** refresh `git fetch origin develop`, `git rev-list --left-right --count HEAD...origin/develop`, build and focused tests **after local v2 staging and again at final commit**. Latest user-reported reference before this release: develop `77d1f57`, 20 ahead/8 behind; build passed with 2 unrelated xUnit2031 warnings; focused rules 20/20. Do not claim results at the new v2 commit without rerun.
8. **Synthetic SQL runner:** `12` + `13` implement S01–S15 filters, bounded synthetic fixture writes and injectable SQL faults at TX-A/B/C. Exact `-TestFilter` named cases in `10_TEST_MATRIX.md`; true COMMIT ack loss and multi-connection contention are explicitly NOT YET IMPLEMENTED (separate E2 code/approval required).

## Stop conditions (not a test authorization)

- Current approved test DB **was absent** at last read-only report. Do not create it until separate written Phase A approval.
- Baseline script hash/physical layout mismatch; column collation mismatch; missing alternate keys; index-width objection; non-localDB connection; external login not tested; unclear M4 identity; unexpected test rows; stale lease; unknown receipt/commit outcome → **STOP/HOLD**, no automatic repairs or production activation.
- The 7-column composite key has a declared maximum 1,204 bytes against modern SQL Server's 1,700-byte nonclustered-key limit. Actual LocalDB FK/column-collation and index creation remain untested.
- A/F hardening addresses normal SQLCMD batch boundary bypass in those exact files; a privileged actor could still manually extract SQL. Written approval is the controlling boundary.

## Static review and next submission

Read `09_PHASE_COMMANDS_AND_SAFETY.md`, `10_TEST_MATRIX.md`, `14_BASELINE_MIGRATION_IMMUTABLE_SHA_REVIEW.md`, `15_V2_CORRECTIONS_AND_OPEN_GATES.md`. Stage the package in a documentation-only branch, run `git diff --cached --check`, `dotnet build --no-restore` and 20 focused unit tests. Calculate local baseline SHA-256, current `origin/develop`, exact new commit SHA and release SHA256 manifest before asking for **review of specific A and B phases**, never blanket execution.
