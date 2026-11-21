# M3-MS08 notifications readiness

Base `617d16f5a6bc4cdada7d724156edca098564935a` includes PR #267. Scope is the M3-MS08 milestone from the Second Brain `planning/member_briefs/M3_FULLSTACK_MILESTONES_TASKS.md`. This branch adds backend code and proposed metadata. Shared SQL schema, audience integration and external delivery remain disabled.

## Task coverage

| Task | Implemented and pending |
|---|---|
| T01 | Trip, Safety, Social, Hunter, Game, Coins and Voucher categories, per-category push preference and default opt-out. Inbox retention is independent of push eligibility. |
| T02 | Proposed shared EF model for stable events, recipients, devices, preferences, scheduled jobs and recipients, preview receipts, operation audit and delivery audit. Schema approval and SQL tests are pending. |
| T03 | Unregistered SQL claim/acknowledgement store and worker. Conditional claims increment attempts, expire for crash recovery, and fence results by token, attempt and expiry. Terminal and retry writes also persist attempt audit. Reuses M3 settings and exponential retry policy. |
| T04 | Owner-scoped register/rotate/revoke routes and SQL implementation draft. Token binding prevents a fingerprint being rebound to a different user or installation after rotation. Provider-confirmed invalidation can only revoke the exact owner/device rowversion. Production attestation, token protection and FCM adapter unavailable. |
| T05 | Owner/session-scoped reminder schedule/reschedule/cancel, SQL publication gated by trip rowversion, date, open session and scheduled revision, and push-time revalidation. It creates no Start action. Due-job polling and provider race policy remain pending. |
| T06 | Version 1 fixed titles and generic bodies for all seven categories. Push receives a category/template descriptor and stable delivery identity. Typed targets are inbox-only. |
| T09 | Verified Admin principal plus a dedicated composer scope check, authoritative server preview, bounded explicit scope and defense-in-depth checks on returned role, district, and explicit recipient IDs. Actual recipient directory integration remains pending. |
| T11 | SQL draft persists operation identity, one-use bound preview, schedule and recipient snapshot; cancels only a matching scheduled revision. Both immediate and scheduled commands queue rather than reporting delivery. Explicit due publication revalidates role/district scope and active recipients, then atomically writes one event and its inbox recipients. Due-job polling is still unregistered and needs operational design. |
| T13 | Fixed templates only. Reject all free text and arbitrary deep links, even for Admin. No promotional template or audience bypass. A typed category and GUID reference must be authorized by the authoritative directory. Command and delivery audit use controlled fields. |
| T07, T10, T12, T14 | Backend route/DTO contracts for inbox, preferences, read state, typed target, composer preview and confirmation, cancellation and aggregate delivery summary. Flutter and Angular work remain out of scope. |
| T08, T15 | Non-SQL unit, HTTP and metadata tests. Isolated SQL and real provider verification pending. |

## Existing contracts

The existing membership-revocation dispatcher, worker and processor, SafeCheckIn dispatcher, and Coins/Voucher outbox remain intact and retain their event types. MS08 reuses `M3OutboxWorkerSettings`, `M3OutboxRetryPolicy`, the exact-session `ITripFinishedReminderGuard` and M1 trusted-principal/Admin checks. It does not reinterpret a revocation, safe check-in, reward, or issuance record as an MS08 event. Business-event ingestion and target permission adapters require coordination with their owners before activation.

## API boundary

All routes require authenticated M1 identity. The actor comes from trusted claims. Admin routes require the existing trusted Admin role contract plus the directory's separate scope decision. Responses are no-store. A missing approved dependency returns 503 when reached; invalid data 400, identity failure 401, scope denial 403/404, and state/confirmation conflicts 409.

| Route | Contract |
|---|---|
| GET `/api/mobile/notifications?limit=30` | Bounded current-user inbox, unread count and typed targets. Target pages must independently authorize access and check current target availability. |
| PUT `/api/mobile/notifications/{eventId}/read` | Idempotently set the current user's first-read timestamp. |
| GET/PUT `/api/mobile/notifications/preferences` | List choices or update one category and push flag. |
| PUT `/api/mobile/notifications/devices/{deviceId}` and POST `/devices/{deviceId}/revoke` | Operation ID, token and opt-in at registration. Revocation uses operation ID only. Responses omit the token and fingerprint. |
| PUT `/api/mobile/notifications/reminders` and POST `/reminders/{jobId}/cancel` | Operation ID, trip/session, planned date and requested UTC due time; cancel by revision and operation ID. Scheduling the same scope with a new operation supersedes the stored revision while unpublished. |
| POST `/api/admin/notifications/preview` | Template, typed optional target, approved filters, operation ID and optional future UTC time. Returns server count and short-lived binding receipt. |
| POST `/api/admin/notifications/schedules` | Same command, explicit bulk acknowledgement and preview receipt when more than one recipient. Server recomputes scope. An identical authorized retry reuses its persisted operation result. |
| POST `/api/admin/notifications/schedules/{jobId}/cancel` and GET `/schedules/{jobId}/summary` | Actor-owned scheduled revision cancellation and aggregate queued/delivered/suppressed/failed/read counts. |

## Proposed shared SQL metadata

All physical declarations require DB Coordinator approval. Existing M1 user references use required `nvarchar(256)` and NO ACTION. Existing session alternate key enforces job trip/session binding. No migration or snapshot update accompanies the model.

| Table, prefix `TripSafety_Notification` | Scope, keys and indexes |
|---|---|
| Events | GUID EventId primary key, template/category/version, UTC create, optional typed target and job. Filtered unique JobId gives one logical publication per schedule. |
| Recipients | Composite (EventId, UserId) PK/FKs. Private ReadAtUtc, delivery state, attempt count, due/lease/complete times, token and safe outcome. User/read index and pending retry/lease index. State and paired lease constraints. |
| Preferences | (UserId, Category) PK, push choice and category check. |
| Devices | GUID DeviceId PK, owner FK, protected ciphertext, keyed fingerprint unique, opt-in/revocation, rowversion and owner/revocation index. |
| TokenBindings | Keyed fingerprint PK, owner and original device FKs, UTC binding time. Keep historical binding across token rotation; retention and account/device recovery require a decision. |
| Jobs | GUID JobId PK, actor and optional trip/session FKs, kind/state/revision, template/target, UTC due, optional date/rowversion snapshot and audience fingerprint/role/district. Filtered unique trip/session/actor for reminders and state/due index. SQL rowversion and state/kind checks. |
| JobRecipients | (JobId, UserId) PK/FKs. Frozen audience of each Admin job. |
| AudienceReceipts | GUID receipt PK, actor FK, scoped request/audience digest, count (1?100), expiry and consumption time. Expiry index. |
| Operations | (ActorId, OperationId) PK, content fingerprint, resource/action/time and job result for idempotency. Resource/time audit index. |
| DeliveryAudit | GUID audit PK, recipient composite FK, attempts, safe outcome and UTC timestamp. |

## Delivery and scheduling guarantees to verify

The store claims a due recipient with a conditional state/attempt update and persists claim audit before returning a lease. It records acknowledgement or retry only under the current unexpired lease. Crashed leases expire. Expired final attempts are dead-lettered, and terminal delivery outcomes leave inbox history available. Retry uses bounded backoff. The dispatch gate checks current account, category choice, an active opted-in device, and target authority. The provider contract must resolve active opted-in devices and conditionally revoke invalid versions. It must use the same event/recipient/device identity on retries. A provider acceptance followed by a lost SQL acknowledgement can cause another attempt. FCM support for durable recipient deduplication is NOT VERIFIED.

Schedule mutation and publication are fenced by serializable transactions and revision/state checks. Reminder publication rechecks exact trip/session status, owner, recorded trip date and rowversion. A finished, cancelled or stale reminder is suppressed. A cancel committed first prevents publication; after a publication commits, cancellation returns conflict. The provider call is external to SQL. Finish or an authorization change can still race between the final eligibility read and an in-flight send. Do not enable production dispatch before a coordinator/owner-approved boundary for in-flight sends. Production scheduling also needs due-job polling, stale-job disposition and replay/runbook policies.

Admin publication never expands the frozen audience. A changed current audience blocks publication. An approved adapter must establish authoritative role, account, district, topic preferences, recipient target eligibility and sender scope. The shared M1 model has no approved district authority for this use. Test fakes supply these fields only to verify guard behavior. Pending SQL approval includes validating the separation and transaction boundary between recipient verification and the job commit.

## DECISION_REQUIRED before activation

- DB Coordinator approves the tables, indexes, migrated baseline, isolation, clock/lease behavior, and an isolated test profile. Confirm existing unrelated snapshot drift before generating any migration. Verify concurrent duplicate writes, cancel versus publish, both Finish race orders, revoked device CAS, lease expiry and stale acks using multiple SQL connections.
- M1 and Admin owners approve district authority, role memberships, recipient scope and sender grant. Product owners approve any further templates, localization, targets, preference semantics, limits, cancellation policy, UTC schedule/time-zone interpretation, and data retention. No promoter or provider is registered.
- Approve FCM provider and credentials outside source, install/token ownership proof, key rotation and protected token storage, invalid-token retire policy, audit privacy rules, and a durable deduplication contract. No raw token appears in a response, SQL row, or delivery audit. Disable request body logging for device routes before any real provider activation.
- Define end-to-end business event adapters and approval for FCM/worker activation. Keep SafeCheckIn and MS07 wiring unchanged. No Flutter or Angular UI in this branch.

## Verification evidence

Restore, build, full and focused test runs passed. Build reported 0 errors and two existing M2 xUnit2031 warnings. Full tests passed 3,228 unit cases and 171 integration cases with 0 failures and 85 opt-in SQL skips. The focused MS08 rerun passed 16 unit/metadata and 9 HTTP cases, with no failures or skips. All repository SQL connection opt-ins were cleared in the test process. No SQL DDL ran. SQL uniqueness, transaction concurrency, and lease behavior under real SQL remain unverified.
