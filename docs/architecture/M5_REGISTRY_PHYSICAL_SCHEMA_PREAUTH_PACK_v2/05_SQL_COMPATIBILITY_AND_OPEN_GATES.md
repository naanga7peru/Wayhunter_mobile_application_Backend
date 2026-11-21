# SQL Server 17 candidate compatibility — static review + actual environment evidence

| Item | Evidence | Status |
|---|---|---|
| Instance version | User's `sqlcmd` master SELECT: `17.0.4025.3` | VERIFIED READ-ONLY |
| Server collation | `SQL_Latin1_General_CP1_CI_AS` | VERIFIED READ-ONLY |
| Proposed dedicated DB | `Wayhunter_M5_Registry_IsolatedTest`: no row in `sys.databases` | ABSENT AT INSPECTION |
| Test database collation | Proposed `SQL_Latin1_General_CP1_CI_AS`, not actually created | NOT VERIFIED |
| Operation/registry media pair collation | Candidate explicit `Latin1_General_100_BIN2` on both | PROPOSED, NOT PHYSICAL |
| Player FK collation | `GameAttempts.PlayerUserId` and operation/claim player field inherit DB default in proposed fixture | PHYSICAL CHECK PENDING |

## Key widths (SQL Server nonclustered only)

- Composite operation alternate key: 3 × `uniqueidentifier` (48 bytes) + `int` (4) + `nvarchar(256)` trusted player (512) + `nvarchar(64)` namespace (128) + `nvarchar(256)` canonical media (512) = **1,204 declared maximum key-data bytes** across **7 columns**.
- Registry unique media pair: `128+512 = 640 bytes`.
- GameAttempt owner principal: `16+16+512 = 544 bytes`.
- Evidence principal: `16+16+16+4 = 52 bytes`.
- SQL Server 2016+ nonclustered index key maximum: **1,700 bytes**; column key maximum: 32. The 1,204-byte candidate is under this documented limit. Use `UNIQUE NONCLUSTERED`, not clustered; physical index/actual collation/FK tests remain unperformed.

## Composite FK prerequisites and exact source findings

1. `Claim(AttemptId, GameId, AuthenticatedPlayerUserId)` → `GameAttempts(Id, GameId, PlayerUserId)`. Existing `GamePersistenceConfigurations.cs` defines `AK_GameAttempts_Id_Game_Player` (EF metadata); shared baseline idempotent migration text also contains that name. **Target physical presence pending.**
2. `Claim(SubmissionOperationId, GameId, AttemptId, AuthenticatedPlayerUserId, StepNumber, MediaProviderNamespace, CanonicalMediaId)` → proposed `GameProofSubmissionOperations` seven-column **unique nonclustered** `AK_GameProofOperations_ImmutableClaimOwner`. Proposed schema gives operation columns matching registry columns in type/order and explicit BIN2 media pair.
3. `Claim(EvidenceId, GameId, AttemptId, StepNumber)` → `GameProofEvidence(Id, GameId, AttemptId, StepNumber)`. Current EF baseline only has `AK_GameProofEvidence_Id_Attempt`; proposed isolated delta adds 4-column principal constraint (no shared-schema change).

**Cross-source mismatch to review:** existing *unregistered* `GameProofSubmissionOperationRowConfiguration` does not specify BIN2 for operation namespace/media; candidate SQL uses it. After M4's definitive canonicalization decision, EF test metadata must be reconciled with the approved physical fixture; do not silently change registered shared mappings. `GameProofMediaClaimRowConfiguration` already proposes BIN2 on registry pair. SQL collation is not M4 normalization or ownership proof.

**Unknown/untested:** actual target DB `sys.columns` metadata (DB absent), FK creation results, effective SQL permissions, stored procedures, trigger behavior, runtime transaction commits, provider receipt durability, and M4 capture purpose. No SQL test pass is claimed.

**Official SQL Server references:**
- https://learn.microsoft.com/en-us/sql/t-sql/statements/create-index-transact-sql
- https://learn.microsoft.com/en-us/sql/relational-databases/tables/create-foreign-key-relationships
- https://learn.microsoft.com/en-us/sql/relational-databases/collations/set-or-change-the-column-collation
- https://learn.microsoft.com/en-us/sql/t-sql/statements/create-trigger-transact-sql
