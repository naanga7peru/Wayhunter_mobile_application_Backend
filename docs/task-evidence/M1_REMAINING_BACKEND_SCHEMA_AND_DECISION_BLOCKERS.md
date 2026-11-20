# M1 remaining backend schema and decision blockers

Status: implementation planning evidence; no shared migration generated or applied.
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
| MS05 profile | Normalized username, visibility, public location policy, and media asset ownership/reference | Unique normalized username; owner-only writes; avatar reference must belong to the authenticated owner; no private contact, wallet or GPS data in public projection |
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
