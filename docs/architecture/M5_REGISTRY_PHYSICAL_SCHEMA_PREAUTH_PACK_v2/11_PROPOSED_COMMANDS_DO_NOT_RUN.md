# Exact proposed commands — **FOR FUTURE WRITTEN APPROVAL ONLY**

**DO NOT COPY THESE INTO A LIVE TERMINAL NOW.** Current files `01`, `03`, `04`, `04B`, `08` are deliberately **inert top-level comment blocks**, not executable SQL. Approval should name release/version/SHA, target, approved SQL principal, allowed scripts/cases, and teardown scope. The DB Coordinator must approve **separate unwrapped executable revisions** before commands for modifying SQL may be run. The baseline existing idempotent script is *not* inert — therefore even greater care.

## Authorized now: code-only and read-only inspection

```powershell
# From local M5 backend repository:
git status -sb
git rev-parse HEAD
git fetch origin develop
git rev-list --left-right --count HEAD...origin/develop
dotnet build --no-restore
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GameProofMediaClaimRulesTests" --verbosity normal
# Only if DB Coordinator's existing read-only approval covers connecting to master:
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -b -i "docs\architecture\M5_REGISTRY_PHYSICAL_SCHEMA_PREAUTH_PACK_v2\06_READ_ONLY_METADATA.sql"
```

## Future proposed SQL execution — every command below is **ON HOLD**

```powershell
# 1. PRE-APPROVED COPY ONLY of the unwrapped reviewed CREATE script,
#    EXACT target (localdb)\MSSQLLocalDB / master; never run comment-only file.
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -b -i "APPROVED_SCRIPT_COPY\01_CREATE_DATABASE.sql"
# 2. Once DB identity confirmed, apply the exact existing baseline script to disposable DB.
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -b -i "src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql"
# 3. Confirm baseline metadata, approved SQL snapshot and independent target checks.
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -b -i "docs\architecture\M5_REGISTRY_PHYSICAL_SCHEMA_PREAUTH_PACK_v2\06B_TEST_DATABASE_METADATA_READ_ONLY.sql"
# 4. Apply separate approved executable schema delta / trigger-batch / procedure release.
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -b -i "APPROVED_SCRIPT_COPY\03_REGISTRY_SCHEMA.sql"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -b -i "APPROVED_SCRIPT_COPY\04_GUARDS_PERMISSIONS.sql"
# 5. Approved synthetic SQL tests only when separately implemented, filtered,
#    individually allowlisted and isolated. No arbitrary --filter guessed here.
# 6. Independent new written permission required before ANY future DROP:
sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -b -i "APPROVED_SCRIPT_COPY\08_TEARDOWN.sql"
```

The `APPROVED_SCRIPT_COPY` file paths are **explicit placeholders**, not files included in the ZIP. Reviewer must not treat this runbook as SQL execution authorization. Do not use `dotnet ef database update`, `EnsureCreatedAsync` or an unfiltered integration suite. For SQL tests, the exact `dotnet test ... --filter ...` command cannot be finalized until an authorized test project/classes exist; record this as a genuine blocker rather than inventing runnable tests.

## Fail-close / stop signals

Unexpected DB exists, wrong instance/name/login, missing GameAttempts principal key, mismatched FK collations, unapproved SQL objects, SQLCMD nonzero exit, unapproved procedure/role changes, historical data present, provider provenance unknown, or missing teardown grant → **STOP and contact Kaviyan**.
