# Kaviyan's ten required items — evidence map and remaining gates

| # | Requested deliverable | File(s) | State |
|---:|---|---|---|
| 1 | Exact test-only database CREATE and baseline initialization | `01_CREATE_DATABASE_REVIEW_ONLY.sql`, `02_BASELINE_INITIALIZATION.md` + existing repo idempotent SQL | REVIEW candidate; execution HOLD |
| 2 | Registry 16-col table, 5 CHECKs, PK, 5 indexes, 3 composite FKs | `03_REGISTRY_SCHEMA_REVIEW_ONLY.sql` | REVIEW candidate; not compiled on SQL |
| 3 | Operation and evidence supporting alternate keys | `03_REGISTRY_SCHEMA_REVIEW_ONLY.sql`, `05_SQL_COMPATIBILITY_AND_OPEN_GATES.md` | REVIEW candidate; test DB absent |
| 4 | Index widths + collations / physical compatibility | `05_SQL_COMPATIBILITY_AND_OPEN_GATES.md`, `06B_TEST_DATABASE_METADATA_READ_ONLY.sql` | Static + server version confirmed; actual FK/collation PENDING |
| 5 | Controlled-writer rights + immutable UPDATE/DELETE guards | `04_REGISTRY_GUARD_AND_PERMISSIONS_REVIEW_ONLY.sql`, `04B_OPERATION_AND_EVIDENCE_GUARDS_REVIEW_ONLY.sql` | Candidate trigger/role; TX procedure bodies/privilege tests PENDING |
| 6 | DB identity + connection-string guards | `01_CREATE_DATABASE_REVIEW_ONLY.sql`, `06_READ_ONLY_METADATA.sql`, `07_ACTUAL_READ_ONLY_BASELINE.md`, `11_PROPOSED_COMMANDS_DO_NOT_RUN.md` | Real master inspection; target absent |
| 7 | Setup, rollback, cleanup, teardown | `08_TEARDOWN_REVIEW_ONLY.sql`, `09_SETUP_ROLLBACK_CLEANUP_PLAN.md` | Proposed; destructive scope separately approval |
| 8 | Synthetic registry vs M4-dependent test separation | `10_TEST_MATRIX.md` | Prepared; no registry SQL executed |
| 9 | Exact proposed commands & sanitized results | `11_PROPOSED_COMMANDS_DO_NOT_RUN.md`, `12_SANITIZED_RESULTS_TEMPLATE.md` | Prepared; execution commands ON HOLD |
| 10 | Branch, build & tests | `07_ACTUAL_READ_ONLY_BASELINE.md` | Actual provided evidence; new push SHA & post-pack test rerun pending |

**Explicit outstanding blockers:** M4 authoritative namespace/equivalence/canonical media identity; Game-purpose capture compatibility; test DB creation/DDL/role/procedure/trigger/drop approval; complete TX-A/TX-B/TX-C test writer code; actual FK/collation verification on approved instance; final SQL test allowlist; approved fixture disposal; PR #256 merge is prohibited.
