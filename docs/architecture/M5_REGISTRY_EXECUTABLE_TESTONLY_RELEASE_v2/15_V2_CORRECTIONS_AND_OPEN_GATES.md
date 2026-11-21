# Eight DB Coordinator corrections — v2 evidence crosswalk

| # | Correction | Proposed v2 files | Evidence state |
|---|---|---|---|
| 1 | A CREATE / F DROP batch boundary safety | `01A`, `08`; same guarded batch, no GO; hard-coded target and manifest; explicit F flag | Static file inspection only; SQL execution HOLD |
| 2 | TX-C finalizer token/lease and stale/expired/races | `04D`, new `04F`, updated `03B`; S06/S11/S12 | Sequential negative tests authored; two-connection contention not run/fully implemented |
| 3 | Bound synthetic TX-B receipt | `02B`, updated `04C`; S04/S09 | Synthetic ledger and immutable binding comparisons authored; real M4 not approved |
| 4 | Trigger consistency + multirow | `03A/B/C`, S07/S08/S13/S14 | Code review; SQL Server trigger behavior not verified |
| 5 | External effective permissions | `05B` external Windows login bind, `05C` connected identity probe | Real external principal not yet named/connected; proof pending |
| 6 | Exact InitialSharedSchema source SHA | `14_BASELINE_MIGRATION_IMMUTABLE_SHA_REVIEW.md` | Git blob SHA recorded, local SHA-256 pending developer output |
| 7 | Develop + final build/test refresh | `11_REVIEW_EVIDENCE.md`, Windows commands below | User must run; previous evidence is pre-v2 only |
| 8 | SQL test runner + fault injections | `12` list/gated run, `13` cases, `04A/C/D` fault points | Test code prepared but not executed; real ack-loss/concurrent runner pending |

## No-go gates for executable approval

Phase A requires reviewer sign-off on an exact external Windows login or named isolated owner, database identity, and audited script hashes. Phase B requires the actual local SHA-256 and exact InitialSharedSchema migration bytes. Phase C/D require test-only schema/guard/procedure static review and physical stop conditions. Phase E1 requires separately authorized tests and actual external-identity permission validation. Phase E2 requires two-connection/cross-Game contention and client-side unknown COMMIT acknowledgment harness before requesting concurrency approval. F requires cleanup approval plus explicit zero-row/database-identity proof.

Read-only Windows commands (from M5 repo root; **do not execute SQL**):

```powershell
git status -sb
git fetch origin develop
git rev-list --left-right --count HEAD...origin/develop
git hash-object -- "src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql"
Get-FileHash -Algorithm SHA256 -LiteralPath "src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql"
dotnet build --no-restore
dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GameProofMediaClaimRulesTests" --verbosity normal
```

**Do not run:** sqlcmd, `dotnet ef database update`, `EnsureCreated`, SQL tests, CREATE/DROP, provider calls or shared migrations without explicit phase authorization.
