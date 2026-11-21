# M5 phase B revised stable-target commands — PREPARATION ONLY

**CURRENT DECISION: Phase B HOLD. Previous single attempt consumed. NOTHING BELOW IS EXECUTION AUTHORIZATION.**

## Allowed: fresh read-only evidence from repo root after the new review files are committed

```powershell
$p = 'docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1'
. "$p\05_READ_ONLY_TARGET_BINDING_PREFLIGHT.ps1"
```

This verifies fresh actual alias-to-pipe binding, critical hashes, SQL/database/fixture identity, empty history, unexpected objects and visible competing SQL sessions. It invokes only `sqllocaldb info MSSQLLocalDB` and read-only SQLCMD scripts. No `sqllocaldb start/stop`, no DDL, no migrations. Existing `00_READ_ONLY_RECOVERY_AUDIT.sql` is called automatically.

**On any error, STOP and paste exact output.** In particular, if `State` is not `Running`, do not auto-start. A stopped LocalDB is not authorization to cause a new start/pipe identity. The script does not promise physical-process exclusivity, which also requires human attestation.

## Proposed future Phase B command — **NOT AUTHORIZED, DO NOT EXECUTE**

```powershell
# Run only after new approval AND immediately successful preflight, from repo root.
# Dot-sourced preflight gives $verifiedPipe, $verifiedServerName, $verifiedInstanceName.
sqlcmd -S "$verifiedPipe" -d "Wayhunter_M5_Registry_IsolatedTest" -E -I -b -V 11 -v "M5AuthorizedPhase=B_BASELINE" "M5PreflightServerName=$verifiedServerName" "M5PreflightInstanceName=$verifiedInstanceName" -i "docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1\01B_BASELINE_RECOVERY_CANDIDATE.sql"
```

The named pipe is acquired through independent read-only inspection of `(localdb)\MSSQLLocalDB` and cross-checked against actual SQL Server identity. The wrapper rejects mismatches and preserves original database ID, `datetime2(3)` creation time, manifest RunId, empty history and schema-inventory guards. The baseline is unmodified and imported via `:r`.

This would consume a *new* explicitly authorized attempt, **only after Kaviyan separately grants it**, and must never be retried automatically after any error or ambiguous result.
