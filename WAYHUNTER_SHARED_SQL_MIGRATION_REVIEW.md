# Wayhunter Shared SQL Migration Review

**Review date:** 2026-11-22
**Worktree:** `WAYHUNTER_FINAL_INTEGRATION`
**Branch:** `task/backend-final-integration-readiness` (tracks the head branch of open PR #293)
**HEAD:** `54b727f1f343149fd0ba3074a97123dbdb816a88`
**Disposition:** Review inventory only. This report is uncommitted and must be proposed on a separate branch; PR #293 is unchanged. No database was contacted and no migration was created or applied.

## 1. Exact schema differences

The design-time `WayhunterSharedDbContext` model differs from the checked-in `InitialSharedSchema` model snapshot by **94 operations**: 18 table additions, 25 column additions, 1 nullability alteration, 2 alternate keys, 42 index additions/replacements, 4 check-constraint additions/replacements, and 2 dropped objects (one index and one check constraint). This is model-to-snapshot drift, not evidence of any deployed database's actual schema or data.

### New tables (18)

1. `IdentityPasswordResetCredentials`
2. `UserRoleAssignments`
3. `TripPlanCopyAttributions`
4. `TripSafety_MemberRevocationOutbox`
5. `TripSafety_SafeCheckIns`
6. `TripSafety_SafeCheckInOutbox`
7. `TripSafety_TripMemories`
8. `TripSafety_TripMemoryMedia`
9. `TripSafety_NotificationEvents`
10. `TripSafety_NotificationRecipients`
11. `TripSafety_NotificationPreferences`
12. `TripSafety_NotificationDevices`
13. `TripSafety_NotificationTokenBindings`
14. `TripSafety_NotificationJobs`
15. `TripSafety_NotificationJobRecipients`
16. `TripSafety_NotificationAudienceReceipts`
17. `TripSafety_NotificationOperations`
18. `TripSafety_NotificationDeliveryAudit`

### Added columns (25)

| Existing table | Columns |
|---|---|
| `TripSafety_HazardReports` | `Description`, `Latitude`, `Longitude`, `PhotoAssetReference` |
| `SampleVoucherRedemptionRequests` | `DecisionActorUserId`, `DecisionReason`, `OwnerNameSnapshot` |
| `SampleVoucherIssuances` | `AttemptCount`, `ExpiresAtUtc`, `LastErrorCode`, `RowVersion`, `UseReason`, `UsedAtUtc`, `UsedByActorUserId` |
| `SampleVoucherInventory` | `AddedAtUtc`, `AddedByAdminUserId`, `ExpiresAtUtc`, `RowVersion`, `SampleImageReference` |
| `CoinRuleVersions` | `CreatedAtUtc`, `CreatedByUserId`, `EffectiveFromUtc`, `EffectiveUntilUtc` |
| `CoinLedgerEntries` | `ActorUserId`, `Reason` |

### Existing objects altered or replaced

- `SampleVoucherIssuances.InventoryId`: required to nullable. The existing unique index `UX_VoucherIssuances_Inventory` is dropped and recreated with filter `[InventoryId] IS NOT NULL`, retaining uniqueness for assigned inventory while permitting unassigned issuance rows.
- `CK_VoucherIssuances_Status`: snapshot expression is `[Status] IN (1, 2, 3)`; current configuration replaces it with `[Status] IN (1, 2, 3, 4, 5)`. The current draft enum maps these values to Unissued, Failed, Issued, Used, Expired respectively.
- Added `CK_TripSafety_HazardReports_Coordinates`: coordinates are both null or both in latitude `[-90,90]` and longitude `[-180,180]` ranges.
- Added `CK_VoucherIssuances_Attempts`: `AttemptCount >= 0`.
- Added `CK_VoucherInventory_Expiry`: `ExpiresAtUtc > AddedAtUtc`.
- Added alternate keys `AK_TripStops_Id_TripPlanId` on `(Id, TripPlanId)` and `AK_TripSafety_LiveTripSessions_SessionId_TripPlanId` on `(SessionId, TripPlanId)`.
- New dependent references use `NoAction` deletion behavior in current mappings. Relevant composite references include safe check-ins and notification jobs to `(SessionId, TripPlanId)`, and trip memories to `(TripStopId, TripPlanId)`.

### Indexes added or recreated (42)

**M1:** `IX_IdentityPasswordResetCredentials_User_Expiry`, `UX_IdentityPasswordResetCredentials_TokenHash`, `IX_UserRoleAssignments_AssignedAtUtc`, `IX_UserRoleAssignments_UserId_Status`, `UX_UserRoleAssignments_Active_UserId_Role`.

**M2:** `IX_TripPlanCopyAttributions_SourceTripPlanId`.

**M3:** `IX_TripSafety_MemberRevocationOutbox_Pending`, `UX_TripSafety_MemberRevocationOutbox_MemberId`, `IX_TripSafety_NotificationAudienceReceipts_ActorId`, `IX_TripSafety_NotificationAudienceReceipts_ExpiresAtUtc`, `IX_TripSafety_NotificationDeliveryAudit_EventId_UserId`, `IX_TripSafety_NotificationDevices_TokenFingerprint`, `IX_TripSafety_NotificationDevices_UserId_RevokedAtUtc`, `IX_TripSafety_NotificationEvents_JobId`, `IX_TripSafety_NotificationJobRecipients_UserId`, `IX_TripSafety_NotificationJobs_ActorId`, `IX_TripSafety_NotificationJobs_SessionId_TripPlanId`, `IX_TripSafety_NotificationJobs_State_DueAtUtc`, `IX_TripSafety_NotificationJobs_TripPlanId_SessionId_ActorId`, `IX_TripSafety_NotificationOperations_ResourceId_RecordedAtUtc`, `IX_TripSafety_NotificationRecipients_NextAttemptAtUtc_LeaseUntilUtc`, `IX_TripSafety_NotificationRecipients_UserId_ReadAtUtc_EventId`, `IX_TripSafety_NotificationTokenBindings_DeviceId`, `IX_TripSafety_NotificationTokenBindings_UserId`, `IX_TripSafety_SafeCheckInOutbox_PendingDue`, `IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry`, `IX_TripSafety_SafeCheckInOutbox_RecipientUserId`, `UX_TripSafety_SafeCheckInOutbox_CheckInId`, `IX_TripSafety_SafeCheckIns_SessionId_TripPlanId`, `IX_TripSafety_SafeCheckIns_TripPlanId`, `IX_TripSafety_SafeCheckIns_UserId`, `UX_TripSafety_SafeCheckIns_SessionId_UserId`, `IX_TripSafety_TripMemories_OwnerUserId`, `IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreatedAtUtc`, `IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreateOperationId`, `IX_TripSafety_TripMemories_TripStopId_TripPlanId`, `IX_TripSafety_TripMemoryMedia_MemoryId`, `IX_TripSafety_TripMemoryMedia_ObjectKey`, `IX_TripSafety_TripMemoryMedia_State_CreatedAtUtc`.

**M6:** `UX_VoucherIssuances_Inventory` (recreated with nullable filter), `UX_CoinLedgerEntries_CorrectionOf`, `UX_CoinLedgerEntries_Wallet_NonRewardSource`.

The complete key, column types, filters, FKs and FK principals should be checked from a generated offline candidate diff before approval. This inventory does not assert that these definitions have been approved.

## 2. Owning modules

| Owner | Differences | Related PR/ownership context |
|---|---|---|
| M1 | Two identity tables and five indexes | Identity schema ownership; open PR #287 is API error-contract work, and #288 includes identity runtime wiring. Neither PR alone approves these schema changes. |
| M2 | Trip-copy attribution table/index and `TripStops` alternate key | M2 owns route/trip planning. PRs #280–#286 form a dependent M2 stack; schema key is shared with M3 memory references. |
| M3 | Fifteen TripSafety tables, 33 M3 indexes, four hazard columns, session composite alternate key | Open PR #290 concerns TripSafety test-host/docs work; it does not authorize this schema. M3 migration acceptance docs require explicit SQL authorization. |
| M6 | Voucher/ledger/rule columns, issuance nullability, status/check changes, three M6 indexes | PR #291 is wallet auth test/docs work. Approved voucher decision VCH-DEC-01 applies to business behavior, not automatic approval of this schema. |
| M4, M5, M7 | No operations in this model diff | Their schema/design owners still need to confirm that these shared changes preserve cross-module FK, retention, and deployment contracts. |

These ownership assignments follow the repository ownership map and entity/configuration names. They are review routing, not proof of owner approval. The changes appear to be accumulated model work missing from the initial snapshot; that intent classification is an inference. Existing open PRs are not represented as merged changes in this worktree.

## 3. Existing-data risks

1. **Required metadata on existing rows:** current EF mappings require `OwnerNameSnapshot`, `SampleVoucherInventory.AddedAtUtc`, `AddedByAdminUserId`, `ExpiresAtUtc`, `SampleImageReference`, and `CoinRuleVersions.CreatedAtUtc`, `CreatedByUserId`, `EffectiveFromUtc`. `CoinLedgerEntries.ActorUserId` is also required. The model comparison includes required column metadata/defaults (some default to empty text or `DateTimeOffset.MinValue`); those are not verified business-valid backfills. An existing populated database needs owner-approved, deterministic backfills, nullability staging, or an explicit fresh-database-only decision. The expiry check would reject rows with `ExpiresAtUtc <= AddedAtUtc`.
2. **Voucher status range:** values 1–3 satisfy the old constraint; 4–5 become legal only after replacement. Before tightening/expanding constraints, profile actual stored values in the authorized isolated database and reconcile them to the enum. No current data inspection has occurred. Invalid or undefined values must not be silently normalized.
3. **Voucher issuance ownership:** making `InventoryId` nullable permits an Unissued/Failed issuance without inventory. Existing issuance rows must retain correct inventory assignment; duplicate non-null assignments could prevent creation of the filtered unique index. Confirm the one issuance-per-request unique index and current one-time debit semantics remain intact.
4. **VCH-DEC-01 boundary:** request requires at least 10,100 spendable coins and reserves 10,000; only Admin Approve performs the single final 10,000 debit and consumes reservation; reject/cancel releases reservation once. Issuance is downstream and must not debit again; failed issuance remains Approved/Unissued for controlled idempotent retry. The schema alone cannot guarantee this transaction/idempotency behavior. Test actual transactional service and duplicate/concurrency paths before release.
5. **Unique indexes and alternate keys:** duplicate existing role assignments, token hashes, attribution references, event/source ledger corrections, notification receipts, or composite parent keys can block creation. For every unique/filtered index, run read-only duplicate preflight against an authorized disposable copy and obtain an owner-approved remediation plan.
6. **FKs and delete behavior:** new `NoAction` FKs may expose existing orphan rows or change deletion expectations. Profile every principal/child pair and retention rule. No cascade or cleanup should be inferred.
7. **M3 coordinates and sensitive data:** pre-existing partial coordinate pairs or out-of-range values would fail the new check. Hazard descriptions/media references, trip memories, notification devices/tokens, and delivery audit need approved access, retention, and redaction policies. Do not put raw tokens, contacts, voucher codes, or GPS trails in review artifacts/logs.
8. **`rowversion` and defaults:** adding rowversion columns and other required fields to populated tables requires SQL Server behavior verification and approved backfill semantics. No SQL version, deployed schema, row counts, or data distribution is known.

## 4. Dependency ordering

For review and rollout planning, dependencies should be handled in this order; the final migration's actual operation ordering must be derived from reviewed FKs and the target baseline:

1. Confirm the target is a fresh isolated catalog or an existing schema and identify the exact baseline. Existing database contents and migration history have not been verified.
2. Confirm canonical M1 `Users` and M2 `TripPlans`/`TripStops` principals, column types/collations, keys, and retention/delete rules.
3. Add M1 identity tables and M2 attribution/key structures after principals are established.
4. Add M3 trip-session composite alternate key and M3 parent tables; then dependent safe-check-in, memory/media, revocation-outbox, and notification/audit tables in FK-topological order. Add hazard columns/check only after approved coordinate backfill rules are set.
5. Reconcile M6 existing table data and backfills first; then add M6 metadata, checks, filtered/unique indexes, nullable inventory relationship, and ledger indexes in a transaction plan that preserves approval/debit idempotency.
6. Build and review one shared migration candidate against the selected baseline. EF can order many operations, but it cannot decide historical data meaning, backfills, deployment windows, or safe recovery.

Cross-module blockers: M3 references M2 trips/stops/sessions and M1 users; M6 references M1 users/wallet rows and voucher request/inventory parents. M4/M5/M7 should confirm that no separate migration or unmerged PR changes these principals concurrently. Merge/deployment sequence must be refreshed against the then-current `develop` and PR dependency graph; this report does not authorize merging.

## 5. Required schema decisions

- Fresh schema only, or upgrade of a populated schema? Name the authoritative baseline and supported prior schema versions.
- Per-required-column backfill policy and accepted historical values, especially owner snapshot, voucher inventory timestamps/admin/image, coin rule creator/effective dates, and ledger actor/reason.
- Are `DateTimeOffset.MinValue` and empty-string model defaults permitted as transitional values? If not, specify safe staged nullability/backfill/finalization.
- Confirm voucher status integer mapping and whether `Unissued`/`Failed` issuance records may exist with `InventoryId = NULL`; define recovery for existing issuance rows that violate intended mapping.
- Confirm `AdminApprove` is the single final 10,000 debit trigger, consumes reservation once, while issue/retry never debits; define atomic transaction, idempotency key, and failure recovery.
- Confirm exact unique-key/filter definitions and remediation policy for pre-existing duplicates, including filtered inventory assignment and M1 active user-role uniqueness.
- Confirm all FK principal types, composite-key semantics, `NoAction` deletion/retention requirements, and orphan handling.
- Confirm hazard coordinate null-pair semantics and data remediation; confirm M3 private media/token retention and audit redaction.
- Confirm SQL Server version/collation support, locking/online-index expectations, migration downtime/transaction behavior, and max permitted deployment duration.
- Obtain M1–M7 owner sign-off, appoint the DB coordinator, and authorize the candidate migration/recovery approach. Approved business rule VCH-DEC-01 does not substitute for these schema decisions.

## 6. Isolated SQL testing prerequisites

**Current status: not authorized and not executed.** Existing M3 pre-execution readiness is explicitly blocked pending SQL execution authorization. Existing module fixtures are opt-in and are not blanket permission:

- M3 profiles use `WAYHUNTER_M3_GPS_TEST_SQL_CONNECTION` / `Wayhunter_M3_GpsConsentIntegrationTest`, `WAYHUNTER_M3_SESSION_TEST_SQL_CONNECTION` / `Wayhunter_M3_LiveSessionIntegrationTest`, `WAYHUNTER_M3_TEST_SQL_CONNECTION` / `Wayhunter_M3_RevocationIntegrationTest`, and `WAYHUNTER_M3_SAFECHECKIN_TEST_SQL_CONNECTION` / per-test `Wayhunter_M3_SafeCheckInIntegrationTest_<32 lowercase hex>`.
- M4, M5 and M6 fixtures use their respective `WAYHUNTER_M4_TEST_SQL_CONNECTION`, `WAYHUNTER_M5_TEST_SQL_CONNECTION`, and `WAYHUNTER_M6_TEST_SQL_CONNECTION` variables and fixed `Wayhunter_M4_IntegrationTest`, `Wayhunter_M5_IntegrationTest`, and `Wayhunter_M6_IntegrationTest` catalogs. M6 requires its schema to exist. Missing variables cause skips.
- Some M3 fixtures use `EnsureCreated`, which provisions the current model and does **not** verify migration application. The Safe Check-in fixture creates and drops per-test catalogs; create/drop, `ALTER`, and cleanup require explicit operation-level authorization.

Before any SQL verification, obtain and record all of the following:

1. Written approval naming the isolated disposable SQL Server/LocalDB instance, exact catalog(s), supported version/collation, authorized purpose, owner, and exclusive reservation/cleanup authority.
2. Exact test profile and allowed operations (read, write, DDL/ALTER, create/drop, cleanup, migration apply/retry, restore) separately authorized. Existing fixture opt-in is not sufficient.
3. Least-privilege identity and secure out-of-band secret delivery; validate fixture catalog allowlists and preflight the exact server/catalog without exposing connection strings.
4. Approved clean baseline or sanitized disposable data copy, data classification, backfill script review, and proof no production/shared data or provider is used.
5. Offline-reviewed migration SQL and expected schema diff; explicit approval to apply it. `EnsureCreated` must not be presented as migration verification.
6. Pre-run backup/snapshot and a successful restore rehearsal, named recovery owner/operator, rollback window, and documented response if migration fails midway.
7. Seven module-owner approvals, appointed DB coordinator approval, test cases for uniqueness/FKs/backfills/concurrency and voucher debit idempotency, and post-run evidence/cleanup plan.

No environment variable values, credentials, or connection strings should be recorded in this report.

## 7. Migration approval checklist

- [ ] Confirm this inventory against a reproducible offline current-model-versus-snapshot diff and review every generated SQL statement.
- [ ] Resolve fresh-versus-existing schema and baseline; verify target migration history in the authorized isolated environment.
- [ ] Approve each table, column, type, nullability, default, PK, alternate key, FK principal/delete behavior, index/filter, and check expression with its owner.
- [ ] Approve explicit data preflight queries, backfills, duplicate/orphan/invalid-status remediation, and expected row counts.
- [ ] Verify VCH-DEC-01 behavior in code/contracts/tests: reserve on request; debit exactly once at Admin Approve; release on reject/cancel; no debit on issuance retry.
- [ ] Validate migration on a disposable isolated SQL catalog, including fresh install and upgrade from the approved baseline; run only separately approved operations.
- [ ] Verify schema and EF migration history after apply, data invariants, FKs, unique constraints, constraints, and permitted application behavior.
- [ ] Exercise failure/restart/restore using disposable data and record recovery evidence. Do not assume a down migration reverses data loss.
- [ ] Obtain M1–M7 approvals, DB coordinator sign-off, deployment/operations approval, and final explicit authorization before any non-disposable or production rollout.
- [ ] Keep PR #293 unchanged; create a separate branch/PR for this report and for any later migration implementation, with required reviews/checks.

## 8. Next executable steps

1. Review this uncommitted inventory on a separate branch; do not add it to PR #293, whose current head is this worktree's branch.
2. Have M1, M2, M3 and M6 owners verify their listed definitions; request M4/M5/M7 cross-module compatibility confirmation and appoint the DB coordinator.
3. Record the target database baseline and make the fresh-versus-existing decision. If existing data is in scope, prepare approved read-only profiling and per-column backfill/remediation plans before migration authoring.
4. After approvals, generate a candidate migration in a dedicated branch/worktree, inspect generated code and SQL, reconcile the model snapshot, and get independent schema/security review. Do not modify existing M1–M7 worktrees.
5. Obtain explicit isolated SQL authorization matching the prerequisites above; reserve the exact disposable catalog, validate fixture behavior/permissions, test backup restore, then run approved acceptance tests only.
6. Record results, unresolved failures, recovery evidence, and owner approvals. Only then determine whether the migration is ready for a separately reviewed deployment plan.

**Readiness statement:** schema review inventory prepared; migration approval and isolated SQL verification remain outstanding. SQL migration readiness is **not complete**.
