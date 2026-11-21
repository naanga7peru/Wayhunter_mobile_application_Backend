# WAYHUNTER M5 — Test-only executable SQL release candidate v1

**Authoring baseline:** PR #256, branch `task/m5-idempotent-proof-recovery`, last user-reported HEAD `4f1f7646d13e04dc156d8d58d7c331be4b776e0d` (not re-fetched here).

**Authority:** The DB Coordinator accepted the static v2 review pack, **NOT** this executable release and **NOT any SQL execution**. These `.sql` files are executable proposals behind SQLCMD approval-phase and instance/database/login safety guards. They have **not been run or parsed by SQL Server**. No production M4 integration is included.

**Do not run** `sqlcmd -i`, copy scripts into an IDE and execute, create the database, start migrations, run registry SQL tests or drop the database until a separate written grant for that exact phase, statement set, principal and target.

## Exact approval phases (separate grants, not blanket consent)

| Phase | Action/files | Approval state |
|---|---|---|
| A_CREATE | `01A_CREATE_ISOLATED_DB.sql`, creates exact disposable DB + fixture manifest | **HOLD** |
| B_BASELINE | `01B_BASELINE_INITIALIZE.sql`, uses exact `InitialSharedSchema.idempotent.sql` with `:r` | **HOLD** |
| C_SCHEMA | `02_INSTALL_REGISTRY_SCHEMA.sql` with operation/claim, indexes, CHECKs and FK/alternate key | **HOLD** |
| D_GUARDS | `03A`, `03B`, `03C` triggers (one batch each) | **HOLD** |
| D_PROCS | `04A` TX-A, `04B` synthetic lease, `04C` TX-B, `04D` TX-C, `04E` read-only reconciliation | **HOLD** |
| D_SECURITY | `05` role, loginless probe principal, DENY/GRANT and effective access probe | **HOLD** |
| E_INSPECT | `06` metadata + `07` row-count / disposal-identity read-only reports (needs test DB) | **HOLD** |
| E_SYNTHETIC | Only approved, explicitly scoped synthetic seed/fault-injection test harness, separately submitted | **HOLD** |
| F_DISPOSE | `08` whole test DB disposal (identity and recorded creation check) | **HOLD** |

No phase is approved by the presence of a filename or the `-v` flag. M4 namespace/canonicalization/Game purpose remain HOLD; **synthetic fake keys only**.

## Scope / implementation caveats

1. TX-A atomically inserts operation+registry with Game-first lock; TX-P is **not** implemented. Fake M4 confirmation is a **trusted, externally supplied synthetic receipt**. TX-B writes op receipt and claim state together. TX-C inserts evidence, sets progress to WaitingForVerification, completes operation and claim in one commit. No Coins, game winner or admin release.
2. `M5Test_ClaimForFakeProvider` is a test-only lease holder prerequisite before TX-B; **never calls the real provider**. Recovery procedure returns a sanitized classification and does not alter provider state.
3. All procedures are experimental and require DB Coordinator signoff on matching runtime binding, lease policy, a transaction deadlock/failure test and manual hold semantics. They **must not be called by production DI**.
4. Physical DDL compared with current EF model: operation BIN2 pair requires a future **unregistered** EF proposal correction; shared model/snapshot not touched.
5. The optional registration retry is read-only if the exact operation and registry already exist, even when stage progress has advanced; completed TX-C returns exactly the existing evidence ID without another write. Both paths still require exhaustive fault-injection and locking verification.
6. Test-only evidence `MediaId` is set to the proposed canonical value; this is **not** authorization to assume legacy raw MediaId equivalence. H0-H3 history cutover remains disabled.
7. Effective least privilege only verified **after** Phase D_SECURITY under `m5_registry_test_probe` without login. A real external test principal's connection/token permissions must be inspected separately before using it; no real principal created or granted access here.
8. Several critical behaviors remain unproven until actual authorized SQL Server execution: trigger multi-row SET behavior, `GO` boundaries, collation and FK enforcement, timeout/commit-ack handling, and cleanup. Static checks are not SQL tests.
9. `CREATE/DROP DATABASE` are not transactional reversible DDL. Phase A/Phase F require separate grants; fail-closed on unexpected database ID, manifest, sessions or version.

Read `07_SETUP_ROLLBACK_CLEANUP_POLICY.md`, `09_PHASE_COMMANDS_AND_SAFETY.md`, `10_TEST_MATRIX.md` and `11_REVIEW_EVIDENCE.md` before requesting each separate authorization. **PR #256 REVIEW ONLY, DO NOT MERGE.**
