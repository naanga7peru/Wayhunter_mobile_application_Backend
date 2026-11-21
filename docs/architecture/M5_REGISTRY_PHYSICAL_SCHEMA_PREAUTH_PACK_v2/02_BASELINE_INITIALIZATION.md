# Exact proposed baseline initialization — **NOT AUTHORIZED**

Baseline path already present in the backend repository, **do not edit this existing file**:

`src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql`

Its migration ID is `20261117093023_InitialSharedSchema`. Repository static inspection of the feature branch confirmed the source contains the `GameAttempts` and `GameProofEvidence` tables and the proposed attempt-owner alternate-key name. It is a *shared-schema-shaped* script; running a copy **inside a newly approved disposable database only** would initialize a synthetic baseline for M5, not apply a production/shared migration. DB Coordinator must review the whole existing script and its dependencies before approving even this isolated execution.

**After separate written authorization only**, proposed sequence from backend repository root:

1. Create database via separately approved executable release of `01_CREATE_DATABASE_REVIEW_ONLY.sql` (currently an inert review comment).
2. Confirm `DB_ID(N'Wayhunter_M5_Registry_IsolatedTest')` and approved Login/Instance; stop if it already existed before setup.
3. Use this **exact proposed** baseline command (NOT RUN NOW):

   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Wayhunter_M5_Registry_IsolatedTest" -E -b -i "src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql"
   ```

4. Verify `dbo.__EFMigrationsHistory` row `20261117093023_InitialSharedSchema` and required table/key metadata using the read-only inventory in `06_READ_ONLY_METADATA.sql`; counts and object metadata must match coordinator-approved fixture expectations. Do **not** use `dotnet ef database update`, `EnsureCreatedAsync`, app startup migrations, or automatic schema updates.
5. Review physical PK, alternate keys, SQL type lengths, `sys.columns.collation_name`. Only then consider separately approved exact registry delta in `03_REGISTRY_SCHEMA_REVIEW_ONLY.sql`.

**Blockers:** schema source is not independently executed in this work; whether its historical M3 mappings are current is a separate project issue, and only M5-required objects may be assumed from source inspection. Script execution, row-count baselines and physical FK checks all remain **NOT RUN / HOLD**.
