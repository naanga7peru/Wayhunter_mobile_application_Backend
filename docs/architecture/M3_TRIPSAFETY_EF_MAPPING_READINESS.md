
# WAYHUNTER — M3 TripSafety EF Mapping Readiness

**Owner:** Mathi
**Approved scope:** Team Leader Kaviyan
**Task:** TripSafety EF Core Persistence Mapping Foundation
**Reviewed baseline:** develop cba9dd4
**Migration:** HOLD

## 1. Scope

This task adds EF Core configuration for:

- TripInvitation
- TripMember
- TripGpsConsent
- HazardReport

It registers all four configurations in the
existing WayhunterSharedDbContext and verifies
the EF model through metadata tests.

## 2. Identity Contracts

M1 UserAccount:

- Table: Users
- Primary key: UserId
- CLR type: string
- Maximum length: 256

M2 TripPlan:

- Table: Trips
- Primary key: Id
- CLR type: Guid
- SQL type: uniqueidentifier

M3 LiveTripSession:

- Table: TripSafety_LiveTripSessions
- Primary key: SessionId
- CLR type: Guid

## 3. Proposed Tables

| Entity | Table | Primary key |
|---|---|---|
| TripInvitation | TripSafety_TripInvitations | Id |
| TripMember | TripSafety_TripMembers | Id |
| TripGpsConsent | TripSafety_GpsConsents | SessionId + UserId |
| HazardReport | TripSafety_HazardReports | Id |

## 4. Relationships

TripInvitation:

- TripPlanId -> Trips.Id
- SenderUserId -> Users.UserId
- RecipientUserId -> Users.UserId

TripMember:

- TripPlanId -> Trips.Id
- MemberUserId -> Users.UserId
- InvitationId -> TripInvitations.Id

TripGpsConsent:

- SessionId -> LiveTripSessions.SessionId
- UserId -> Users.UserId

HazardReport:

- ReporterUserId -> Users.UserId
- VerifiedByUserId -> Users.UserId
- ResolvedByUserId -> Users.UserId

## 5. Constraints and Indexes

TripInvitation:

- Status 0..3
- Response timestamp consistency
- One open/accepted invitation for a trip recipient

TripMember:

- Active/removal timestamp consistency
- One membership per invitation
- One active member per trip/user

TripGpsConsent:

- Explicit grant timestamp
- Revocation timestamp consistency
- One record per session/user

HazardReport:

- Status 1..4
- ExpiresAtUtc equals ReportedAtUtc + 24 hours
- Verification and resolution audit timestamp consistency
- Expiry timestamp consistency

Delete behavior proposal: NoAction to preserve
historical and audit data.

Final delete behavior requires DB Coordinator review.

## 6. Verification Tests

Metadata tests inspect:

- Correct table names
- Primary keys
- Composite primary key
- M1, M2, M3 foreign keys
- Delete behaviors
- Indexes and filtered unique indexes
- Check constraints
- SQL types and property lengths
- Shared DbContext entity registration
- Generated SQL schema script

These tests do not connect to SQL Server.

## 7. Limitations

The current domain HazardReport has no coordinate,
image media reference or trip ID properties.

This mapping does not add any new properties
or relationships to the domain.

Those fields require separate product and
cross-module approval if needed.

## 8. Migration Restrictions

This task does not:

- Generate EF migrations
- Execute database updates
- Add manual SQL DDL
- Modify shared SQL Server data
- Create module-specific migrations

## 9. Final Status

EF code: To be verified

Targeted tests: To be verified

Full build/tests: To be verified

PR review: Pending

Seven-member schema approval: Pending

DB Coordinator approval: Pending

Team Leader migration authorization: Not granted

**Migration remains HOLD.**
