# M1 Password Recovery Reset Table Migration Proposal

**Audience:** DB Coordinator
**Status:** PROPOSAL â€” approval required before migration generation or database update
**Review base:** `origin/develop` at `bae0a8c` (fetched 2026-11-21)
**Working branch at review:** `task/m1-sql-verification` at `53c3996`

## Scope and evidence

This proposal covers only the additive `IdentityPasswordResetCredentials` table described by the EF mapping at `src/Wayhunter.Infrastructure/Persistence/Configurations/IdentitySocial/IdentityPasswordResetCredentialConfiguration.cs` on `origin/develop`. It is a schema proposal, not evidence of a deployed database shape or a migration approval.

The repository's `docs/second-brain/CONTEXT.md` refers to a draft Master BRD v2.1 in the separate Second Brain hub, but that document is not in this checkout. Accordingly, the table definition below is grounded in the checked-in EF mapping and existing `Users` migration, not in an unavailable BRD text. The repo's documented implementation status is not treated as proof of deployed behavior.

## KEEP / MODIFY / ADD plan

- **KEEP:** `20261117093023_InitialSharedSchema` and its generated SQL unchanged.
- **MODIFY:** No existing migration. When approved and generated, the model snapshot should advance only by the reset-credential entity and its relationship, indexes, and check constraint.
- **ADD:** One follow-up migration that creates/drops only `IdentityPasswordResetCredentials`, plus a review record of the schema delta.
- **Verification before approval:** Review generated `Up`/`Down` against this proposal; confirm the migration's only table operation is create/drop of this table and that it has the exact FK, indexes, and check below. No migration or database update is performed by this proposal.

## Proposed table

| Column | SQL type | Nullability / mapping |
|---|---|---|
| `CredentialId` | `uniqueidentifier` | `NOT NULL`, PK, `ValueGeneratedNever()` |
| `UserId` | `nvarchar(256)` | `NOT NULL`, FK to `Users.UserId` |
| `TokenHash` | `char(64)` | `NOT NULL` |
| `CreatedAtUtc` | `datetimeoffset` | `NOT NULL` |
| `ExpiresAtUtc` | `datetimeoffset` | `NOT NULL` |
| `ConsumedAtUtc` | `datetimeoffset` | `NULL` |
| `RevokedAtUtc` | `datetimeoffset` | `NULL` |

Constraints and indexes from the EF mapping:

- PK: `PK_IdentityPasswordResetCredentials` on `CredentialId`.
- FK: `FK_IdentityPasswordResetCredentials_Users_UserId` from `UserId` to `Users.UserId`, `ON DELETE NO ACTION`.
- Unique index: `UX_IdentityPasswordResetCredentials_TokenHash` on `TokenHash`.
- Non-unique index: `IX_IdentityPasswordResetCredentials_User_Expiry` on (`UserId`, `ExpiresAtUtc`) in that order.
- Check: `CK_IdentityPasswordResetCredentials_Expiry`: `[ExpiresAtUtc] > [CreatedAtUtc]`.

The referenced `Users.UserId` is the existing `nvarchar(256)` primary key in `InitialSharedSchema`, so the proposed FK column type/length matches it.

## Snapshot comparison

The current `WayhunterSharedDbContextModelSnapshot.cs` (same on this branch and `origin/develop`) has no `IdentityPasswordResetCredential` entity, table, index, check constraint, or relationship. `InitialSharedSchema` likewise does not create this table. The EF mapping and DbSet are present on `origin/develop`; therefore the reset entity is a pending model-to-snapshot delta, not an already-migrated table.

For a reset-table-only release, the follow-up migration's target model and snapshot delta must contain only this entity and the definitions above. Do not let migration scaffolding absorb the unrelated context mappings listed below. Once this approved migration is accepted, the snapshot represents the approved schema history including the reset table. When those unrelated mappings are later approved, scaffold a separate follow-up migration from that snapshot; its diff should surface those still-unshipped mappings then. Review each later migration's `Up`, `Down`, and resulting snapshot before any database update. This keeps the snapshot truthful to the migrations that ship and avoids silently marking unshipped tables as applied.

If the EF tooling for the approved reset-only generation cannot isolate the target model from those unrelated mappings, stop before accepting generated output: have the DB Coordinator choose an isolated model/configuration or a reviewed manual migration and snapshot update. Do not â€œfixâ€ the diff by adding unrelated table operations or by editing `InitialSharedSchema`.

## Unrelated shared model changes on `origin/develop`

The `WayhunterSharedDbContext` diff from the reviewed working branch adds four proposed M3 mappings, explicitly annotated in code as model mappings with migration approval separate:

- `TripSafety_SafeCheckIns` (`TripSafeCheckInRow`)
- `TripSafety_SafeCheckInOutbox` (`TripSafeCheckInOutboxRow`)
- `TripSafety_TripMemories` (`TripMemoryRow`)
- `TripSafety_TripMemoryMedia` (`TripMemoryMediaRow`)

Their configurations are registered in the shared context on `origin/develop`; they are not part of this M1 reset-table proposal. Keep them out of this migration and snapshot update for the reset-only release. They require their own scope and review. The fetched branch also contains two proposed M7 advertisement upload configuration files, but they are not registered in the shared DbContext model and are not part of this active model delta. Other application and documentation differences between branches are outside this schema review.

## Decision and approval record

- **BRD rule:** No specific Master BRD clause is claimed because the draft is not available in this checkout. Source inspected for this proposal: the M1 EF mapping and `Users` definition in `InitialSharedSchema`.
- **Admin module:** Not applicable; password recovery is M1 IdentitySocial.
- **Branch/commit:** Review comparison `task/m1-sql-verification` `53c3996` against fetched `origin/develop` `bae0a8c`.
- **Changed files in this proposal:** `docs/api/M1_PASSWORD_RESET_TABLE_MIGRATION_PROPOSAL.md` only.
- **Tests run:** None. This is a read-only schema review and proposal; no migration was generated, no database was updated, and no tests were run.
- **Risks / remaining decisions:** DB Coordinator approval is required for the reset-only migration and snapshot procedure. The separate draft Master BRD is unavailable here. The four M3 mappings remain outside scope and require separate migration review.
