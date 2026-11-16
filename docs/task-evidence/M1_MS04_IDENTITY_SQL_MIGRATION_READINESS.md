# M1-MS04 — Identity SQL Migration Readiness

## Status

- Preparation: COMPLETED - verification PASSED
- Consolidated SQL migration: HOLD
- DB Coordinator ownership decision: Pending
- Draft PR #141: Not updated by this preparation
- Scope: M1 IdentitySocial persistence and EF schema verification

## Approved Identity Tables

| Table | Primary Key | Foreign Key |
|---|---|---|
| Users | UserId | None |
| UserProfiles | UserId | UserId -> Users.UserId |
| IdentitySessions | SessionId | UserId -> Users.UserId |
| IdentityOtpChallenges | ChallengeId | UserId -> Users.UserId |
| IdentityConsents | ConsentId | UserId -> Users.UserId |

All four user-owned relationships use DeleteBehavior.NoAction.

## Current EF Configuration

IdentityDbContext registers exactly once:

1. UserAccountConfiguration
2. UserProfileConfiguration
3. IdentitySessionConfiguration
4. IdentityOtpChallengeConfiguration
5. IdentityConsentConfiguration

WayhunterSharedDbContext in the locally available origin/develop
also registers the same five configuration classes once each.

Both contexts use the same configuration definitions.
Final shared migration ownership remains a DB Coordinator decision.

## Schema Details

### Users

- UserId: string, max length 256, required, primary key
- DisplayName: string, max length 200, required
- NormalizedEmail: string, max length 320, required
- PasswordHash: string, max length 1024, required
- Status: integer conversion, required
- CreatedAtUtc: datetimeoffset, required
- VerifiedAtUtc: datetimeoffset, nullable
- DeletionRequestedAtUtc: datetimeoffset, nullable
- Unique index: UX_Users_NormalizedEmail

### UserProfiles

- UserId: string, max length 256, required, primary key and foreign key
- AvatarUrl: string, max length 2048, nullable
- Bio: string, max length 500, nullable
- CreatedAtUtc: datetimeoffset, required
- UpdatedAtUtc: datetimeoffset, nullable
- FK delete behavior: NoAction

### IdentitySessions

- SessionId: string, max length 36, required, primary key
- UserId: string, max length 256, required, foreign key
- CreatedAtUtc: datetimeoffset, required
- ExpiresAtUtc: datetimeoffset, required
- RevokedAtUtc: datetimeoffset, nullable
- Non-unique indexes: UserId, ExpiresAtUtc
- FK delete behavior: NoAction

### IdentityOtpChallenges

- ChallengeId: string, max length 36, required, primary key
- UserId: string, max length 256, required, foreign key
- OtpVerifierHash: string, max length 1024, required
- CreatedAtUtc: datetimeoffset, required
- ExpiresAtUtc: datetimeoffset, required
- FailedAttemptCount: required
- MaxFailedAttempts: required
- ConsumedAtUtc: datetimeoffset, nullable
- RevokedAtUtc: datetimeoffset, nullable
- Non-unique indexes: UserId, ExpiresAtUtc
- FK delete behavior: NoAction

### IdentityConsents

- ConsentId: string, max length 36, required, primary key
- UserId: string, max length 256, required, foreign key
- ConsentKey: string, max length 200, required
- GrantedAtUtc: datetimeoffset, required
- RevokedAtUtc: datetimeoffset, nullable
- Non-unique composite index: UserId, ConsentKey
- FK delete behavior: NoAction

## SQL Configuration and Dependency Injection

Current IdentityPersistenceServiceCollectionExtensions:

- Reads configuration.GetConnectionString("WayhunterIdentity")
- Rejects missing or empty connection strings
- Registers IdentityDbContext with SQL Server
- Registers SqlAuthenticatedSessionReader
- Registers SqlIdentitySessionRevocationStore
- Registers SessionLogoutService

The current WayhunterIdentity key is not approved as the
canonical consolidated database connection-string key.

Do not modify shared appsettings, connection-string aliases,
or shared DbContext DI registration until DB Coordinator approval.

## Cross-Module Dependencies

- M1 authenticated identity is required by other user-owned modules.
- M4 Hunter identity mapping depends on M1 UserAccount.UserId.
- String-to-Guid mapping and trusted server-side identity resolution
  must follow the approved cross-module identity contract.
- Shared FK ownership and migration ordering require coordination.

## Verification

EF metadata tests:
- Existing IdentityEfModelTests: 19 passed
- Additional IdentityEfSchemaCoverageTests: 35 passed
- Total focused EF tests: 54 passed, 0 failed

Full solution verification (2026-11-16):

- dotnet restore Wayhunter.slnx: PASSED
- dotnet build Wayhunter.slnx --no-restore: PASSED
- dotnet test Wayhunter.slnx --no-restore: PASSED
- Full regression: 2,127 passed, 0 failed, 0 skipped
- Focused Identity EF metadata tests: 54 passed, 0 failed

Note: Integration test logs include AI/Maps provider
configuration validation errors. The complete test suite
still passed; these logs have not been independently
classified as expected negative-test output.

## Outstanding Decisions

1. Authoritative consolidated migration DbContext.
2. Canonical shared SQL Server connection-string key.
3. Final shared DI registration and migration configuration.
4. Cross-module foreign-key ownership and migration ordering.
5. Final DB Coordinator schema review and migration authorization.

No migration generation, application or production SQL
integration is authorized by this document.
