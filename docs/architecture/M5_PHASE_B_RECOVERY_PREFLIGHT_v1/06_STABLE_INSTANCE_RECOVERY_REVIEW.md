# M5 PR #256 — Revised Phase B LocalDB stable-binding recovery proposal v2

**STATUS: PROPOSAL / READ-ONLY ONLY. PHASE B HOLD.** Prior single-attempt authorization was consumed by SQL error 59220; NO retry is authorized. Reviewed base commit: `3b13daf12ad2504d2a9144378e329a84abf33d8e`.

## Cause: do not freeze the transient LocalDB# suffix

Microsoft documents `MSSQLLocalDB` as the user's automatic LocalDB logical instance, and its named-pipe suffix after `LOCALDB#` **changes each time the LocalDB instance is started**. The read-only records first showed `UNICOMTIC48\LOCALDB#00357BA9`, then `UNICOMTIC48\LOCALDB#6CD03817`. This is **consistent with** an instance restart; it is **not proof of why that restart happened**. Do not claim a measured restart cause or timestamp absent actual LocalDB logs.

Sources: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver17 and https://learn.microsoft.com/en-us/sql/tools/sqllocaldb-utility?view=sql-server-ver17

## Proposed stable binding (layered, fail closed)

1. Use **read-only** `sqllocaldb info MSSQLLocalDB` to verify the logical instance `MSSQLLocalDB`, its owner `UNICOMTIC\ut011148`, `Running` state, and the current per-start named pipe `np:\\.\pipe\LOCALDB#XXXXXXXX\tsql\query`. No auto-start/stop/create/delete.
2. Connect **read-only** via both `(localdb)\MSSQLLocalDB` and the newly discovered pipe. Require both to yield identical SQL Server/registered/instance/machine identity, operator, DB name, ID 7, create_date `2026-11-21 14:56:24.330`, exact manifest, empty migration history, QUOTED_IDENTIFIER ON, sysadmin session visibility and zero other target sessions. Compare the runtime ID with the pipe suffix and ensure instance/machine/server consistency. Reject null/malformed/extra output.
3. Repeat `sqllocaldb info` after probes and again after independent, existing read-only catalog audit. A pipe/instance change during verification causes immediate STOP.
4. A **future separately authorized** single attempt would connect to the **validated physical named pipe**, not a re-resolving alias, to pin the execution target. The SQL wrapper accepts `M5PreflightServerName` and `M5PreflightInstanceName` from validated preflight (not hand-guessed or hardcoded) and rejects a mismatch, unknown LocalDB runtime shape, machine change, wrong operator, DB ID/name/create_date, manifest, nonempty migration history or unexpected objects.
5. The original baseline remains byte-for-byte unchanged. The `-I`, `-b`, `-V 11`, `-E` SQLCMD switches, `:ON ERROR EXIT`, indexed-computed-column SET options, original preflight, required SQL migration-history marker and table postflight are preserved.

A dynamic runtime identifier is accepted **only through fresh independent alias+pipe verification**; it cannot silently bypass the rest of the immutable DB/operator/fixture/schema checks. Pipe binding limits restart races: a stale physical pipe should fail connection rather than silently reconnecting a new `LOCALDB#` process. Still, any failure, timeout or interrupted/ambiguous outcome requires STOP and coordinator review. Preflight queries alone do not establish indefinite exclusivity; the operator must close test runners and explicitly attest no competing processes before a future execution.

## Fresh read-only evidence — required from actual operator

The old observed identities and post-error results are previously reported, **not fresh evidence for this revised proposal**. Run from repository root after committing the review files:

```powershell
$p = 'docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1'
. "$p\05_READ_ONLY_TARGET_BINDING_PREFLIGHT.ps1"  # READ ONLY; no Phase B execution
```

The script checks the clean reviewed working tree, raw SHA-256 manifest (all payloads), authoritative baseline SHA-256 + git blob, SQLCMD identity via both endpoints, catalog, original fixture, exact creation timestamp, empty history and visible competing sessions. Record the entire output and exact exit/error on failure. Do not weaken regexes, skip validation, restart LocalDB, or modify any DB object to make it pass. A localized `sqllocaldb info` output not matching the parser is STOP + manual review, not permission to approximate.

**Remaining before any future execution (not authorized):** coordinator review of revised commit + wrapper hash, renewed written phase-specific single-attempt approval, **fresh** read-only proof immediately before execution, exclusive operator-control attestation (including no test processes in flight), and immutable baseline validation. If any step fails, STOP without invoking SQL mutating command.

## Proposed future command — UNAUTHORIZED / DO NOT RUN

**Only if renewed coordinator authorization explicitly approves named-pipe targeting, the revised wrapper SHA, the commit, and preflight steps:**

```powershell
# The following values are populated only by successful dot-sourced read-only preflight.
# $verifiedPipe, $verifiedServerName, $verifiedInstanceName
# UNAUTHORIZED EXAMPLE BELOW: DO NOT RUN.
sqlcmd -S "$verifiedPipe" -d "Wayhunter_M5_Registry_IsolatedTest" -E -I -b -V 11 -v "M5AuthorizedPhase=B_BASELINE" "M5PreflightServerName=$verifiedServerName" "M5PreflightInstanceName=$verifiedInstanceName" -i "docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1\01B_BASELINE_RECOVERY_CANDIDATE.sql"
```

This command would run the unchanged baseline and mutate the test DB. It is supplied for coordinator inspection only; no new single-attempt approval exists. Dot-sourced preflight variables are ephemeral and must be refreshed for any eventual approved attempt. Command arguments must not be edited or manually populated to bypass a failed preflight. A separate approval must cover the change from logical SQLCMD alias to its independently verified named pipe.

## Immutable recovery identity & restrictions

- Logical target: `(localdb)\MSSQLLocalDB`; physical pipe: fresh from `sqllocaldb info MSSQLLocalDB` and cross-verified against live SQLCMD alias (never hardcoded).
- Server machine: `UNICOMTIC48`; operator/fixture CreatedBy: `UNICOMTIC\ut011148`.
- Database name: `Wayhunter_M5_Registry_IsolatedTest`; database ID: `7`.
- `sys.databases.create_date` at `datetime2(3)`: `2026-11-21T14:56:24.330`.
- Fixture RunId: `F42D6B20-B250-4F93-AB07-0478C2FA8E85`.
- Required state: exactly two expected tables and their PKs, empty `__EFMigrationsHistory`, no unexpected schema objects, indexes or constraints.
- Baseline Git blob: `90a193505e8efe0e680ec2c94cae4d2186b3d87e`.
- Baseline raw SHA-256: `74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724`.
- Phase B **HOLD**; Phase C **HOLD**; database repair/deletion/recreation **HOLD**; shared/production migration **HOLD**; M4 integration **HOLD**; PR #256 merge **HOLD**.

## Evidence template (fill ONLY after actual read-only execution)

- Corrected proposal Git commit: **pending actual commit**.
- Corrected wrapper raw SHA-256: see updated manifest; validate local file.
- Operator workstation date/time and repo root: **not yet observed**.
- `sqllocaldb info MSSQLLocalDB`: **pending** (record full output).
- Alias SQLCMD binding row: **pending**.
- Named pipe SQLCMD binding row: **pending**.
- Final repeated pipe check: **pending**.
- Read-only inventory audit: **pending**.
- SQLCMD/read-only PowerShell exit status: **pending**.
- Human attestation of no competing test processes: **pending**.
- Phase B/Phase C database changes: **NONE AUTHORIZED**.
