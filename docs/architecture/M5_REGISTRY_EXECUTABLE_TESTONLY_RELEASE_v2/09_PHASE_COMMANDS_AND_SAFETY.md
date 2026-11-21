# M5 registry test-only SQL release v2: phase-specific authorization — NO PHASE APPROVED

**Coordinator decision:** v1 executable candidate requires corrections; A/B/C/D/E/F **all HOLD**. This file is a review proposal, not authority to run scripts.

| Phase | Scope and exact files | Prerequisite and stop condition |
|---|---|---|
| A | `01A_CREATE_ISOLATED_DB.sql` (master; database + marker) | **Separate A grant**, named owner/login, existing DB must be absent, LocalDB, no script edits. No independent CREATE batch after a `GO`. |
| B | `01B_BASELINE_INITIALIZE.sql` imports repo's **exact** `InitialSharedSchema.idempotent.sql` | **Separate B grant**, verified SHA-256 + Git blob + target/manifest, no baseline objects or migration history, postflight report |
| C1 | `02_INSTALL_REGISTRY_SCHEMA.sql` | **Separate C1 grant**, exact alternate keys, FK/collation/index width approval, no objects already installed |
| C2 | `02B_SYNTHETIC_RECEIPT_LEDGER.sql` | **Separate C2 grant**, only synthetic test identity, no real M4 provenance |
| D1 | `03A/B/C` immutable/append-only triggers | **Separate D1 grant**, successful TX transition checks + multirow negative tests must follow |
| D2 | `04A/B/C/D/E/F` TX-A, fake claim, TX-B, TX-C, reconcile, finalizer | **Separate D2 grant**, matching SQL batch guards, finalizer expiry/receipt rules |
| D3 | `05_PERMISSIONS_AND_EFFECTIVE_PROBE.sql`, `05B_BIND_APPROVED_EXTERNAL_TEST_PRINCIPAL.sql` | **Separate D3 grant**, approved external Windows login exists; no owner/sysadmin/db_owner bypass |
| E0 | `05C_TRUE_EXTERNAL_IDENTITY_PERMISSION_CHECK_READ_ONLY.sql`, `06_INSPECT_PHYSICAL_METADATA_READ_ONLY.sql`, `07_READONLY_ROW_COUNTS_AND_DISPOSAL_ID.sql` | **Separate E0 grant**, actual external token, physical object/collation checks; stop on any discrepancy |
| E1 | `12_SYNTHETIC_SQL_TEST_RUNNER.ps1` -> `13_SYNTHETIC_CASES_E_SCOPE_DO_NOT_RUN.sql` | **Separate E1 test-filter-specific grant**. `-Execute` denied by runner without grant/commit/baseline hash. Only synthetic fixture rows. |
| E2 | Actual 2-connection contention, real ack-loss, physical trigger multirow positives | **NOT IMPLEMENTED / NOT AUTHORIZED**; requires separate harness/approval; must not count as passed |
| F | `08_DISPOSAL_DISTINCT_SCOPE.sql` | **Separate F grant**, independently recorded `database_id`,`create_date`,manifest, no active sessions, zero synthetic rows, archived results, explicit approval |

## Non-bypass property of A/F
`CREATE DATABASE` and `DROP DATABASE` appear exclusively inside hard-coded `EXEC(N'...')` in the **same guarded SQL batch** (no intervening `GO` or standalone destructive DDL). This blocks ordinary batch-skipping of preflight guards when executing the reviewed file intact. It **does not** stop a privileged person from manually extracting a DDL string; written approval and least-privileged access still matter. Never paste the inside string alone in SSMS.

## Hash check before every phase
Record current release commit SHA and `SHA256_MANIFEST.json` SHA-256 entries for each exact script. For B also verify `14_BASELINE_MIGRATION_IMMUTABLE_SHA_REVIEW.md`, Git blob `90a193505e8efe0e680ec2c94cae4d2186b3d87e`, **and the exact local SHA-256 approved by coordinator**. A source change is a stop condition, not a prompt to auto-replace.

## Failure and cleanup rules
A cannot silently undo a partly created DB; if CREATE succeeds but manifest creation fails, **stop manual HOLD**, no automatic DROP. Baseline/schema DDL failures require a Coordinator decision after snapshot review, not a blanket cleanup command. Negative tests may leave synthetic test rows (test DB only); F refuses deletion while rows remain. Clean fixture-data deletion requires a **distinct E_CLEANUP proposal and grant**; no unreviewed `DELETE`, `TRUNCATE`, `DROP` or restoration.

## M4 boundary
`M5TestSyntheticConsumedReceipts` is a deliberately fake test ledger; it is *not* an M4 receipt, and no trusted canonical provider namespace or Game-purpose capture compatibility has been approved. No M4 consumption or production activation permitted.
