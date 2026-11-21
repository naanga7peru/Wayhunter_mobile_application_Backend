# v2 final evidence record — all SQL phases HOLD

**Previous reviewed executable candidate:** `b02a6ee443f3c4144db7731546933e4507eab26c`, 20 files, v1 corrections requested.

**Last user-reported observations at v1 preparation (not new v2 results):** `origin/develop=77d1f57`, M5 branch 20 branch-only / 8 develop-only; build passed with 2 unrelated M2 xUnit2031 warnings; focused `GameProofMediaClaimRulesTests` 20 passed / 0 failed / 0 skipped. Test DB `Wayhunter_M5_Registry_IsolatedTest` not found in read-only `sys.databases`; LocalDB version 17.0.4025.3, collation SQL_Latin1_General_CP1_CI_AS.

| Required evidence | v2 status |
|---|---|
| v2 staging diff whitespace | Pending local check |
| v2 GitHub commit SHA | Pending local commit/push |
| release scripts exact SHA256 manifest | Included `SHA256_MANIFEST.json` |
| Current `origin/develop` SHA / ahead / behind | Pending refresh at v2 commit |
| Baseline Git blob SHA-1 | `90a193505e8efe0e680ec2c94cae4d2186b3d87e` (GitHub source at prior b02a6ee commit) |
| Local baseline SHA-256 | **Pending**; do not invent |
| Build and focused unit tests at v2 commit | **Pending**; rerun |
| Phase A-F SQL execution | **NONE APPROVED / NONE EXECUTED** |
| External real principal effective permissions | **NOT INSPECTED** |
| Synthetic S01-S15 SQL tests / trigger positives/negatives | **NOT EXECUTED** |
| True lost COMMIT ACK and cross-Game concurrency | **NOT IMPLEMENTED / NOT EXECUTED** |
| M4 provider namespace + canonicalization + Game capture | **HOLD** |
| PR #256 merge | **HOLD** |

Reviewers must not count static tests, a generated SHA manifest or scripted assertions as SQL verification. Sanitized phase reports must include exact instance/database, operator, grant ID, DB ID/create_date, SHA256, test ID, result/expected assertions, affected row counts, rollback, residual rows and notes; never log real media IDs or token contents.
