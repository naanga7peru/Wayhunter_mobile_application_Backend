# Registry SQL authorization & execution results — blank signed-off template

**Use `NOT RUN`, `BLOCKED`, `PASS`, `FAIL` explicitly. Do not mark unfinished cases passed.**

| Field | Value |
|---|---|
| Authorizing reviewer / scope / approval reference | PENDING |
| Approved commit SHA + proposed test runner version | PENDING |
| Instance / database | `(localdb)\MSSQLLocalDB` / `Wayhunter_M5_Registry_IsolatedTest` (**proposed only**) |
| Database owned by test operator; pre-existing? | PENDING (last read-only inspection: absent) |
| Principal / connection safety | PENDING (no secrets in report) |
| SQL Server version / collation | `17.0.4025.3` / `SQL_Latin1_General_CP1_CI_AS` (read-only master) |
| DDL authorized / executed | NO / NO |
| SQL registry tests authorized / executed | NO / 0 |
| Synthetic tests PASS/FAIL/SKIP/BLOCKED | 0/0/0/all NOT RUN |
| M4 real-provider tests | BLOCKED |
| Registry fake-provider unit tests | 20 PASS, 0 FAIL, 0 SKIP (previous code-only run) |
| Build / warning count | PASS / 2 unrelated M2 xUnit2031 |
| develop compare | 18 feature-only / 2 develop-only (at inspected `1355391`) |
| Pre-count/after-count claim state 0/1/2/3/4 | NOT AVAILABLE (no test DB) |
| Operation, evidence, progress and receipt counts | NOT AVAILABLE |
| Atomic rollback, ambiguous commit reconciliation evidence | NOT RUN |
| Trigger/permission negative case evidence | NOT RUN |
| Teardown type, authorization, post-cleanup DB_ID | NOT AUTHORIZED; NOT RUN |
| Shared DB mutation check | No test SQL executed; independent full baseline comparison pending |
| PR #256 | REVIEW ONLY / DO NOT MERGE |

## Per-test row template

`TestID | layer (unit/synthetic SQL/M4) | status | commit SHA | allowed instance/db | expected result | observed result | error code | fake consume calls | before/after row counts | rollback outcome | cleanup disposition | sanitized logs reference`

Never include credentials, access tokens, provider receipt secrets, personal data, real media URL or actual canonical asset IDs. All real SQL results, if someday authorized, must identify the exact DB and session and exclude unexecuted cases from pass counts.
