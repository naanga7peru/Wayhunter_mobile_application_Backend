# WAYHUNTER M5 — Phase B stable LocalDB binding recovery proposal v2

**PHASE B HOLD — PREPARATION / READ-ONLY ONLY.** Prior single attempt was consumed by error 59220; database preservation and all other holds continue. Never run baseline or Phase C from this pack.

Previous fixed `LOCALDB#00357BA9` guard was too brittle because the physical named-pipe suffix changes across LocalDB starts. The revised wrapper only accepts a fresh runtime identity which the read-only `05_READ_ONLY_TARGET_BINDING_PREFLIGHT.ps1` has verified against both the logical `(localdb)\MSSQLLocalDB` and its `sqllocaldb info MSSQLLocalDB` physical named pipe. **The actual future proposed SQLCMD connection is the verified pipe** (requires coordinator authorization for this revised choice). The wrapper still fails closed on identity mismatch and protects DB ID 7, exact datetime2(3) create timestamp, operator, fixture, zero history, schema inventory and unmodified baseline.

### Review contents

- `00_READ_ONLY_RECOVERY_AUDIT.sql`: existing unchanged, read-only catalog audit.
- `01B_BASELINE_RECOVERY_CANDIDATE.sql`: NEW unapproved execution candidate, no hardcoded transient runtime suffix; requires verified SQLCMD variables.
- `02_PHASE_B_COMMANDS_DO_NOT_RUN.md`: read-only and explicitly **UNAUTHORIZED** future SQLCMD command.
- `03_RECOVERY_EVIDENCE_TEMPLATE.md`: existing evidence template, unchanged.
- `04_READ_ONLY_BINDING_ROW.sql`: read-only, headerless SQL identity probe, both alias and pipe.
- `05_READ_ONLY_TARGET_BINDING_PREFLIGHT.ps1`: strictly read-only, fail-closed hash/alias/pipe/fixture/catalog checks; NEVER invokes mutating wrapper.
- `06_STABLE_INSTANCE_RECOVERY_REVIEW.md`: cause, security boundaries, evidence pending, reviewer decisions.
- `SHA256_MANIFEST.json`: SHA256 hashes for repository payloads except itself and the previously committed `99_ACTUAL_READ_ONLY_RECOVERY_RESULT.md` historical evidence file.

**Original authoritative baseline:** `src/Wayhunter.Infrastructure/Persistence/Migrations/InitialSharedSchema.idempotent.sql`, SHA-256 `74243E7CE8C5C79A79D1B455C7EE0C263E1894CD570A4EF3FC45FEDE3DAE6724`, Git blob `90a193505e8efe0e680ec2c94cae4d2186b3d87e`. Never edit it.
