# Unrelated EF model drift at `origin/develop` `13553911febb44343bf51227c08db470712bf804`

This inventory comes from a separate EF-generated `FullModelDriftInventory` scaffold using the current `WayhunterSharedDbContext` against the latest committed snapshot plus the approved reset mapping. The scratch migration was inspected and removed; none of its operations are in the reset migration or reset-only snapshot.

## Additional tables detected

- **M1:** `UserRoleAssignments`.
- **M2:** `TripPlanCopyAttributions`.
- **M3:** `TripSafety_MemberRevocationOutbox`, `TripSafety_SafeCheckIns`, `TripSafety_SafeCheckInOutbox`, `TripSafety_TripMemories`, and `TripSafety_TripMemoryMedia`.
- **M8:** `TripSafety_NotificationAudienceReceipts`, `TripSafety_NotificationDevices`, `TripSafety_NotificationEvents`, `TripSafety_NotificationJobRecipients`, `TripSafety_NotificationJobs`, `TripSafety_NotificationOperations`, `TripSafety_NotificationPreferences`, `TripSafety_NotificationRecipients`, `TripSafety_NotificationTokenBindings`, and `TripSafety_NotificationDeliveryAudit`.

## Existing table changes detected

- **M2:** Add unique alternate key `AK_TripStops_Id_TripPlanId` on (`Id`, `TripPlanId`).
- **M3:** Add unique alternate key `AK_TripSafety_LiveTripSessions_SessionId_TripPlanId` on (`SessionId`, `TripPlanId`). Add nullable `Description`, `Latitude`, `Longitude`, and `PhotoAssetReference` to `TripSafety_HazardReports`, plus `CK_TripSafety_HazardReports_Coordinates`.
- **M6:** Add `DecisionActorUserId`, `DecisionReason`, and `OwnerNameSnapshot` to `SampleVoucherRedemptionRequests`. Make `SampleVoucherIssuances.InventoryId` nullable; add `AttemptCount`, `ExpiresAtUtc`, `LastErrorCode`, `RowVersion`, `UseReason`, `UsedAtUtc`, and `UsedByActorUserId`; replace its inventory unique index with a filtered index and update/add voucher checks. Add `AddedAtUtc`, `AddedByAdminUserId`, `ExpiresAtUtc`, `RowVersion`, and `SampleImageReference` to `SampleVoucherInventory`, plus its expiry check. Add `CreatedAtUtc`, `CreatedByUserId`, `EffectiveFromUtc`, and `EffectiveUntilUtc` to `CoinRuleVersions`. Add `ActorUserId` and `Reason` to `CoinLedgerEntries`, plus unique indexes for correction links and non-reward wallet/source pairs.

The full-model scaffold also detects the new M3/M8 indexes, foreign keys, and checks that accompany the listed tables. M4, M5, and M7 contributed no additional schema operations in this scaffold. These are **separate pending schema decisions** and are intentionally absent from the reset-only migration, designer, and snapshot delta.
