# Proposed exact operator commands — DO NOT EXECUTE without phase authorization

**Machine read-only baseline already reported by Thuvarathan, Nov 21, 2026:** `(localdb)\MSSQLLocalDB`, SQL product version `17.0.4025.3`, server collation `SQL_Latin1_General_CP1_CI_AS`, proposed test DB **absent**, repo clean. SQLCMD v16 present. Re-verify before each phase: the user has not performed any new SQL action.

Proposed connection string for separate approval (never production):

`Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_M5_Registry_IsolatedTest;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=Wayhunter.M5.Registry.IsolatedSqlTest;Connect Timeout=10;`

Exact SQLCMD flags: `-S "(localdb)\MSSQLLocalDB" -E -b -X -l 10 -d master|Wayhunter_M5_Registry_IsolatedTest -v M5AuthorizedPhase=... -i <reviewed-file>`.

**All commands below are illustrative and intentionally marked NOT AUTHORIZED; do not paste/run.** Phase argument is only a safety fence; it cannot replace a written grant.

From repository root after copying the release directory to `docs\architecture\M5_REGISTRY_EXECUTABLE_TESTONLY_RELEASE_v1\`:

```powershell
# NOT AUTHORIZED -- separate written Phase A required
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -X -l 10 -d master -v M5AuthorizedPhase=A_CREATE -i "docs\architecture\M5_REGISTRY_EXECUTABLE_TESTONLY_RELEASE_v1\01A_CREATE_ISOLATED_DB.sql"
# NOT AUTHORIZED -- separate written Phase B required; run from repository root for :r path
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -X -l 10 -d Wayhunter_M5_Registry_IsolatedTest -v M5AuthorizedPhase=B_BASELINE -i "docs\architecture\M5_REGISTRY_EXECUTABLE_TESTONLY_RELEASE_v1\01B_BASELINE_INITIALIZE.sql"
# NOT AUTHORIZED -- separate written Phase C required
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -X -l 10 -d Wayhunter_M5_Registry_IsolatedTest -v M5AuthorizedPhase=C_SCHEMA -i "docs\architecture\M5_REGISTRY_EXECUTABLE_TESTONLY_RELEASE_v1\02_INSTALL_REGISTRY_SCHEMA.sql"
# NOT AUTHORIZED -- Phase D requires one grant per reviewed guard/procedure/role batch
# NOT AUTHORIZED -- after D: Phase E metadata inspection and synthetic test grants
# NOT AUTHORIZED -- Phase F is a DISTINCT DROP DATABASE grant + recorded DB identity
```

**Pre-execution safety manual gate (each phase):** DB Coordinator signature, exact target instance/database/Windows identity, source commit SHA and SQL file hashes, no shared/prod credentials, no unrelated DB, confirmed no M4 calls, exact SQLCMD batch list, backup or disposal plan, expected row counts, approved time window, abort instructions, output sanitization. Disconnect and stop on errors, unknown commit, mismatched metadata, unexpected DB existence, identity/privilege drift or insufficient authorization.

**B baseline note:** SQLCMD `:r .\src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql` must resolve to the checked-out reviewed repo's file. Verify the exact absolute path and SHA of `InitialSharedSchema.idempotent.sql` before B. DO NOT use `dotnet ef database update`, `EnsureCreatedAsync`, automatic migration or apply any global/shared DDL.

**C schema note:** Index key widths, FK referenced-column names/types/collations and SQL Server metadata verification are STOP criteria. Seven-column principal max is **1,204 declared bytes** (nonclustered ceiling 1,700) but physical acceptance unproven. Physical setup must stop on pre-existing objects; never auto-drop/replace.

**D triggers:** Each CREATE TRIGGER is the first CREATE statement in its own `GO` batch; errors under sqlcmd `:ON ERROR EXIT` stop subsequent batches. Test multi-row update/delete, primary-key rewrite, terminal tombstone release, receipt/evidence mutation and direct bypass before signoff.

**F teardown:** Require independently recorded SQL `database_id`, `create_date`, manifest RunId and owner, no other sessions, archived evidence, and separate coordinator approval. Script does not execute automatically and must never affect shared/production or historical evidence databases.
