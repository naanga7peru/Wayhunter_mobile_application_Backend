# WAYHUNTER M5 — Phase B recovery preflight proposal v1

**Coordinator status: Phase B HOLD; recovery review required.** No SQL mutations were run while preparing this package.

This is a **new add-on proposal**, not a modification to the approved 29-file Release v2 or to its baseline migration file. Package is for review and approved read-only inspection only. The existing isolated database must remain intact (reported database ID **7**).

## Files

- `00_READ_ONLY_RECOVERY_AUDIT.sql`: Only session SET statements, catalog reads, fixture identity/read-only SELECT queries, and THROW fail-closed checks. **No schema/data changes.**
- `01B_BASELINE_RECOVERY_CANDIDATE.sql`: **NOT AUTHORIZED** SQLCMD executable recovery wrapper, with `QUOTED_IDENTIFIER ON`, other indexed-computed-column SET options, empty-history special case, no unexpected objects, fixed LocalDB/DB ID/operator/fixture identity, import of unmodified baseline, and required postflight history/objects checks.
- `02_PHASE_B_COMMANDS_DO_NOT_RUN.md`: exact approved-scope read-only audit and **unapproved** Phase B proposed SQLCMD command (`-I`).
- `03_RECOVERY_EVIDENCE_TEMPLATE.md`: actual-result placeholders; must not invent postflight results.
- `SHA256_MANIFEST.json`: SHA-256 hashes of the five payload files (including README), for copy/review integrity. The manifest does not hash itself.

## Important finding

The prior Release v2 `01B_BASELINE_INITIALIZE.sql` has a guard that rejects `__EFMigrationsHistory` **whenever it exists**, even empty. That does not match the coordinator's post-error-1934 recovery state. The new candidate instead requires the history table **already present and empty**, and refuses unexpected user schema objects/constraints/indexes. A clean uncreated database will NOT pass this recovery wrapper.

The authoritative migration `src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql` **must remain byte-identical**; expected Git blob `90a193505e8efe0e680ec2c94cae4d2186b3d87e`, previously observed local file SHA-256 `74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724`. Re-verify before authorization. Its reviewed source has no top-level `USE`, `:connect`, `:r`, or `SET QUOTED_IDENTIFIER OFF`; it has `GO` and a guarded migration transaction. SQLCMD `-I` plus connection-level SETs before import are meant to keep required options across those batches.

## Hard hold

No rerun of Phase B, no database repair, no CREATE/DROP, no Phase C, no production M4 or shared migrations, no PR #256 merge. The Coordinator must issue a separate written Phase B *recovery* grant after actual read-only inventory evidence and approved script hashes.
