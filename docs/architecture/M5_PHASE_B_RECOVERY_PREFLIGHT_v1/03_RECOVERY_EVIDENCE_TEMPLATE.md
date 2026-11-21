# Phase B read-only recovery evidence — fill from actual Windows outputs

Status: **DRAFT — no execution authorized**

| Field | Actual verified evidence |
|---|---|
| Current M5 feature commit SHA | [pending fresh check] |
| Latest origin/develop SHA + divergence | [pending fresh fetch] |
| Windows operator (`whoami`) | [pending fresh check; expected `UNICOMTIC\ut011148`] |
| SQL Server instance and version | [pending new read-only report] |
| Database name / ID | `Wayhunter_M5_Registry_IsolatedTest` / reported ID **7**; reverify |
| Database create date / collation | [pending fresh read-only report] |
| Manifest RunId and CreatedBy | [pending fresh read-only report] |
| User tables | Coordinator reported `M5RegistryFixtureManifest`, `__EFMigrationsHistory` only; reverify |
| Additional objects, constraints, triggers, indexes | [pending object inventory] |
| Migration-history row count | Coordinator reported **0**; reverify |
| Session quoted identifier from audit | [pending] |
| Baseline Git blob SHA-1 | `90a193505e8efe0e680ec2c94cae4d2186b3d87e` — expected; reverify |
| Baseline Windows SHA-256 | `74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724` — previously observed; reverify |
| Corrected wrapper SHA-256 | [copy from new manifest and verify Windows bytes] |
| Exact proposed SQLCMD command | In `02_PHASE_B_COMMANDS_DO_NOT_RUN.md` |
| Phase B execution authorization | **NOT GRANTED** |
| Phase C execution authorization | **HOLD** |

**Incident:** SQL Server error 1934 was reported in Phase B. The precise failed SQL statement/session settings have not been independently recovered in this chat. The SET-option fix is a **proposed likely remedy**, not evidence of SQL correctness. The original v2 Phase B wrapper rejects even empty `__EFMigrationsHistory`; the corrected candidate permits ONLY the approved empty-history recovery state.

**Failure action:** stop, archive exact console/SQLCMD exit code, keep DB untouched, notify DB Coordinator. Do not rerun/create/drop/migrate until authorization.
