# Final backend closure and demo handoff

Updated 2026-11-24 from this worktree. Branch: `task/m5-production-game-writes`.

## API inventory

[`wayhunter-swagger-endpoint-inventory.json`](wayhunter-swagger-endpoint-inventory.json) contains 120 operations grouped by module, with route, method, available request schema and response status metadata from Swagger. The default Development host exposes 112; the remaining eight authenticated M5 draft/review routes are feature gated. Swagger does not emit complete JSON examples or security schemes for every operation, so authentication annotations in the inventory are controller-level summaries; verify DTO schemas in `components.schemas` and controller attributes before generating a client.

## Verification

- Release solution build: passed.
- Unit tests after seed runner and Home work: 3,492 passed, 0 failed, 0 skipped.
- Previously verified unique M1–M7 integration baseline: 287 passed, 0 final failed, 58 skipped. The skips were M4 7, M5 48, M6 3; SQL opt-in tests comprised most of these. Individual opt-in tests are listed in their test classes.
- This closure run: M2 SQL/API 2 passed (includes Swagger), M5 authenticated SQL draft/review 1 passed, M6 voucher authenticated HTTP-to-SQL 1 passed, M6 SQL wallet reward concurrency/idempotency 3 passed, M7 advertisement HTTP-to-SQL 1 passed. All targeted only `Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb`; no migrations were run.
- New M3 SQL closure tests: Hazard SQL active/expiry and concurrent review; Trip Memory SQL ownership and tombstone. Both passed on the same authorized catalog. Cleanup removes only test-created hazard, memory, trip and account rows.
- M7 SQL HTTP E2E now also verified authenticated Home dashboard response, active advertisement inclusion, and anonymous rejection.
- EF pending model check: no changes since last migration.
- Approved disposable catalog: previously verified ONLINE with eight applied migrations through `20261123175746_M5GameDraftCreateIdempotency`.
- Swagger runtime before Home addition: 111 default operations; current default surface is 112. SQL-mode route test verifies 120 including `GET /api/mobile/home` and the eight gated M5 operations.
- `git diff --check`: run at handoff.

Do not add these repeated test runs to the earlier unique totals. The earlier 287 + 58 integration result is the regression baseline; closure runs above are additional targeted confirmations.

## Verified workflows and remaining gates

- M1 registration, OTP, password reset, login, logout, profile and session authentication are present. Refresh-token rotation/replay protection and durable account lockout are not implemented: `docs/task-evidence/M1_REMAINING_BACKEND_SCHEMA_AND_DECISION_BLOCKERS.md` marks refresh and lockout policy as `DECISION_REQUIRED`. Do not claim refresh endpoint availability.
- M2 TripPlan SQL HTTP acceptance and Swagger were verified against the authorized disposable database. Route/Places/AI/weather adapters require their server keys and fail closed when disabled.
- M3 SQL session, consent, invitation/revocation, Safe Check-in, block and notification tests passed in the prior focused SQL batch. Hazard and Trip Memory SQL persistence/ownership/tombstone tests passed in this closure run. Google Routes geometry is wired as the hazard route provider; live hazard-route matching still depends on a configured provider. Trip Memory upload/read bytes remain media-provider gated. Notification outbox persistence is not proof of external delivery.
- M4/M5 verified capture receipts and media are unavailable without an approved trusted private-media receipt issuer/verifier. Client media references do not constitute verified evidence. M5 authenticated SQL draft CRUD, idempotency, ETag/If-Match and Admin review passed the focused SQL E2E. API remains behind its explicit deployment gate.
- M6 wallet/reward idempotency and voucher request, approval, rejection, atomic debit, issue and owner-only secret access passed focused SQL tests. Private voucher image bytes/provider delivery were not verified; current response protects opaque image reference only.
- M7 advertisement SQL CRUD and active placement/date filtering passed SQL E2E. The authenticated `GET /api/mobile/home` dashboard passed SQL HTTP verification and returns profile summary, wallet totals, upcoming trips, published games, approved Hunter places, and active banner/sponsored-POI advertisements. No analytics ingestion was added because its event schema, retention, and privacy contract are not approved; no data collection was invented.

## Provider configuration

Use [`../../config/wayhunter.server.example.json`](../../config/wayhunter.server.example.json) as a placeholder-only template. Supply secrets via deployment secret storage/environment variables, never commit them. Exact keys:

- `Authentication:Jwt:Issuer`, `Authentication:Jwt:Audience`, `Authentication:Jwt:SigningSecret`
- `Identity:Email:ResendApiKey`, `Identity:Email:SenderAddress`
- `TravelPlanning:Maps:EnableGeocoding`, `GeocodingServerApiKey`, `EnablePlaces`, `PlacesServerApiKey`, `EnableRoutes`, `RoutesServerApiKey`
- `TravelPlanning:AiTrip:Enabled`, `ApiKey`, `Model`, `TimeoutSeconds`, `MaxRequestsPerUserPerDay`
- `TravelPlanning:Weather:Enabled`, `ApiKey`, `CacheMaxAgeMinutes`, `TimeoutSeconds`
- `Advertisements:Cloudinary:Enabled`, `CloudName`, `ApiKey`, `ApiSecret`
- `TripSafety:EnableSqlPersistence`, `EnableSqlTripMemories`, `EnableNotificationOutboxWorker`, `TripSafety:NotificationOutbox:WebhookUrl`, `WebhookApiKey`

No real provider credentials were available or tested in this run. OTP/email, Cloudinary image transport, weather, Gemini, Google Places/Routes and notification receiver delivery remain provider-dependent. Push token storage/delivery and M4/M5 trusted receipt issuance have no verified production provider contract.

## Demo seed readiness

An explicit console runner now seeds deterministic M1 account/profile records, M2 trips/stops, M3 live session/check-in/outbox/hazard/text memory, M4 draft submissions, M5 draft games/stages/hints, M6 approved welcome rewards and protected academic voucher offer/inventory, and M7 banner/sponsored-POI records. It uses the password hash format accepted by ASP.NET Identity, trusted Admin role assignment, existing reward/voucher/Game/Trip Memory/Safe Check-in domain services and deterministic IDs. It does not run on API startup. It does not generate OTP receipts, trusted media, or provider delivery outcomes. It only accepts explicitly confirmed LocalDB `Wayhunter_Demo_*` targets with schema preflight and never permits Official/production. The runner has not been executed or SQL-idempotency-tested; do not execute it until a target demo database and run are separately authorized. Its exact command and environment are in [`DEMO_SEED_RUNNER.md`](DEMO_SEED_RUNNER.md). Seed transactions are per operation/context rather than one all-modules transaction, so an interrupted run is resumed by rerunning; existing records are checked and conflicting identities fail closed. Welcome-only balances are 100 Coins; the runner deliberately does not mint the additional amount needed for voucher redemption.

The authenticated Home dashboard is `GET /api/mobile/home`. It composes owner profile summary, wallet balances, upcoming trips, published games, approved Hunter places and active advertisement placements. It collects no analytics events and includes no contact, GPS or media data. The checked-in Swagger inventory contains 120 SQL-mode operations; it describes gated surfaces as well as enabled routes, so deployment feature gates still apply.

## Flutter connection

- API base URL is the origin only; routes already include `/api`.
- Android emulator: `http://10.0.2.2:<port>`.
- Physical Android: `https://<reachable-hostname-or-LAN-IP>:<port>` with a certificate trusted by the device; API must bind to a reachable interface and firewall must allow the port.
- Flutter web: use the deployed HTTPS origin and set `Cors:AllowedOrigins` to the exact web origin. Do not use wildcard CORS with credentials.
- Send `Authorization: Bearer <access-token>` on protected routes. Use `Idempotency-Key` for Game draft creation; send the returned `ETag` as `If-Match` for draft updates. Never store provider secrets in Flutter.
- Configure issuer/audience consistently between token issuer and API. Review response DTOs and the endpoint inventory before generating mobile models.

## Database and startup procedure

The prior authorization is only for the disposable LocalDB catalog named above. Do not substitute or target Official. Before any migration, query the exact catalog with standard `sqlcmd` and confirm it is ONLINE. Migration commands below are a future procedure and are **not run by this handoff**:

```powershell
$env:ConnectionStrings__WayhunterSharedDb = 'Server=(localdb)\MSSQLLocalDB;Database=Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb;Integrated Security=true;TrustServerCertificate=true'
dotnet ef database update --project src/Wayhunter.Infrastructure --startup-project src/Wayhunter.Api --configuration Release
```

Take and verify a backup before upgrading a final demo database. Review generated SQL/migration list and exact catalog first. The current disposable catalog already has eight migrations applied and no pending model changes.

Backend startup after secure configuration:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Wayhunter.Api --urls http://0.0.0.0:5055
```

Enable SQL persistence only after applying reviewed migrations to the explicitly selected demo database. Do not use these commands with production/Official.

## Git review

No commit, push or merge was made. See [`FINAL_CHANGED_FILES.md`](FINAL_CHANGED_FILES.md) for the complete modified/untracked inventory. Migration inventory includes the existing change `20261123175746_M5GameDraftCreateIdempotency`; it has already been applied to the previously authorized disposable catalog. Recommended commit title after human review: `Complete M5 SQL game draft review workflow`.

Suggested PR title: `Complete authenticated M5 game draft SQL workflow`.

PR summary: durable draft create idempotency, ownership-safe SQL CRUD, ETag/If-Match updates, Admin approval/rejection and authenticated SQL E2E. Include M3 profile/fixture safety changes, M6 voucher offer detail and demo seed factory, plus current verification. Risks: explicit M5 API gate, unverified external media/email/notification providers, unresolved M1 refresh/lockout policy, and M7 dashboard/analytics decisions.

## Remaining blockers before demo/production

1. Select and authorize a dedicated `Wayhunter_Demo_*` database, then execute and verify the guarded seed runner; prepare demo passwords/voucher codes through environment variables.
2. Obtain M1 refresh/lockout policy and schema approval, then implement rotation/replay-safe persistence.
3. Decide whether analytics ingestion is required; if so, approve event schema, privacy and retention before implementation.
4. Approve and configure a trusted private media/receipt provider for M3/M4/M5 and private voucher image bytes.
5. Configure and verify Resend, Google, Gemini, weather, Cloudinary and durable notification receiver credentials/receipts.
6. Review the route inventory/DTOs with Flutter and resolve remaining 58 integration skips against safe isolated targets before production.
