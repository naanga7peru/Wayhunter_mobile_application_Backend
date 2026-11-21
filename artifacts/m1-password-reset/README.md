# M1 password-reset migration review

**Status:** Local candidate for Kaviyan's review; execution approval remains pending. No migration SQL or database update was executed.

## Git and EF baseline

- Latest fetched `origin/develop` base: `13553911febb44343bf51227c08db470712bf804`.
- Requested proposal commit: `fad5e19cf5f5d036fef122b84a869cdb0860f674`.
- Existing PR branch: `codex/m1-password-reset-migration`. Synchronized with a merge, preserving history; merge commit `58189bf35baac150d00434a6b5b8a00dbe8d66be`. The branch is seven commits ahead of its remote tracking ref; nothing was pushed.
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

The full-context EF scaffold found unrelated pending model changes beyond the reset table. They are excluded from the reset migration/designer/snapshot. See [unrelated-model-drift.md](unrelated-model-drift.md) for the full list: 17 additional M1/M2/M3/M8 tables, M2/M3 alternate keys, M3 hazard changes, and M6 voucher/ledger changes.

## Commands and results

Scratch-only reset scaffold and script generation:

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

- Focused M1/M3 unit tests: **674 passed, 0 failed**. This includes the corrected M3 snapshot assertion.
- Full solution build: **succeeded, 0 warnings, 0 errors**.
- Full suite: unit **3,238 passed**; integration **197 passed, 85 skipped**; **0 failed**. SQL integration-test connection variables were unset, so opt-in database tests skipped. No test wrote to `Wayhunter_M1_Verify`.
- EF SQL script generation: succeeded. Script inspected: only reset table create, two indexes, and migration-history insert; no unrelated objects.

## Read-only SQL pre-execution verification

Target checked after the full suite:

- LocalDB instance: `(localdb)\MSSQLLocalDB`, LocalDB `15.0.4382.1`, `Running`, owner `DESKTOP-7K755NU\divik`.
- SQL Server identity: `DESKTOP-7K755NU\LOCALDB#B7B9EF76`.
- Database: `Wayhunter_M1_Verify`, `ONLINE`, created `2026-11-20 12:44:43.090`, owner `DESKTOP-7K755NU\divik` (same as the dedicated LocalDB instance owner). The `Verify` database name, owner, create time, and sole recorded baseline migration are the disposable-target evidence.
- Connected login: `DESKTOP-7K755NU\divik`.
- `20261117093023_InitialSharedSchema`: **present**, ProductVersion `10.0.15`.
- `20261121100000_AddIdentityPasswordResetCredentials`: **absent** from `__EFMigrationsHistory`.
- `dbo.IdentityPasswordResetCredentials`: `OBJECT_ID` **NULL**.
- Migration history table exists; read-only lookup returned only the initial migration among the two checked IDs.

Only `SELECT`/metadata queries were run. No migration SQL or database update was executed.

## Proposed execution command for review only

```powershell
dotnet ef database update 20261121100000_AddIdentityPasswordResetCredentials --project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --startup-project src/Wayhunter.Infrastructure/Wayhunter.Infrastructure.csproj --connection "Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_M1_Verify;Integrated Security=True;TrustServerCertificate=True"
```

This command is documented for Kaviyan's review only; it was not run. Remaining approval: Kaviyan/DB Coordinator must explicitly approve migration execution.
