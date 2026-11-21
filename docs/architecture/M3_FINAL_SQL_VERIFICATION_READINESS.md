# M3 final SQL verification readiness

**BLOCKED — EXPLICIT SQL EXECUTION AUTHORIZATION REQUIRED.** SQL test bodies executed: 0. Backend baseline `13553911febb44343bf51227c08db470712bf804` matched fetched develop. SecondBrain `42a5cf38001c1afbb0c69e75718c0aa615bdc39d` supplied the active rules, milestones and team workflow. Conditional schema review does not authorize SQL. See the [final pre-execution checklist](M3_FINAL_SQL_PREEXECUTION_AUTHORIZATION_CHECKLIST.md) for post-merge target identity, permissions and outstanding sign-off.

## Inventory

VSTest discovery returned 45 tests. Profile references apply the operation, dependency and cleanup rules in the next section.

| Exact fully qualified test name | Profile | Intended write/assertion |
|---|---|---|
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.AcceptedInvitation_ShouldNotAutoGrantGps` | GPS | Accept invitation and create membership; no consent row |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.Grant_ShouldPersistAndAllowLocationAccess` | GPS | Grant consent; cross-context authorization succeeds |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.Revoke_ShouldPersistAndDenyLocationAccess` | GPS | Revoke existing consent; publishing denied |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.NonMember_ShouldNotCreateGpsConsent` | GPS | Nonmember rejected; no consent insert |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.WrongTripOrSession_ShouldDenyConsent` | GPS | Mismatched trip/session rejected; no consent write |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.CommitFailure_ShouldRollbackGpsConsent` | GPS | Injected commit failure; no consent commit |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.MemberRemoval_ShouldDenyGpsAndRegrant` | GPS | Remove seeded member; publishing and regrant denied |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.FinishedSession_ShouldRejectConsentChange` | GPS | Finish session; consent mutation rejected |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.PausedSession_ShouldDenyLocationPublishing` | GPS | Pause session; location access denied |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3GpsConsentSqlTests.RepeatedGrantRevoke_ShouldRemainIdempotent` | GPS | Grant/revoke retries preserve state and row identity |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3InvitationDecisionSqlTests.Accept_ShouldPersistInvitationAndMembership` | Invitation | Accept pending invitation and insert membership together |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3InvitationDecisionSqlTests.Reject_ShouldNotCreateMembership` | Invitation | Reject pending invitation; no membership |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3InvitationDecisionSqlTests.RepeatedAccept_ShouldNotDuplicateMembership` | Invitation | Replay acceptance; one membership |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3InvitationDecisionSqlTests.CommitFailure_ShouldRollbackInvitationAndMember` | Invitation | Injected commit failure; pending invitation and no membership |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.Start_ShouldPersistActiveSession` | Session | Start writes active row |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.RepeatedStart_ShouldReturnExistingSession` | Session | Repeated start returns one open row |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.PauseResume_ShouldPersistAcrossContexts` | Session | Pause/resume persists across contexts |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.Finish_ShouldCloseOpenSession` | Session | Finish updates row and closes open slot |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.FinishedSession_ShouldAllowNewSession` | Session | Start after finish preserves history |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.RepeatedFinish_ShouldNotChangeTimestamp` | Session | Repeated finish retains timestamp |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.DuplicateOpenSession_ShouldBeRejected` | Session | Duplicate open session rejected by repository |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.SqlUniqueIndex_ShouldRejectSecondOpenSession` | Session | Direct duplicate insert rejected by filtered index |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.Recovery_ShouldReturnOnlyOpenSessions` | Session | Recover open sessions only |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.Recovery_ShouldRejectUnauthorizedUser` | Session | Unauthorized recovery rejected |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.InvalidTransitions_ShouldNotCorruptSqlState` | Session | Invalid transitions leave state unchanged |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3LiveTripSessionSqlTests.UnknownSession_UpdateShouldFail` | Session | Unknown update rejected |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3MembershipRevocationSqlConcurrencyTests.ConcurrentRemoval_ExactlyOne_ShouldSucceed` | Revocation | Parallel removal yields one outbox event |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3MembershipRevocationSqlConcurrencyTests.RepeatedRemoval_ShouldNotDuplicateOutbox` | Revocation | Repeated removal preserves one event |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3MembershipRevocationSqlRollbackTests.CommitFailure_ShouldRollbackMembershipAndOutbox` | Revocation | Commit failure rolls back membership and outbox |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.ConcurrentCheckIns_CommitOneRecordAndOneOwnerEvent` | Safe | Parallel check-ins commit one row and outbox event |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.InvalidActor_InactiveMembership_CrossTripAndIneligibleState_AreRejected` | Safe | Reject invalid actor, membership, cross-trip and state |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.FinishedSession_AuthorizedRetryReturnsOriginalRecordAndEvent` | Safe | Authorized replay after finish preserves event |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.FinishedSession_RejectsFirstCheckInAndFormerMemberRetry` | Safe | Reject first check-in and former member replay after finish |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.FinishedSession_RejectsRetryFromSuspendedAccountWithActiveMembership` | Safe | Suspended account cannot replay |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.FinishedSession_RejectsRetryForCancelledTrip` | Safe | Cancelled trip cannot replay |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.FinishedSession_RejectsRetryWhenCompanionEventCannotBeVerified` | Safe | Delete companion event; reject replay |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.CompetingFinishHoldingSessionLock_PreventsNewCheckIn` | Safe | Competing finish transaction blocks new check-in |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.CompetingRemovalHoldingMembershipLock_PreventsNewCheckIn` | Safe | Competing member removal blocks check-in |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.CheckInCommitsBeforeCompetingFinish_AndKeepsItsHistoricalEvent` | Safe | Check-in commits before finish and retains history |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.OutboxInsertFailure_RollsBackCheckIn` | Safe | Rejecting CHECK constraint causes atomic rollback |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.ParallelWorkers_LeaseBeforeSend_AndFenceStaleAcknowledgement` | Safe | Parallel claims fence stale acknowledgement |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.Failure_RetriesWithStableIdAndDeadLettersAtAttemptLimit` | Safe | Stable ID retries and dead-letter |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.RecipientChange_NeverSendsToFormerOwnerOrRedirectsEvent` | Safe | Owner changed; suppress rather than redirect |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SafeCheckInSqlTests.CancelledFinalLease_ExpiresAndDeadLettersWithoutSecondSend` | Safe | Expired final lease dead-letters without duplicate send |
| `Wayhunter.IntegrationTests.Modules.TripSafety.M3SqlExecutionStrategyTests.RetryStrategy_ShouldAllowAtomicRevocation` | Strategy | Retry-enabled revocation commits membership and outbox together |

## Profiles and cleanup

All profiles now require exact `(localdb)\MSSQLLocalDB`, integrated security and exact catalog. The test code rejects unsupported connection options and reconstructs the connection. No fixture applies a shared migration.

| Profile | Environment and target | Operations, dependencies and cleanup |
|---|---|---|
| GPS | `WAYHUNTER_M3_GPS_TEST_SQL_CONNECTION`, `Wayhunter_M3_GpsConsentIntegrationTest` | Shared-model EnsureCreated; seed accounts/trip/invitation/member/session; consent and revocation writes and injected rollback; test-keyed outbox, consent, member, invite, session, trip and user deletes. Fixed catalog retained. |
| Session | `WAYHUNTER_M3_SESSION_TEST_SQL_CONNECTION`, `Wayhunter_M3_LiveSessionIntegrationTest` | Shared-model EnsureCreated; lifecycle writes and direct unique index conflict; test-keyed session, trip and user deletes. Fixed catalog retained. |
| Revocation | `WAYHUNTER_M3_TEST_SQL_CONNECTION`, `Wayhunter_M3_RevocationIntegrationTest` | All four REV classes call the same shared-model EnsureCreated helper directly or via their seed setup; transactional member/outbox writes; rollback class injects commit failure. Test-keyed outbox/member/invite/trip/user deletes. |
| Invitation / Strategy | Same Revocation target | Both classes invoke the revocation concurrency class bootstrap helper before seeding; no test-order dependency within one process. Atomic accept/reject or retry-enabled revocation; injected invitation rollback. Each cleans its seeded relationship, trip and account rows. |
| Safe | `WAYHUNTER_M3_SAFECHECKIN_TEST_SQL_CONNECTION`, template `Wayhunter_M3_SafeCheckInIntegrationTest` | Each fixture creates `Wayhunter_M3_SafeCheckInIntegrationTest_<32 lowercase hex GUID>` using its reduced model. Drop is gated by the exact private connection and EnsureCreated success flag. That flag alone cannot prove creation ownership of an existing empty catalog. See the final checklist for the pending drop blocker. One test uses an unfiltered companion-outbox delete in its isolated catalog. One adds a rejecting CHECK via ALTER TABLE. Other tests exercise competing writes, claim fencing, retries and fake sends. |

## Read-only EF reconciliation

The design-time SQL Server relational model was compared with both the initialized EF snapshot and the target model of `20261117093023_InitialSharedSchema`. Both checked-in baselines agree for the inspected M3 tables and TripStops. Live session, GPS, invitation, member and hazard are present. The current-only structures below are model evidence. Deployed schema was outside the comparison. M3 owns TripSafety dependents. M2 owns Trips/TripStops and coordinates the stop composite key; M1 owns Users. Create principal composite keys before dependent FKs. Coordinate unrelated owner drift separately.

```text
TripSafety_HazardReports CHECK CK_TripSafety_HazardReports_Coordinates: ([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)
TripSafety_HazardReports COLUMN Description nvarchar(1000)
TripSafety_HazardReports COLUMN Latitude float
TripSafety_HazardReports COLUMN Longitude float
TripSafety_HazardReports COLUMN PhotoAssetReference nvarchar(512)
TripSafety_LiveTripSessions KEY AK_TripSafety_LiveTripSessions_SessionId_TripPlanId (SessionId,TripPlanId)
TripSafety_MemberRevocationOutbox CHECK CK_TripSafety_MemberRevocationOutbox_Attempts: [DeliveryAttempts] >= 0
TripSafety_MemberRevocationOutbox CHECK CK_TripSafety_MemberRevocationOutbox_DeliveryState: ([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL)
TripSafety_MemberRevocationOutbox COLUMN CreatedAtUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN DeadLetteredAtUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN DeliveredAtUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN DeliveryAttempts int
TripSafety_MemberRevocationOutbox COLUMN Id uniqueidentifier
TripSafety_MemberRevocationOutbox COLUMN IsDelivered bit
TripSafety_MemberRevocationOutbox COLUMN LastError nvarchar(256)
TripSafety_MemberRevocationOutbox COLUMN LeaseToken uniqueidentifier
TripSafety_MemberRevocationOutbox COLUMN LeaseUntilUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN MemberId uniqueidentifier
TripSafety_MemberRevocationOutbox COLUMN MemberUserId nvarchar(256)
TripSafety_MemberRevocationOutbox COLUMN NextAttemptAtUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN RemovedAtUtc datetimeoffset
TripSafety_MemberRevocationOutbox COLUMN TripPlanId uniqueidentifier
TripSafety_MemberRevocationOutbox FK FK_TripSafety_MemberRevocationOutbox_TripSafety_TripMembers_MemberId (MemberId) -> TripSafety_TripMembers(Id) NoAction
TripSafety_MemberRevocationOutbox INDEX IX_TripSafety_MemberRevocationOutbox_Pending (IsDelivered,CreatedAtUtc) unique=False filter=
TripSafety_MemberRevocationOutbox INDEX UX_TripSafety_MemberRevocationOutbox_MemberId (MemberId) unique=True filter=
TripSafety_MemberRevocationOutbox KEY PK_TripSafety_MemberRevocationOutbox (Id)
TripSafety_MemberRevocationOutbox TABLE
TripSafety_NotificationAudienceReceipts CHECK CK_MS08_NotificationAudience_Count: [Count] BETWEEN 1 AND 100
TripSafety_NotificationAudienceReceipts COLUMN ActorId nvarchar(256)
TripSafety_NotificationAudienceReceipts COLUMN ConsumedAtUtc datetimeoffset
TripSafety_NotificationAudienceReceipts COLUMN Count int
TripSafety_NotificationAudienceReceipts COLUMN ExpiresAtUtc datetimeoffset
TripSafety_NotificationAudienceReceipts COLUMN Fingerprint varchar(64)
TripSafety_NotificationAudienceReceipts COLUMN ReceiptId uniqueidentifier
TripSafety_NotificationAudienceReceipts FK FK_TripSafety_NotificationAudienceReceipts_Users_ActorId (ActorId) -> Users(UserId) NoAction
TripSafety_NotificationAudienceReceipts INDEX IX_TripSafety_NotificationAudienceReceipts_ActorId (ActorId) unique=False filter=
TripSafety_NotificationAudienceReceipts INDEX IX_TripSafety_NotificationAudienceReceipts_ExpiresAtUtc (ExpiresAtUtc) unique=False filter=
TripSafety_NotificationAudienceReceipts KEY PK_TripSafety_NotificationAudienceReceipts (ReceiptId)
TripSafety_NotificationAudienceReceipts TABLE
TripSafety_NotificationDeliveryAudit COLUMN Attempt int
TripSafety_NotificationDeliveryAudit COLUMN AuditId uniqueidentifier
TripSafety_NotificationDeliveryAudit COLUMN EventId uniqueidentifier
TripSafety_NotificationDeliveryAudit COLUMN Outcome varchar(48)
TripSafety_NotificationDeliveryAudit COLUMN RecordedAtUtc datetimeoffset
TripSafety_NotificationDeliveryAudit COLUMN UserId nvarchar(256)
TripSafety_NotificationDeliveryAudit FK FK_TripSafety_NotificationDeliveryAudit_TripSafety_NotificationRecipients_EventId_UserId (EventId,UserId) -> TripSafety_NotificationRecipients(EventId,UserId) NoAction
TripSafety_NotificationDeliveryAudit INDEX IX_TripSafety_NotificationDeliveryAudit_EventId_UserId (EventId,UserId) unique=False filter=
TripSafety_NotificationDeliveryAudit KEY PK_TripSafety_NotificationDeliveryAudit (AuditId)
TripSafety_NotificationDeliveryAudit TABLE
TripSafety_NotificationDevices COLUMN ChangedAtUtc datetimeoffset
TripSafety_NotificationDevices COLUMN DeviceId uniqueidentifier
TripSafety_NotificationDevices COLUMN ProtectedToken varbinary(max)
TripSafety_NotificationDevices COLUMN PushAllowed bit
TripSafety_NotificationDevices COLUMN RevokedAtUtc datetimeoffset
TripSafety_NotificationDevices COLUMN RowVersion rowversion
TripSafety_NotificationDevices COLUMN TokenFingerprint varchar(64)
TripSafety_NotificationDevices COLUMN UserId nvarchar(256)
TripSafety_NotificationDevices FK FK_TripSafety_NotificationDevices_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_NotificationDevices INDEX IX_TripSafety_NotificationDevices_TokenFingerprint (TokenFingerprint) unique=True filter=
TripSafety_NotificationDevices INDEX IX_TripSafety_NotificationDevices_UserId_RevokedAtUtc (UserId,RevokedAtUtc) unique=False filter=
TripSafety_NotificationDevices KEY PK_TripSafety_NotificationDevices (DeviceId)
TripSafety_NotificationDevices TABLE
TripSafety_NotificationEvents CHECK CK_MS08_NotificationEvent_Template: [Category] BETWEEN 1 AND 7 AND [Template] BETWEEN 1 AND 7 AND [TemplateVersion] > 0
TripSafety_NotificationEvents COLUMN Category int
TripSafety_NotificationEvents COLUMN CreatedAtUtc datetimeoffset
TripSafety_NotificationEvents COLUMN EventId uniqueidentifier
TripSafety_NotificationEvents COLUMN JobId uniqueidentifier
TripSafety_NotificationEvents COLUMN TargetId uniqueidentifier
TripSafety_NotificationEvents COLUMN Template int
TripSafety_NotificationEvents COLUMN TemplateVersion int
TripSafety_NotificationEvents FK FK_TripSafety_NotificationEvents_TripSafety_NotificationJobs_JobId (JobId) -> TripSafety_NotificationJobs(JobId) NoAction
TripSafety_NotificationEvents INDEX IX_TripSafety_NotificationEvents_JobId (JobId) unique=True filter=[JobId] IS NOT NULL
TripSafety_NotificationEvents KEY PK_TripSafety_NotificationEvents (EventId)
TripSafety_NotificationEvents TABLE
TripSafety_NotificationJobRecipients COLUMN JobId uniqueidentifier
TripSafety_NotificationJobRecipients COLUMN UserId nvarchar(256)
TripSafety_NotificationJobRecipients FK FK_TripSafety_NotificationJobRecipients_TripSafety_NotificationJobs_JobId (JobId) -> TripSafety_NotificationJobs(JobId) NoAction
TripSafety_NotificationJobRecipients FK FK_TripSafety_NotificationJobRecipients_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_NotificationJobRecipients INDEX IX_TripSafety_NotificationJobRecipients_UserId (UserId) unique=False filter=
TripSafety_NotificationJobRecipients KEY PK_TripSafety_NotificationJobRecipients (JobId,UserId)
TripSafety_NotificationJobRecipients TABLE
TripSafety_NotificationJobs CHECK CK_MS08_NotificationJob_Kind: ([Kind] = 1 AND [TripPlanId] IS NOT NULL AND [SessionId] IS NOT NULL AND [TripVersion] IS NOT NULL AND [PlannedStartDate] IS NOT NULL) OR ([Kind] = 2 AND [TripPlanId] IS NULL AND [SessionId] IS NULL AND [TripVersion] IS NULL AND [PlannedStartDate] IS NULL AND [AudienceFingerprint] IS NOT NULL)
TripSafety_NotificationJobs CHECK CK_MS08_NotificationJob_State: [State] BETWEEN 0 AND 2 AND [Revision] > 0
TripSafety_NotificationJobs COLUMN ActorId nvarchar(256)
TripSafety_NotificationJobs COLUMN AudienceDistrict nvarchar(40)
TripSafety_NotificationJobs COLUMN AudienceFingerprint varchar(64)
TripSafety_NotificationJobs COLUMN AudienceRole int
TripSafety_NotificationJobs COLUMN DueAtUtc datetimeoffset
TripSafety_NotificationJobs COLUMN JobId uniqueidentifier
TripSafety_NotificationJobs COLUMN Kind int
TripSafety_NotificationJobs COLUMN PlannedStartDate date
TripSafety_NotificationJobs COLUMN Revision int
TripSafety_NotificationJobs COLUMN RowVersion rowversion
TripSafety_NotificationJobs COLUMN SessionId uniqueidentifier
TripSafety_NotificationJobs COLUMN State int
TripSafety_NotificationJobs COLUMN TargetId uniqueidentifier
TripSafety_NotificationJobs COLUMN Template int
TripSafety_NotificationJobs COLUMN TemplateVersion int
TripSafety_NotificationJobs COLUMN TripPlanId uniqueidentifier
TripSafety_NotificationJobs COLUMN TripVersion varchar(16)
TripSafety_NotificationJobs FK FK_TripSafety_NotificationJobs_TripSafety_LiveTripSessions_SessionId_TripPlanId (SessionId,TripPlanId) -> TripSafety_LiveTripSessions(SessionId,TripPlanId) NoAction
TripSafety_NotificationJobs FK FK_TripSafety_NotificationJobs_Trips_TripPlanId (TripPlanId) -> Trips(Id) NoAction
TripSafety_NotificationJobs FK FK_TripSafety_NotificationJobs_Users_ActorId (ActorId) -> Users(UserId) NoAction
TripSafety_NotificationJobs INDEX IX_TripSafety_NotificationJobs_ActorId (ActorId) unique=False filter=
TripSafety_NotificationJobs INDEX IX_TripSafety_NotificationJobs_SessionId_TripPlanId (SessionId,TripPlanId) unique=False filter=
TripSafety_NotificationJobs INDEX IX_TripSafety_NotificationJobs_State_DueAtUtc (State,DueAtUtc) unique=False filter=
TripSafety_NotificationJobs INDEX IX_TripSafety_NotificationJobs_TripPlanId_SessionId_ActorId (TripPlanId,SessionId,ActorId) unique=True filter=[Kind] = 1
TripSafety_NotificationJobs KEY PK_TripSafety_NotificationJobs (JobId)
TripSafety_NotificationJobs TABLE
TripSafety_NotificationOperations COLUMN Action varchar(32)
TripSafety_NotificationOperations COLUMN ActorId nvarchar(256)
TripSafety_NotificationOperations COLUMN DueAtUtc datetimeoffset
TripSafety_NotificationOperations COLUMN Fingerprint varchar(64)
TripSafety_NotificationOperations COLUMN JobState int
TripSafety_NotificationOperations COLUMN OperationId uniqueidentifier
TripSafety_NotificationOperations COLUMN RecordedAtUtc datetimeoffset
TripSafety_NotificationOperations COLUMN ResourceId uniqueidentifier
TripSafety_NotificationOperations COLUMN Revision int
TripSafety_NotificationOperations FK FK_TripSafety_NotificationOperations_Users_ActorId (ActorId) -> Users(UserId) NoAction
TripSafety_NotificationOperations INDEX IX_TripSafety_NotificationOperations_ResourceId_RecordedAtUtc (ResourceId,RecordedAtUtc) unique=False filter=
TripSafety_NotificationOperations KEY PK_TripSafety_NotificationOperations (ActorId,OperationId)
TripSafety_NotificationOperations TABLE
TripSafety_NotificationPreferences CHECK CK_MS08_NotificationPreference_Category: [Category] BETWEEN 1 AND 7
TripSafety_NotificationPreferences COLUMN Category int
TripSafety_NotificationPreferences COLUMN PushEnabled bit
TripSafety_NotificationPreferences COLUMN UserId nvarchar(256)
TripSafety_NotificationPreferences FK FK_TripSafety_NotificationPreferences_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_NotificationPreferences KEY PK_TripSafety_NotificationPreferences (UserId,Category)
TripSafety_NotificationPreferences TABLE
TripSafety_NotificationRecipients CHECK CK_MS08_NotificationRecipient_Lease: ([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([State] = 0 AND [LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)
TripSafety_NotificationRecipients CHECK CK_MS08_NotificationRecipient_State: [Attempts] >= 0 AND (([State] = 0 AND [CompletedAtUtc] IS NULL) OR ([State] BETWEEN 1 AND 3 AND [CompletedAtUtc] IS NOT NULL AND [NextAttemptAtUtc] IS NULL))
TripSafety_NotificationRecipients COLUMN Attempts int
TripSafety_NotificationRecipients COLUMN CompletedAtUtc datetimeoffset
TripSafety_NotificationRecipients COLUMN EventId uniqueidentifier
TripSafety_NotificationRecipients COLUMN LastOutcome nvarchar(48)
TripSafety_NotificationRecipients COLUMN LeaseToken uniqueidentifier
TripSafety_NotificationRecipients COLUMN LeaseUntilUtc datetimeoffset
TripSafety_NotificationRecipients COLUMN NextAttemptAtUtc datetimeoffset
TripSafety_NotificationRecipients COLUMN ReadAtUtc datetimeoffset
TripSafety_NotificationRecipients COLUMN State int
TripSafety_NotificationRecipients COLUMN UserId nvarchar(256)
TripSafety_NotificationRecipients FK FK_TripSafety_NotificationRecipients_TripSafety_NotificationEvents_EventId (EventId) -> TripSafety_NotificationEvents(EventId) NoAction
TripSafety_NotificationRecipients FK FK_TripSafety_NotificationRecipients_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_NotificationRecipients INDEX IX_TripSafety_NotificationRecipients_NextAttemptAtUtc_LeaseUntilUtc (NextAttemptAtUtc,LeaseUntilUtc) unique=False filter=[State] = 0
TripSafety_NotificationRecipients INDEX IX_TripSafety_NotificationRecipients_UserId_ReadAtUtc_EventId (UserId,ReadAtUtc,EventId) unique=False filter=
TripSafety_NotificationRecipients KEY PK_TripSafety_NotificationRecipients (EventId,UserId)
TripSafety_NotificationRecipients TABLE
TripSafety_NotificationTokenBindings COLUMN DeviceId uniqueidentifier
TripSafety_NotificationTokenBindings COLUMN Fingerprint varchar(64)
TripSafety_NotificationTokenBindings COLUMN RecordedAtUtc datetimeoffset
TripSafety_NotificationTokenBindings COLUMN UserId nvarchar(256)
TripSafety_NotificationTokenBindings FK FK_TripSafety_NotificationTokenBindings_TripSafety_NotificationDevices_DeviceId (DeviceId) -> TripSafety_NotificationDevices(DeviceId) NoAction
TripSafety_NotificationTokenBindings FK FK_TripSafety_NotificationTokenBindings_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_NotificationTokenBindings INDEX IX_TripSafety_NotificationTokenBindings_DeviceId (DeviceId) unique=False filter=
TripSafety_NotificationTokenBindings INDEX IX_TripSafety_NotificationTokenBindings_UserId (UserId) unique=False filter=
TripSafety_NotificationTokenBindings KEY PK_TripSafety_NotificationTokenBindings (Fingerprint)
TripSafety_NotificationTokenBindings TABLE
TripSafety_SafeCheckInOutbox CHECK CK_TripSafety_SafeCheckInOutbox_Attempts: [DeliveryAttempts] >= 0
TripSafety_SafeCheckInOutbox CHECK CK_TripSafety_SafeCheckInOutbox_DeliveryState: ([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL AND [DeadLetteredAtUtc] IS NULL)
TripSafety_SafeCheckInOutbox CHECK CK_TripSafety_SafeCheckInOutbox_LeasePair: ([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)
TripSafety_SafeCheckInOutbox CHECK CK_TripSafety_SafeCheckInOutbox_TerminalState: ([IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL) OR ([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)
TripSafety_SafeCheckInOutbox COLUMN CheckInId uniqueidentifier
TripSafety_SafeCheckInOutbox COLUMN CreatedAtUtc datetimeoffset(7)
TripSafety_SafeCheckInOutbox COLUMN DeadLetteredAtUtc datetimeoffset(7)
TripSafety_SafeCheckInOutbox COLUMN DeliveredAtUtc datetimeoffset(7)
TripSafety_SafeCheckInOutbox COLUMN DeliveryAttempts int
TripSafety_SafeCheckInOutbox COLUMN Id uniqueidentifier
TripSafety_SafeCheckInOutbox COLUMN IsDelivered bit
TripSafety_SafeCheckInOutbox COLUMN LastError nvarchar(256)
TripSafety_SafeCheckInOutbox COLUMN LeaseToken uniqueidentifier
TripSafety_SafeCheckInOutbox COLUMN LeaseUntilUtc datetimeoffset(7)
TripSafety_SafeCheckInOutbox COLUMN NextAttemptAtUtc datetimeoffset(7)
TripSafety_SafeCheckInOutbox COLUMN RecipientUserId nvarchar(256)
TripSafety_SafeCheckInOutbox FK FK_TripSafety_SafeCheckInOutbox_SafeCheckIns_CheckInId (CheckInId) -> TripSafety_SafeCheckIns(CheckInId) NoAction
TripSafety_SafeCheckInOutbox FK FK_TripSafety_SafeCheckInOutbox_Users_RecipientUserId (RecipientUserId) -> Users(UserId) NoAction
TripSafety_SafeCheckInOutbox INDEX IX_TripSafety_SafeCheckInOutbox_PendingDue (NextAttemptAtUtc,CreatedAtUtc,Id) unique=False filter=[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL
TripSafety_SafeCheckInOutbox INDEX IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry (LeaseUntilUtc,Id) unique=False filter=[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL AND [LeaseUntilUtc] IS NOT NULL
TripSafety_SafeCheckInOutbox INDEX IX_TripSafety_SafeCheckInOutbox_RecipientUserId (RecipientUserId) unique=False filter=
TripSafety_SafeCheckInOutbox INDEX UX_TripSafety_SafeCheckInOutbox_CheckInId (CheckInId) unique=True filter=
TripSafety_SafeCheckInOutbox KEY PK_TripSafety_SafeCheckInOutbox (Id)
TripSafety_SafeCheckInOutbox TABLE
TripSafety_SafeCheckIns COLUMN CheckInId uniqueidentifier
TripSafety_SafeCheckIns COLUMN CheckedInAtUtc datetimeoffset(7)
TripSafety_SafeCheckIns COLUMN SessionId uniqueidentifier
TripSafety_SafeCheckIns COLUMN TripPlanId uniqueidentifier
TripSafety_SafeCheckIns COLUMN UserId nvarchar(256)
TripSafety_SafeCheckIns FK FK_TripSafety_SafeCheckIns_LiveTripSessions_SessionId_TripPlanId (SessionId,TripPlanId) -> TripSafety_LiveTripSessions(SessionId,TripPlanId) NoAction
TripSafety_SafeCheckIns FK FK_TripSafety_SafeCheckIns_Trips_TripPlanId (TripPlanId) -> Trips(Id) NoAction
TripSafety_SafeCheckIns FK FK_TripSafety_SafeCheckIns_Users_UserId (UserId) -> Users(UserId) NoAction
TripSafety_SafeCheckIns INDEX IX_TripSafety_SafeCheckIns_SessionId_TripPlanId (SessionId,TripPlanId) unique=False filter=
TripSafety_SafeCheckIns INDEX IX_TripSafety_SafeCheckIns_TripPlanId (TripPlanId) unique=False filter=
TripSafety_SafeCheckIns INDEX IX_TripSafety_SafeCheckIns_UserId (UserId) unique=False filter=
TripSafety_SafeCheckIns INDEX UX_TripSafety_SafeCheckIns_SessionId_UserId (SessionId,UserId) unique=True filter=
TripSafety_SafeCheckIns KEY PK_TripSafety_SafeCheckIns (CheckInId)
TripSafety_SafeCheckIns TABLE
TripSafety_TripMemories CHECK CK_TripSafety_TripMemories_Timestamps: [UpdatedAtUtc] >= [CreatedAtUtc] AND ([DeletedAtUtc] IS NULL OR [DeletedAtUtc] >= [CreatedAtUtc])
TripSafety_TripMemories CHECK CK_TripSafety_TripMemories_Visibility: [Visibility] IN (0, 1)
TripSafety_TripMemories COLUMN Caption nvarchar(1000)
TripSafety_TripMemories COLUMN CreateOperationId uniqueidentifier
TripSafety_TripMemories COLUMN CreatedAtUtc datetimeoffset
TripSafety_TripMemories COLUMN DeletedAtUtc datetimeoffset
TripSafety_TripMemories COLUMN MemoryId uniqueidentifier
TripSafety_TripMemories COLUMN OwnerUserId nvarchar(256)
TripSafety_TripMemories COLUMN RowVersion rowversion
TripSafety_TripMemories COLUMN TripPlanId uniqueidentifier
TripSafety_TripMemories COLUMN TripStopId uniqueidentifier
TripSafety_TripMemories COLUMN UpdatedAtUtc datetimeoffset
TripSafety_TripMemories COLUMN Visibility int
TripSafety_TripMemories FK FK_TripSafety_TripMemories_TripStops_TripStopId_TripPlanId (TripStopId,TripPlanId) -> TripStops(Id,TripPlanId) NoAction
TripSafety_TripMemories FK FK_TripSafety_TripMemories_Trips_TripPlanId (TripPlanId) -> Trips(Id) NoAction
TripSafety_TripMemories FK FK_TripSafety_TripMemories_Users_OwnerUserId (OwnerUserId) -> Users(UserId) NoAction
TripSafety_TripMemories INDEX IX_TripSafety_TripMemories_OwnerUserId (OwnerUserId) unique=False filter=
TripSafety_TripMemories INDEX IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreateOperationId (TripPlanId,OwnerUserId,CreateOperationId) unique=True filter=
TripSafety_TripMemories INDEX IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreatedAtUtc (TripPlanId,OwnerUserId,CreatedAtUtc) unique=False filter=[DeletedAtUtc] IS NULL
TripSafety_TripMemories INDEX IX_TripSafety_TripMemories_TripStopId_TripPlanId (TripStopId,TripPlanId) unique=False filter=
TripSafety_TripMemories KEY PK_TripSafety_TripMemories (MemoryId)
TripSafety_TripMemories TABLE
TripSafety_TripMemoryMedia CHECK CK_TripSafety_TripMemoryMedia_Size: [ContentLength] > 0
TripSafety_TripMemoryMedia CHECK CK_TripSafety_TripMemoryMedia_State: ([State] = 0 AND [ObjectKey] IS NULL AND [ObjectVersion] IS NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] = 1 AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] IN (2, 3) AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NOT NULL AND (([State] = 2 AND [CleanedAtUtc] IS NULL) OR ([State] = 3 AND [CleanedAtUtc] IS NOT NULL)))
TripSafety_TripMemoryMedia COLUMN CleanedAtUtc datetimeoffset
TripSafety_TripMemoryMedia COLUMN CleanupClaimId uniqueidentifier
TripSafety_TripMemoryMedia COLUMN ContentLength bigint
TripSafety_TripMemoryMedia COLUMN ContentType nvarchar(64)
TripSafety_TripMemoryMedia COLUMN CreatedAtUtc datetimeoffset
TripSafety_TripMemoryMedia COLUMN MemoryId uniqueidentifier
TripSafety_TripMemoryMedia COLUMN ObjectKey nvarchar(450)
TripSafety_TripMemoryMedia COLUMN ObjectVersion nvarchar(256)
TripSafety_TripMemoryMedia COLUMN OperationId uniqueidentifier
TripSafety_TripMemoryMedia COLUMN RowVersion rowversion
TripSafety_TripMemoryMedia COLUMN State int
TripSafety_TripMemoryMedia FK FK_TripSafety_TripMemoryMedia_TripSafety_TripMemories_MemoryId (MemoryId) -> TripSafety_TripMemories(MemoryId) NoAction
TripSafety_TripMemoryMedia INDEX IX_TripSafety_TripMemoryMedia_MemoryId (MemoryId) unique=True filter=
TripSafety_TripMemoryMedia INDEX IX_TripSafety_TripMemoryMedia_ObjectKey (ObjectKey) unique=True filter=[ObjectKey] IS NOT NULL
TripSafety_TripMemoryMedia INDEX IX_TripSafety_TripMemoryMedia_State_CreatedAtUtc (State,CreatedAtUtc) unique=False filter=
TripSafety_TripMemoryMedia KEY PK_TripSafety_TripMemoryMedia (OperationId)
TripSafety_TripMemoryMedia TABLE
TripStops KEY AK_TripStops_Id_TripPlanId (Id,TripPlanId)
```

## Approval and execution request

Kaviyan and DB Coordinator: approve or reject each listed test, exact local instance and catalogs, creation and deletion, the explicit ALTER TABLE and companion deletion, test-keyed cleanup, bootstrap order and row-count inspection. Confirm no shared data and no competing runner on fixed catalogs. Review failure recovery separately. Migration generation and deployment require their own approval.

After approval, run one profile at a time with all other SQL opt-ins unset. Approve and verify the Revocation schema before a run. Each REV class invokes the same helper; the first caller can bootstrap, and no test discovery order is required. Run the inventory classes with `dotnet test tests/Wayhunter.IntegrationTests --no-build --filter 'FullyQualifiedName~<class>'`. EnsureCreated does not update an existing schema. Capture independent counts before seeding, after representative assertions and after cleanup for Users, Trips, Invitations, Members, Sessions, GPS consents and both outboxes/check-ins as applicable. For GUID-suffixed catalogs, capture counts before drop and verify catalog absence. Record catalog identity and counts without connection strings or row contents.

| Result field | Actual |
|---|---|
| Build and non-SQL checks | 1 read-only metadata case and 7 connection-string guard cases passed |
| SQL pass/fail/skip | 0 executed; see full regression for skips |
| Database identity | Pending authorization |
| Pre-test/post-test row counts | Pending authorized execution |
| Cleanup evidence | Pending authorized execution |
| Migration state | Baseline and snapshot unchanged |
| Blocker | Explicit disposable SQL operation approval |
