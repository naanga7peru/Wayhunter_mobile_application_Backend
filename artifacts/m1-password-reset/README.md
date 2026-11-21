# M1 password-reset migration review

**Status:** Local candidate for Kaviyan's review; execution approval remains pending. No migration SQL or database update was executed.

## 2026-11-21 post-merge verification

This section supersedes earlier verification metrics where they differ. Earlier results below are retained as historical evidence.

- PR branch: `codex/m1-password-reset-migration`.
- Verified local HEAD: `0388bf2`.
- Included `origin/develop` baseline: `e3b0e93`.
- SDK: `10.0.404` (installed Windows x64 SDK).
- EF CLI: `10.0.15` (repository tool manifest).
- Full Release backend build: passed, 0 errors, 2 xUnit2031 warnings.
- M1 focused unit tests: 471 passed, 0 failed, 0 skipped.
- M3 focused unit tests: 649 passed, 0 failed, 0 skipped.
- Complete unit suite: 3,256 passed, 0 failed, 0 skipped.
- Filtered non-SQL integration tests: 190 passed, 0 failed, 0 skipped. This is not the full SQL integration suite.
- Actual EF CLI migration SQL generated from `20261117093023_InitialSharedSchema` to `20261121100000_AddIdentityPasswordResetCredentials`.
- Original EF-generated SQL SHA256: `61F447E1B23378A9184141A1542928B0152C26242A3F4DA09DC13460F28308FB`.
- Repository SQL artifact SHA256: `80F66CBAD060AE7CB6BCD202590490DCF6DD0A1A8D2D557B47A5ABF89C82D699`.
- Repository artifact differs from the raw EF output only by removal of one extra blank EOF line.
- Generated SQL contains only the password-reset table, its constraints, two indexes, and the EF migration history insert.
- Migration Up/Down and committed snapshot changes are reset-only.
- `has-pending-model-changes` returned exit code 1 on both the M1 branch and isolated `origin/develop` baseline `e3b0e93`.
- Therefore shared-model drift predates this M1 verification. Whether the M1 branch introduces additional drift has not been independently established.
- The detailed drift inventory below was generated at an earlier baseline and must not be represented as a complete current-baseline inventory.
- No SQL migration was executed during this verification.
- No database update or PR merge is authorized by these results.
- Final SQL review, database execution clearance, and PR merge approval remain separate gates.

## Git and EF baseline (historical verification)

- Latest fetched `origin/develop` base: `588867ffd6b39686092de9f84d15cc34e110c3b7` (PR #277 merge).
- Requested proposal commit: `fad5e19cf5f5d036fef122b84a869cdb0860f674`.
- PR branch: `codex/m1-password-reset-migration`. Preserved local commits `2e53a0a` and `1475409`; latest develop was merged without rewriting history in `389adea`. This review update will be committed locally only; nothing was pushed.
- SDK used: .NET `10.0.404`, installed temporarily under `%TEMP%\wayhunter-dotnet-sdk-10`. The selected executable reported `dotnet --version` = `10.0.404`. No `global.json` or SDK pin was added to the repository.
- EF Core / tooling: `10.0.15`.
- Candidate migration ID: `20261121100000_AddIdentityPasswordResetCredentials`.

## Artifact provenance and scope

The reset-only design scaffold started from the committed ModelSnapshot and added only the approved reset mapping. The candidate migration, designer, and current snapshot are kept local. The designer's target-model body matches the current snapshot body; their class and method declarations differ as expected for a migration designer versus a snapshot. The latest snapshot diff against `origin/develop` contains only the reset entity and its `Users` relationship.

The SQL delta was regenerated after the successful .NET 10 build from `20261117093023_InitialSharedSchema` to the candidate migration. It contains no unrelated DDL.

Artifacts:

- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.cs` — migration operations.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.Designer.cs` — target model with the reset entity and `Users` relationship.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/WayhunterSharedDbContextModelSnapshot.cs` — current snapshot with only the reset delta.
- `artifacts/m1-password-reset/WayhunterSharedDbContextModelSnapshot.reset-only.diff` — snapshot diff against latest `origin/develop`.
- `artifacts/m1-password-reset/ef-generated.sql` — EF-generated SQL delta from `InitialSharedSchema` to the reset migration.
- `artifacts/m1-password-reset/unrelated-model-drift.md` — separate full-context drift inventory, with prior-base provenance below.

## Reset table operations and comparison

`Up` contains one `CreateTable` for `IdentityPasswordResetCredentials` and two `CreateIndex` operations. The table has `CredentialId` PK; `UserId nvarchar(256)` FK to `Users.UserId` with no-action behavior; `TokenHash char(64)`; required `CreatedAtUtc` and `ExpiresAtUtc`; nullable `ConsumedAtUtc` and `RevokedAtUtc`; check constraint `[ExpiresAtUtc] > [CreatedAtUtc]`; unique `UX_IdentityPasswordResetCredentials_TokenHash`; and `IX_IdentityPasswordResetCredentials_User_Expiry` on (`UserId`, `ExpiresAtUtc`). `Down` contains only `DropTable` for the reset table.

The generated SQL contains that table, its PK/FK/check, both indexes, and the EF migration-history insert. It has no other tables, columns, indexes, or constraints. `InitialSharedSchema`, its historical designer, and its idempotent SQL are unchanged from latest base.

## Separate schema drift

The full-context drift inventory was scaffolded at prior base `13553911febb44343bf51227c08db470712bf804`. It lists unrelated M1/M2/M3/M8 tables, M2/M3 alternate keys, M3 hazard changes, and M6 voucher/ledger changes. Those changes are excluded from this reset migration and the reset-only snapshot delta. This refresh rechecked the latest committed snapshot diff; a new full-context scaffold was not required to generate the SQL delta and is not represented as current-base drift evidence.

## Commands and results (historical verification)

The .NET 10 SDK was installed to the user temp directory because the machine initially had only SDKs 9.0.301 and 11.0.100. CLI state was redirected to temp to avoid writing first-use sentinels under the profile. Build and test commands:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'wayhunter-dotnet-cli-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$dotnet10 = Join-Path (Join-Path $env:TEMP 'wayhunter-dotnet-sdk-10') 'dotnet.exe'
& $dotnet10 --version
& $dotnet10 build Wayhunter.slnx --no-restore -m:1
& $dotnet10 test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Modules.IdentitySocial|FullyQualifiedName~M3" --logger "console;verbosity=minimal"
& $dotnet10 test Wayhunter.slnx --no-restore -m:1 --logger "console;verbosity=minimal"
```

- SDK confirmation: **10.0.404**.
- Full build: **succeeded**, 0 errors; two xUnit2031 analyzer warnings in `TravelPlanningSqlServiceCollectionExtensionsTests.cs` lines 152 and 154.
- Focused M1/M3 tests: **675 passed, 0 failed, 0 skipped**.
- Full suite, successful elevated rerun: unit **3,239 passed**; integration **210 passed, 85 skipped**; **0 failed**. SQL-dependent opt-in integration tests were skipped. The first non-elevated full-suite attempt hit Windows Event Log permission failures and an M3 WebSocket timeout; the elevated rerun passed.
- EF SQL generation used `dotnet-ef 10.0.15` with the fresh compiled output and exited **0**:

```powershell
dotnet-ef migrations script 20261117093023_InitialSharedSchema 20261121100000_AddIdentityPasswordResetCredentials --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --no-build --output artifacts/m1-password-reset/ef-generated.sql
```

`--no-build` uses the successful build immediately above. No migration SQL was executed.

## Read-only SQL pre-execution verification

Target checked using read-only metadata and `SELECT` queries:

- LocalDB instance: `(localdb)\MSSQLLocalDB`, version `15.0.4382.1`, `Running`, owner `DESKTOP-7K755NU\divik`.
- SQL Server identity: `DESKTOP-7K755NU\LOCALDB#B7B9EF76`.
- Database: `Wayhunter_M1_Verify`, `ONLINE`, created `2026-11-20 12:44:43.090`, owner `DESKTOP-7K755NU\divik`, matching the dedicated LocalDB instance owner. The dedicated Verify name, matching owner, creation date, and baseline-only migration history provide disposable-target evidence.
- Connected login: `DESKTOP-7K755NU\divik`.
- `20261117093023_InitialSharedSchema`: **present**, ProductVersion `10.0.15`.
- `20261121100000_AddIdentityPasswordResetCredentials`: **absent** from `__EFMigrationsHistory`.
- `dbo.IdentityPasswordResetCredentials`: `OBJECT_ID` **NULL**.

Only read-only queries were run. No migration SQL or database update was executed.

## Proposed execution command for review only

```powershell
dotnet ef database update 20261121100000_AddIdentityPasswordResetCredentials --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --connection "Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_M1_Verify;Integrated Security=True;TrustServerCertificate=True"
```

This command is documented for review only; it was not run. Migration execution approval remains pending.