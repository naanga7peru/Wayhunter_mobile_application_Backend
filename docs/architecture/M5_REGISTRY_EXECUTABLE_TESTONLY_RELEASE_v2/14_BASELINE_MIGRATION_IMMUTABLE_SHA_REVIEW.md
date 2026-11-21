# InitialSharedSchema exact-source and SHA verification gate — no SQL execution

**Authoritative source path:** `src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql`.

**Repository/source commit inspected via connected GitHub:** `b02a6ee443f3c4144db7731546933e4507eab26c`.

**Git *blob* SHA-1 (exact Git object identity):** `90a193505e8efe0e680ec2c94cae4d2186b3d87e`.

Source reviewed: migration identifier `20261117093023_InitialSharedSchema`, SQLCMD `GO` batches, guarded `__EFMigrationsHistory`, existing `GameAttempts` alternate key `AK_GameAttempts_Id_Game_Player`, `GameProofEvidence` with primary key and `AK_GameProofEvidence_Id_Attempt`, and required `Users`, `Games`, `GameSteps`, `GameAttempts`, `GameAttemptProgress` tables. The baseline does **not** create the proposed M5 registry table, M5 operation table or synthetic receipt ledger. The additional four-column evidence alternate key is test-only C_SCHEMA proposal.

**Do not substitute a SHA-1 Git blob hash for a SHA-256 file hash.** The local Windows checkout's `Get-FileHash -Algorithm SHA256` result is still **not recorded** and must be read, compared and approved before any Phase B authorization. It may differ if checked-out bytes use different line endings. If so stop and reconcile exact bytes; do not normalize a migration file casually.

Read-only commands *for the developer's Windows repo* (do not execute any SQL):

```powershell
git rev-parse HEAD
git hash-object -- "src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql"
Get-FileHash -Algorithm SHA256 -LiteralPath "src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql"
git diff -- "src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql"
```

Exact expected **Git object** hash at reviewed commit: `90a193505e8efe0e680ec2c94cae4d2186b3d87e`. Save the complete SHA-256 as immutable Phase B gate value in the coordinator review, then check it again immediately before any authorized baseline execution. **Actual Phase B SQL execution remains HOLD.**
