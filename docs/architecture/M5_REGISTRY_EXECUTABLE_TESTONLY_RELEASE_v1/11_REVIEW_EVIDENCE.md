# M5 Registry executable test-only review evidence — pre-authorization

- Latest coordinator decision supplied by user: **static physical schema review accepted; executable script preparation authorized; all SQL execution including DB creation remains HOLD**.
- Last user-reported clean M5 branch HEAD: `4f1f7646d13e04dc156d8d58d7c331be4b776e0d`.
- Last user-reported remote develop: `1355391`, M5 branch 18 branch-only commits / 2 develop-only at prior fetch, both M3 Admin safety PR #272. Refresh with `git fetch origin develop` **read-only branch ref update**, then inspect `git rev-list --left-right --count HEAD...origin/develop`. No merge/rebase without approval.
- Last user-reported build: passed, **2 existing M2 xUnit2031 warnings**; focused claim tests **20 passed / 0 failed / 0 skipped**. **Not rerun at release commit**; developer must do so locally and paste exact output after checkout/copy.
- Last user-reported LocalDB `(localdb)\MSSQLLocalDB`, version `17.0.4025.3`, collation `SQL_Latin1_General_CP1_CI_AS`, test DB absent from `sys.databases`. Does **not** prove physical keys/FKs or least-privilege on absent DB.
- Candidate nonclustered seven-column principal key: declared maximum 1,204 bytes (3*16+4+512+128+512), below documented SQL Server 2016+ limit 1,700; **still unverified physically**.
- M4 provider namespace, canonical equivalence and Game-purpose capture remain pending. Synthetic tokens/receipts in test-only plan are not M4 approval.

## Final submission checklist — to be filled with ACTUAL evidence

| Field | Outcome |
|---|---|
| This release code/script commit SHA | PENDING LOCAL COMMIT |
| New `origin/develop` SHA and divergence | PENDING REFRESH |
| `git diff --cached --check` | PENDING LOCAL VERIFICATION |
| `dotnet build --no-restore` | PENDING RERUN |
| M5 claim rules focused tests | PENDING RERUN |
| SQL parser/physical DDL compatibility | **NOT EXECUTED** |
| Any test DB created/modified | **NO** |
| Any registry integration tests run | **NO** |
| Physical FK/index/collation validation | **NOT EXECUTED** |
| SQL permissions/trigger effective result | **NOT EXECUTED** |
| Separate written Phase A/B/C/D/E/F approval | **NONE CURRENT** |
| M4 authoritative provider decision | **HOLD** |
| PR #256 merge | **HOLD** |

Report schema: `Commit SHA | Build status/warnings | Unit tests total/passed/failed/skipped | Git ahead/behind | DB instance/version/name/state | Phase ID | Reviewer authorization reference | Authorized SQL IDs | StartedUTC | CompletedUTC | Seeded row count | Expected row count | Actual row count | Constraint/trigger tests | Cleanup/rollback mode | Residual row count | Output redaction confirmation | Open blockers`.

Use only synthetic GUIDs and hashed identifiers. Never include real media URLs, provider secrets or raw session tokens in public logs.
