# Actual environment and source status evidence — 21 November 2026

**Source:** Local PowerShell and `sqlcmd` outputs supplied by Thuvarathan. This is an observed read-only baseline, **not** a registry SQL test.

| Fact | Observed result |
|---|---|
| LocalDB instance name | `MSSQLLocalDB` |
| `sqllocaldb info MSSQLLocalDB` before SQL command | Version `17.0.4025.3`; State `Stopped`; owner `UNICOMTIC\\ut011148` |
| `sqlcmd` connection | `-S "(localdb)\MSSQLLocalDB" -d master -E -b` succeeded |
| `SERVERPROPERTY('ProductVersion')` | `17.0.4025.3` |
| `SERVERPROPERTY('Collation')` | `SQL_Latin1_General_CP1_CI_AS` |
| `sys.databases` target row | **No row** for `Wayhunter_M5_Registry_IsolatedTest` |
| Database creation / modification | None reported or instructed during inspection |
| Baseline table physical metadata, test DB identity | Not inspected; target absent |

**Identity safety:** Only the local instance and owner were reported; no privileged service principal, target DB, connection string, database ID or baseline row counts are approved. The observed Windows user/hostname must be independently compared with a named, approved isolated test principal before any future test run. Do not infer `db_owner`, file paths or test login rights from successful `master` SELECT.

## Updated code-only verification (actual output supplied)

- `dotnet build --no-restore` — **PASS**, 2 unrelated `TravelPlanningSqlServiceCollectionExtensionsTests` xUnit2031 warnings.
- `dotnet test tests/Wayhunter.UnitTests/Wayhunter.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GameProofMediaClaimRulesTests" --verbosity normal` — **20/20 passed, 0 failed, 0 skipped**.
- `git status -sb` — clean on `task/m5-idempotent-proof-recovery` tracking origin.
- Latest reported branch code-only commit: `d854a76` (full SHA not supplied in those current outputs; do not invent).
- `git fetch origin develop` updated `origin/develop` to `1355391`.
- `git rev-list --left-right --count HEAD...origin/develop` — **18 / 2** (18 current-feature-only, 2 develop-only commits).
- Develop-only commits: `1b9e73b` (M3 admin safety hardening), `1355391` (PR #272 merge).
- No merge/rebase of develop into M5 branch was performed.

**After adding this review pack, repeat read-only Git status/build/focused tests and attach outputs, because the final commit SHA and ahead/behind may change.** No registry SQL tests were run. No production provider/worker or PR merge authorized.
