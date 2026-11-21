# M1 remaining backend schema and decision blockers

Status: implementation and schema coordination evidence; no shared migration generated or applied.
Branch: `codex/shayanthan-m1-identitysocial-full-build`

## Existing M1 implementation observed

- `Users`, `UserProfiles`, `IdentityOtpChallenges`, `IdentitySessions`, and
  `IdentityConsents` are mapped by `WayhunterSharedDbContext` and appear in
  `InitialSharedSchema`.
- `UserAccount` has a stable server-created GUID identity, normalized email,
  password hash, account status, verification timestamp, and deletion request
  timestamp. The model has a unique index on normalized email.
- `UserProfile` stores avatar URL and bio only. It has no username, visibility,
  public location, or protected media ownership reference.
- OTP challenges store a verifier hash, expiry, attempt count, consumed time,
  and revoked time. They do not store delivery status or a resend timestamp
  separate from challenge creation.
- Session storage and the logout endpoint are SQL backed. JWT issuance and
  persisted session validation services exist.
- `UserRoleAssignments` EF mapping and a trusted SQL reader are present from
  merged PR #247, but production role activation is explicitly disabled.
- PR #257 records shared model drift and pending DB Coordinator review. It
  directs the team to preserve `InitialSharedSchema` and its snapshot.

## New schema requirements for DB Coordinator

Keep these as additive coordinated schema work. Do not edit or regenerate the
existing migration, snapshot, or migration history in this M1 branch.

| M1 work | Required storage | Required constraints / access |
|---|---|---|
| MS01–04 registration and recovery | OTP delivery metadata and a durable `VerifiedAccountActivatedEvent` outbox record; single-use password-reset challenge | Unique event identity; atomic activation plus event write; hashed OTP/reset token only; expiry, attempt count and consumed/revoked state; resend cooldown persisted atomically |
| MS04 lockout and refresh | Per-account failed-login/lockout state and session renewal token state if refresh is approved | Atomic attempt increments and lockout boundary; refresh token verifier only; one-time rotation and revocation |
| MS05 profile / registration uniqueness | `UserProfiles.NormalizedUsername` (nvarchar) plus profile visibility, public location policy, and media asset ownership/reference | Add unique index `UX_UserProfiles_NormalizedUsername`; owner-only writes; avatar reference must belong to the authenticated owner; no private contact, wallet or GPS data in public projection |
| MS06 follow and block | Directed follow relationship with pending/accepted/rejected/cancelled states; directed block relationship; notification outbox | Unique actor/target active relation; no self-follow or self-block; owner-only request decisions; block immediately invalidates private visibility and invite access; event idempotency key |
| MS07 contact discovery | Reversible discoverability consent and bounded match-request audit/limit state | Explicit opt-in/revoke timestamps; no raw address-book persistence; only minimum-data matching; rate limits and retention must follow approved D07 policy |
| MS08 settings/deletion | Language, privacy, and notification preference values; deletion confirmation/request state and revocation audit | Allowed language and preference values; deletion confirmation must preserve audit and Coins history; physical deletion is blocked pending D12 retention/Coins policy |
| MS09 Admin | Admin action audit records; warning state; protected configuration key/version history and decision reason | Actor/time/target/before-after audit; server-side permission check; no client-set roles; production Admin activation remains disabled; setting values/reasons and approval roles require owner/coordinator contract |

## Decisions and external dependencies still required

- **DECISION_REQUIRED — contact discovery D07:** the M1 task brief requires an
  approved matching method but does not define one in the available backend
  evidence. Do not choose raw-contact upload, hashing, or a provider implicitly.
- **DECISION_REQUIRED — deletion D12:** retention and Coins-ledger handling are
  not approved. Expose a confirmed request/cancel workflow only after the
  coordinated schema and retention behavior are accepted; do not physically
  delete accounts or history.
- **DECISION_REQUIRED — session refresh and lockout thresholds:** existing
  policy helpers contain provisional limits, while the available approved
  evidence does not authorize a durable lockout or refresh-token policy.
- **DEPENDENCY — avatar ownership:** no media authorization/provider contract
  exists in this repository. Do not accept arbitrary external media URLs as
  proof of upload ownership.
- **DEPENDENCY — notifications:** no M1 follow notification transport/outbox
  contract is present. Persisting relationship transitions can be separated
  from delivery once event ownership and retry semantics are approved.
- **DEPENDENCY — Admin operations:** the reviewed role mapping is not in the
  initial physical schema; DB Coordinator approval, migration coordination,
  and separate production activation approval remain required.
- **DEPENDENCY — profile summaries:** source repositories for all games,
  posts, trips, hunts, and Coins summaries are not all exposed through approved
  M1 read contracts. Do not return fabricated counts.

## Code and verification boundary

`GET /api/mobile/identity/me` uses the existing shared DbContext and trusted
authenticated identity to read the owner profile. It returns the existing
private profile DTO only for an active, verified account whose ID matches both
the authenticated principal and database row. It requires the existing
SQL-backed session authentication to be enabled and configured.

The remaining schema-dependent work must be implemented against an approved
coordinated model, tested against a dedicated disposable SQL database, and
then added through the DB Coordinator's migration process. This document does
not assert that unavailable flows or routes are implemented or verified.

## MS03 / MS04 schema blockers identified during implementation

The current M1 registration and OTP endpoints use the existing `Users`,
`UserProfiles`, and `IdentityOtpChallenges` tables. The existing challenge's
`CreatedAtUtc`, `ExpiresAtUtc`, `FailedAttemptCount`, `MaxFailedAttempts`,
`ConsumedAtUtc`, and `RevokedAtUtc` fields support the implemented resend,
expiry, attempt-limit, and single-use rules. No new OTP schema is required.

Persistent login lockout is **not** represented in `Users` or another M1
table. Required coordinated schema: `IdentityLoginSecurity`, with
`UserId` (PK/FK to `Users.UserId`), `FailedAttemptCount` (int),
`LockedUntilUtc` (nullable datetimeoffset), and a concurrency token (`RowVersion`
or an approved equivalent). The `UserId` relationship and conditional update
must support atomic lockout transitions. Until coordinated schema is applied,
the HTTP sign-in flow validates credentials and account state but does not
claim persistent lockout protection.

The M6 handler can be invoked after the activation transaction commits, and
the M6 ledger source key makes the reward grant idempotent. Durable activation
delivery is **not** represented by the current schema. Required coordinated
schema: `IdentityAccountActivationOutbox`, with `EventId` (uniqueidentifier
PK), `UserId` (FK), `VerifiedAtUtc` (datetimeoffset), `CorrelationId`
(uniqueidentifier), `Status`, attempt/retry timestamps, lease fields, and a
safe error code. The row must be inserted in the same transaction as setting
`Users.Status` active and consuming the OTP. Do not repurpose `CoinRewardOutbox`:
that M6 table represents a confirmed Coins reward event, which only exists
after the welcome grant and cannot close the activation-to-delivery crash gap.

The implementation attempts the existing `HandleVerifiedAccountActivatedReward`
after commit when M6's SQL reward writer has been registered. Delivery failure
is logged without secrets or personal fields; the activation remains committed.
No background retry can guarantee delivery until the outbox table is added by
the DB Coordinator. `InitialSharedSchema`, its snapshot and migration history
remain unchanged.

## HTTP routes implemented in this branch

- `POST /api/mobile/identity/register`
- `POST /api/mobile/identity/resend-otp`
- `POST /api/mobile/identity/verify-otp`
- `POST /api/mobile/identity/login`
- Existing `GET /api/mobile/identity/me`
- Existing `POST /api/mobile/auth/logout`

The route-level flow is exercised with EF InMemory and a test-only email sender.
It does not prove SQL transaction isolation, deployed indexes, Resend delivery,
or M6 SQL ledger behavior. OTP settings follow existing M1 policy evidence:
5-minute expiry, 5 attempts, and 60-second resend cooldown. Sign-in requires
`Identity:SessionLifetimeMinutes` (1–1440); supply this through runtime
configuration, not a checked-in secret.

Registration currently enforces normalized email uniqueness using `Users` and
the existing unique email index. Username uniqueness cannot be enforced from
the current `IdentityRegisterRequest`, because `UserProfiles` has no username
column or unique index. This is a schema blocker, not an in-memory-only check.

## M1-MS04-T04 password recovery schema requirement

The recovery implementation maps an additive `IdentityPasswordResetCredentials`
table. The DB Coordinator must create it before enabling these routes against
SQL Server. Required columns: `CredentialId` (`uniqueidentifier`, primary key),
`UserId` (`nvarchar(256)`, foreign key to `Users.UserId`), `TokenHash`
(`char(64)`, unique SHA-256 hex digest), `CreatedAtUtc`, `ExpiresAtUtc`, nullable
`ConsumedAtUtc`, and nullable `RevokedAtUtc` (all timestamps
`datetimeoffset`). Add `CK_IdentityPasswordResetCredentials_Expiry` requiring
expiry after creation, unique index
`UX_IdentityPasswordResetCredentials_TokenHash`, and index
`IX_IdentityPasswordResetCredentials_User_Expiry`. Use no-action deletion for
the user foreign key. Only the digest is persisted; reset credentials are
single-use and are invalid after expiry/revocation.

`InitialSharedSchema`, the model snapshot, and migration history remain
unchanged. No shared migration was generated or applied by this task. Configure
`Identity:PasswordResetLifetimeMinutes` (1 to 1440) and the existing Resend email
settings through runtime configuration; do not commit secrets.

## M1-MS04-T04 HTTP routes

- `POST /api/mobile/identity/forgot-password` returns the same generic `202`
  response for eligible and unknown accounts.
- `POST /api/mobile/identity/reset-password` accepts email, reset token, new
  password, and confirmation; succeeds with `204` and rejects invalid, expired,
  consumed, or mismatched credentials with `400`.

The route tests use the test email sender and EF InMemory. They do not prove the
SQL table has been deployed or validate SQL Server transaction behavior.

## SQL Server execution strategy verification

OTP verify/resend and password recovery now run each serializable database
transaction inside EF Core's configured execution strategy. OTP email, reset
email, and the post-activation reward handler run only after the transaction
delegate succeeds. SQL verification for OTP requires the approved shared
`InitialSharedSchema` in a dedicated disposable M1 SQL Server database.
Forgot/reset verification additionally requires the DB Coordinator's additive
`IdentityPasswordResetCredentials` migration. Neither a migration nor a shared
database update was created or applied by this work.
