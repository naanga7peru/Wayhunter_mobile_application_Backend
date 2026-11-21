# Phase B recovery — exact commands for coordinator review

**NO SQL EXECUTION APPROVAL.** The current isolated database (reported database ID **7**) must be preserved. The reviewed baseline source is **not** changed. No Phase A retry, Phase C, DROP, DELETE, or schema repair.

## Read-only preflight (within already authorized inspection scope)

From the **repository root**:

```powershell
$pack = 'docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1'
$baseline = 'src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql'
git status -sb
git hash-object -- $baseline
(Get-FileHash -Algorithm SHA256 -LiteralPath $baseline).Hash
$hashes = Get-Content "$pack\SHA256_MANIFEST.json" -Raw | ConvertFrom-Json
$actualAuditHash = (Get-FileHash -Algorithm SHA256 -LiteralPath "$pack\00_READ_ONLY_RECOVERY_AUDIT.sql").Hash
$actualWrapperHash = (Get-FileHash -Algorithm SHA256 -LiteralPath "$pack\01B_BASELINE_RECOVERY_CANDIDATE.sql").Hash
[pscustomobject]@{AuditHashMatches=($hashes.'00_READ_ONLY_RECOVERY_AUDIT.sql' -eq $actualAuditHash); WrapperHashMatches=($hashes.'01B_BASELINE_RECOVERY_CANDIDATE.sql' -eq $actualWrapperHash)}
Select-String -LiteralPath $baseline -Pattern '^\s*(USE\b|:CONNECT\b|:r\b|SET\s+QUOTED_IDENTIFIER\s+OFF\b)'
```

Expected baseline Git **blob SHA-1**: `90a193505e8efe0e680ec2c94cae4d2186b3d87e`.
Previously observed Windows baseline **file SHA-256**: `74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724`.

These hashes use different algorithms and are **not interchangeable**. If the baseline hash, wrapper hash or source-directive audit differs, **STOP** and obtain coordinator review; do not rewrite/normalize baseline. The source-pattern search should have **no matches**. It is a review aid, not a general SQL parser.

After checking file hashes, the following is a **read-only SQL audit** and may be run only within the existing approved read-only inspection scope:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -I -b -V 11 -i "$pack\00_READ_ONLY_RECOVERY_AUDIT.sql"
```

Required result: exact LocalDB, operator `UNICOMTIC\ut011148`, database ID 7, original fixture RunId `F42D6B20-B250-4F93-AB07-0478C2FA8E85`, original database `create_date` recorded, only the fixture manifest and empty migration history with their PKs, and **no other user objects/indexes/FKs/checks/defaults/triggers**. On any error, STOP and share the SQL error without attempting any repair.

## Proposed Phase B recovery invocation — **NOT AUTHORIZED / DO NOT RUN**

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -I -b -V 11 -v "M5AuthorizedPhase=B_BASELINE" -i "docs\architecture\M5_PHASE_B_RECOVERY_PREFLIGHT_v1\01B_BASELINE_RECOVERY_CANDIDATE.sql"
```

**This command actually executes the baseline and changes the database.** It is included solely for the Coordinator's review. Do not use without a *new, written, precise Phase B recovery grant* identifying operator, target database, reviewed commit+wrapper hash, baseline SHA-256, and the isolated scope. The `-v` variable **is not approval**. Run from repository root to ensure `:r .\src\...` imports the correct baseline.

`-I` enables quoted identifiers for the sqlcmd connection; the wrapper separately sets `QUOTED_IDENTIFIER ON` and the remaining required SET options before the SQLCMD `GO`/`:r` import. A `GO` statement sends a batch; it does **not** reset connection SET options. **Do not add `-X`** to this proposal without verifying its behavior with script commands and variables (`:r`, `-v`). Do not execute the original v2 `01B_BASELINE_INITIALIZE.sql`, since it rejects an already-created but empty `__EFMigrationsHistory` table.

## Fail-closed conditions

- Database missing, renamed, not LocalDB, not ID 7, wrong operator, altered `create_date` or fixture RunId → STOP.
- `__EFMigrationsHistory` missing, **nonempty**, or wrong columns → STOP.
- Unexpected table, view, trigger, constraint, user object, non-PK index, or partially created schema → STOP.
- Wrong baseline SHA-256/blob, source directive, wrapper hash, session SET options, or absent written approval → STOP.
- SQL execution failure, ambiguous COMMIT, missing postflight `Games`/`GameAttempts`/`GameProofEvidence`/`GameAttemptProgress`, or missing migration history marker → STOP. Preserve the DB, log the error, and request fresh recovery review.

The expected *future* successful Phase B history is **exactly one** row: `MigrationId=20261117093023_InitialSharedSchema`, `ProductVersion=10.0.15`. The expected baseline tables include `Users`, `Games`, `GameAttempts`, `GameAttemptProgress`, `GameProofEvidence`, and `GameSteps`. Do not claim success before observed output. Phase C remains separate and HOLD.
