# M3 safe check-in migration reconciliation

Baseline: feature branch at `db108c4`. The coordinator accepted the proposed schema design and serializable transaction approach for the next stage. Migration generation, application, and runtime activation remain unauthorized.

## Read-only evidence

Compared the checked-in `WayhunterSharedDbContextModelSnapshot` with the current SQL Server design-time model using EF Core's relational model differ. No migration files or SQL scripts were generated, and the model comparison did not connect to a database. The migration assembly contains only `20261117093023_InitialSharedSchema`. Its migration, designer, and checked-in idempotent SQL script do not contain the safe-check-in structures below. This compares checked-in models. The coordinator must independently inventory any deployed database and its migration history.

## Required additive safe-check-in objects

| Order | Missing from snapshot | Details |
|---|---|---|
| 1 | Alternate key on `TripSafety_LiveTripSessions` | `AK_TripSafety_LiveTripSessions_SessionId_TripPlanId` on `(SessionId, TripPlanId)`. Preserve the session PK and filtered open-plan index. |
| 2 | `TripSafety_SafeCheckIns` | GUID `CheckInId` PK; required NO ACTION FKs to `Trips.Id`, `Users.UserId`, and the exact session alternate key. Missing indexes are `UX_TripSafety_SafeCheckIns_SessionId_UserId`, `IX_TripSafety_SafeCheckIns_SessionId_TripPlanId`, `IX_TripSafety_SafeCheckIns_TripPlanId`, and `IX_TripSafety_SafeCheckIns_UserId`. |
| 3 | `TripSafety_SafeCheckInOutbox` | GUID event PK; required NO ACTION FKs to `TripSafety_SafeCheckIns.CheckInId` and recipient `Users.UserId`. Missing indexes are `UX_TripSafety_SafeCheckInOutbox_CheckInId`, `IX_TripSafety_SafeCheckInOutbox_RecipientUserId`, `IX_TripSafety_SafeCheckInOutbox_PendingDue`, and `IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry`. Missing checks are `CK_TripSafety_SafeCheckInOutbox_Attempts`, `CK_TripSafety_SafeCheckInOutbox_DeliveryState`, `CK_TripSafety_SafeCheckInOutbox_LeasePair`, and `CK_TripSafety_SafeCheckInOutbox_TerminalState`. |

The M1 user FKs use the existing `nvarchar(256)` identity key. The trip/session FK depends on the alternate key above. The check-in and outbox are deployed before any writer or worker is enabled. No dependency on the membership-revocation outbox table is introduced by the safe-check-in outbox itself.

## Other differences in the same shared model

| Area | Read-only differences outside the safe-check-in objects |
|---|---|
| M3 membership revocation | Missing `TripSafety_MemberRevocationOutbox` table, including the lease, attempt and dead-letter fields and its checks and member FK. Missing `IX_TripSafety_MemberRevocationOutbox_Pending` and `UX_TripSafety_MemberRevocationOutbox_MemberId`. The existing SQL member-removal writer uses this outbox, so validate the approved removal schema and writer when testing revocation races end to end. |
| M3 hazards | Adds `Description`, `Latitude`, `Longitude`, and `PhotoAssetReference` to `TripSafety_HazardReports`, plus `CK_TripSafety_HazardReports_Coordinates`. |
| Identity | Adds `IdentityPasswordResetCredentials` with `IX_IdentityPasswordResetCredentials_User_Expiry` and `UX_IdentityPasswordResetCredentials_TokenHash`. Adds `UserRoleAssignments` with `IX_UserRoleAssignments_AssignedAtUtc`, `IX_UserRoleAssignments_UserId_Status`, and `UX_UserRoleAssignments_Active_UserId_Role`. |
| M2 trip copy | Adds `TripPlanCopyAttributions` and `IX_TripPlanCopyAttributions_SourceTripPlanId`. |
| M6 voucher requests | Adds `DecisionActorUserId`, `DecisionReason`, and `OwnerNameSnapshot` to `SampleVoucherRedemptionRequests`. |
| M6 voucher issuances | Alters `InventoryId`; adds `AttemptCount`, `ExpiresAtUtc`, `LastErrorCode`, `RowVersion`, `UseReason`, `UsedAtUtc`, and `UsedByActorUserId`; adds the attempts check; drops and recreates the status check and unique inventory index. |
| M6 voucher inventory | Adds `AddedAtUtc`, `AddedByAdminUserId`, `ExpiresAtUtc`, `RowVersion`, and `SampleImageReference`, plus the expiry check. |
| M6 Coins | Adds `CreatedAtUtc`, `CreatedByUserId`, `EffectiveFromUtc`, and `EffectiveUntilUtc` to `CoinRuleVersions`; adds `ActorUserId`, `Reason`, `UX_CoinLedgerEntries_CorrectionOf`, and `UX_CoinLedgerEntries_Wallet_NonRewardSource` to `CoinLedgerEntries`. |

A migration generated from the whole model would pick up these differences, including changes to existing voucher constraints and indexes. **DECISION_REQUIRED** for shared migration sequencing and ownership of the unrelated drift, plus deployed-schema verification. The coordinator should review the selected operation set, including the related snapshot transition, and explicitly account for any previously deployed changes before generating or running an approved additive migration. Leave `InitialSharedSchema` and the existing model snapshot unchanged at this stage.
