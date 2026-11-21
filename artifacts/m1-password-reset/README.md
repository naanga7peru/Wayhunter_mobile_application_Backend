# M1 password-reset migration review

**Status:** Local candidate for Kaviyan's review; execution approval remains pending. No migration SQL or database update was executed.

## Git and EF baseline

- Latest fetched `origin/develop` base: `77d1f57ee717e012efc2d7dca1ad9710de28020b` (PR #275 merge).
- Requested proposal commit: `fad5e19cf5f5d036fef122b84a869cdb0860f674`.
- Existing PR branch: `codex/m1-password-reset-migration`. Saved the generated review artifacts in local commit `2e53a0af` then merged latest develop without rewriting history; merge commit `ac37f88`. The branch is fifteen commits ahead of its remote tracking ref; nothing was pushed.
- Separate scratch worktree: `C:\Users\divik\WAYHUNTER\Wayhunter_mobile_application_Backend\.scratch-ef`, detached at the exact base SHA above.
- EF Core / tooling: `10.0.15`.
- Candidate migration ID retained for the pre-execution check: `20261121100000_AddIdentityPasswordResetCredentials`.

## Artifact provenance and scope

The scratch worktree used a design-only context that seeded the latest committed ModelSnapshot and added only the approved reset mapping. EF scaffolding emitted the reset `Up`/`Down`, target model, and snapshot. The generated migration timestamp was renamed to the already-approved candidate ID above. EF's shadow-model conventions added `ValueGeneratedOnAdd` annotations to unrelated baseline keys in its raw snapshot; those unrelated annotations were filtered out so the checked-in candidate designer and ModelSnapshot are exactly the latest committed snapshot plus the EF-generated reset entity and relationship.

EF then generated the SQL delta from `InitialSharedSchema` to the candidate migration. The final script was regenerated from the compiled PR-branch migration after the full solution build. The candidate migration contains only one `CreateTable`, its PK/FK/check, and the two mapped indexes. `Down` contains only `DropTable` for `IdentityPasswordResetCredentials`.

Artifacts:

- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.cs` — generated operations.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/20261121100000_AddIdentityPasswordResetCredentials.Designer.cs` — baseline target model plus reset entity and `Users` relationship.
- `src/Wayhunter.Infrastructure/Persistence/Migrations/WayhunterSharedDbContextModelSnapshot.cs` — current snapshot with only that reset-table delta.
- `artifacts/m1-password-reset/WayhunterSharedDbContextModelSnapshot.reset-only.diff` — snapshot diff against `origin/develop` at the exact base SHA.
- `artifacts/m1-password-reset/ef-generated.sql` — EF-generated SQL delta from `20261117093023_InitialSharedSchema` to `20261121100000_AddIdentityPasswordResetCredentials`.
- `artifacts/m1-password-reset/unrelated-model-drift.md` — separate full-context drift inventory.

The earlier `20261121100000_AddIdentityPasswordResetCredentials.from-InitialSharedSchema.sql` file is retained as historical scratch output; use `ef-generated.sql` as the generated review script.

## Reset table operations

`Up` adds `IdentityPasswordResetCredentials` with `CredentialId` PK; `UserId nvarchar(256)` FK to `Users.UserId` with `NO ACTION`; `TokenHash char(64)`; required `CreatedAtUtc` and `ExpiresAtUtc`; nullable `ConsumedAtUtc` and `RevokedAtUtc`; expiry check `[ExpiresAtUtc] > [CreatedAtUtc]`; unique `UX_IdentityPasswordResetCredentials_TokenHash`; and `IX_IdentityPasswordResetCredentials_User_Expiry` on (`UserId`, `ExpiresAtUtc`). `Down` drops only this table.

`InitialSharedSchema`, `20261117093023_InitialSharedSchema.Designer.cs`, and `InitialSharedSchema.idempotent.sql` are unchanged from the latest base.

## Separate schema drift

The full-context EF scaffold at the prior base `13553911febb44343bf51227c08db470712bf804` found unrelated pending model changes beyond the reset table. They are excluded from the reset migration/designer/snapshot. See [unrelated-model-drift.md](unrelated-model-drift.md) for that inventory: 17 additional M1/M2/M3/M8 tables, M2/M3 alternate keys, M3 hazard changes, and M6 voucher/ledger changes. On the latest base, the committed snapshot diff was rechecked and remains limited to the reset entity and relationship. A fresh full-context drift scaffold was not completed because the compiler failed before current-model scaffolding could run; the inventory above is retained with its earlier provenance.

## Commands and results

Scratch-only reset scaffold and script generation (completed against the prior base):

```powershell
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~IdentityMigrationReadinessTests"
dotnet ef migrations add AddIdentityPasswordResetCredentials --context ResetOnlyDesignTimeContext --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --output-dir Persistence/Migrations --no-build
dotnet ef migrations script 20261117093023_InitialSharedSchema 20261121100000_AddIdentityPasswordResetCredentials --context ResetOnlyDesignTimeContext --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --no-build --output artifacts/m1-password-reset/reset-generated.sql
```

The scratch M1 compile/readiness command passed **5/5**. `dotnet ef migrations add` and `dotnet ef migrations script` completed successfully. The separate full-context drift scaffold was inspected and removed from scratch; its findings are reported separately.

PR-branch build and tests:

```powershell
dotnet restore Wayhunter.slnx --disable-parallel -m:1 -p:NuGetAudit=false --ignore-failed-sources
dotnet build Wayhunter.slnx --no-restore -m:1
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Modules.IdentitySocial|FullyQualifiedName~M3"
dotnet test Wayhunter.slnx --no-restore -m:1 --logger "console;verbosity=minimal"
```

- On prior base `13553911febb44343bf51227c08db470712bf804`, focused M1/M3 unit tests passed **674/674**; full build succeeded with **0 warnings, 0 errors**; full suite had unit **3,238 passed**, integration **197 passed, 85 skipped**, and **0 failed**. These are prior-base results, not results for the latest merged tip.
- On latest base `77d1f57ee717e012efc2d7dca1ad9710de28020b`, restore completed (all projects up-to-date). Full build, focused test command, and full test command were each attempted but blocked before compilation/test execution by Roslyn `MSB3883` access denied to a `\\.\pipe\LOCAL\dotnet_<id>` named pipe. Both tests therefore have **no current-base result**.
- `dotnet ef migrations script 20261117093023_InitialSharedSchema 20261121100000_AddIdentityPasswordResetCredentials --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --no-build --output artifacts/m1-password-reset/ef-generated.sql` exited **0**. It refreshed the script using the existing compiled migration assembly because the latest source build was blocked. The SQL contains only reset-table create, primary key, FK, expiry check, two indexes, and migration-history insert; no unrelated DDL.
- The latest-base snapshot diff is only the reset entity and relationship. The candidate migration source still has one `CreateTable` with its PK/FK/check, two indexes, and only a `DropTable` in `Down`. Initial migration artifacts remain unchanged.

## Read-only SQL pre-execution verification

Target checked read-only after the latest-base build/test attempts:

- LocalDB instance: `(localdb)\MSSQLLocalDB`, LocalDB `15.0.4382.1`, `Running`, owner `DESKTOP-7K755NU\divik`.
- SQL Server identity: `DESKTOP-7K755NU\LOCALDB#B7B9EF76`.
- Database: `Wayhunter_M1_Verify`, `ONLINE`, created `2026-11-20 12:44:43.090`, owner `DESKTOP-7K755NU\divik` (same as the dedicated LocalDB instance owner). The `Verify` database name, owner, create time, and sole recorded baseline migration are the disposable-target evidence.
- Connected login: `DESKTOP-7K755NU\divik`.
- `20261117093023_InitialSharedSchema`: **present**, ProductVersion `10.0.15`.
- `20261121100000_AddIdentityPasswordResetCredentials`: **absent** from `__EFMigrationsHistory`.
- `dbo.IdentityPasswordResetCredentials`: `OBJECT_ID` **NULL**.
- Migration history table exists; read-only lookup returned only the initial migration among the two checked IDs. The repeated latest check returned the same database identity, owner, creation date, migration history, and NULL table lookup.

Only `SELECT`/metadata queries were run. No migration SQL or database update was executed.

## Proposed execution command for review only

```powershell
dotnet ef database update 20261121100000_AddIdentityPasswordResetCredentials --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --connection "Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_M1_Verify;Integrated Security=True;TrustServerCertificate=True"
```

This command is documented for Kaviyan's review only; it was not run. Remaining approval: Kaviyan/DB Coordinator must explicitly approve migration execution.
