# Final backend closure and demo handoff

Updated 2026-11-24 after PR #314 merge. Base: `develop` at `1db9999`; final runtime hardening branch: `task/final-demo-runtime-hardening`.

## Final demo runtime verification

Verified against `Wayhunter_Demo_Final` on `(localdb)\MSSQLLocalDB`; no database was created, migrated, or reseeded during this verification. The API process remained running at `http://127.0.0.1:5055`.

- `POST /api/mobile/identity/login`: HTTP 200; authenticated JWT issued.
- `GET /api/mobile/home` with the bearer token: HTTP 200.
- Anonymous `GET /api/mobile/home`: HTTP 401.
- Home response: profile present; wallet spendable balance 100; 1 upcoming trip; 0 published games; 0 approved Hunter places; 1 active banner; 1 sponsored POI.
- Swagger: HTTP 200, 120 operations.
- Health: HTTP 200 and remained available for at least 60 seconds.
- The PowerShell 5.1 verifier sends JSON only for non-empty POST/PUT/PATCH requests; GET/HEAD send no request body. It uses `$homeResponse` to avoid collision with PowerShell's automatic `$HOME` variable.
- The seed runner was previously executed twice against this catalog in explicit `--skip-voucher` mode. Verified seeded data: 10 accounts/profiles, 5 trips/10 stops, one Trip Safety session, check-in, notification outbox item, hazard and text-only memory, 2 Hunter drafts, 2 Game drafts with stages/hints, 10 wallets and welcome rewards, no voucher offer/inventory (intentionally skipped), and 2 active advertisements (1 banner, 1 sponsored POI).

The current API listens on loopback only. This is suitable for the Android emulator via host-loopback forwarding; a physical device requires a reachable bind address and firewall rule as described below. Do not restart the verified process solely for physical-device configuration.

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

An explicit console runner seeds deterministic M1 account/profile records, M2 trips/stops, M3 live session/check-in/outbox/hazard/text memory, M4 draft submissions, M5 draft games/stages/hints, M6 approved welcome rewards and (when configured) protected academic voucher offer/inventory, and M7 banner/sponsored-POI records. It uses the password hash format accepted by ASP.NET Identity, trusted Admin role assignment, existing reward/voucher/Game/Trip Memory/Safe Check-in domain services and deterministic IDs. It does not run on API startup and does not generate OTP receipts, trusted media, or provider delivery outcomes. It accepts explicitly confirmed LocalDB `Wayhunter_Demo_*` targets with schema preflight and never permits Official/production. It was executed twice against `Wayhunter_Demo_Final` in explicit `--skip-voucher` mode; both passes succeeded and duplicate prevention was verified. Voucher inventory was skipped because no trusted private image reference/provider was available. Seed writes are per operation/context rather than one all-modules transaction; rerun resumes existing records and conflicting identities fail closed. Welcome-only balances are 100 Coins; the runner deliberately does not mint the additional amount needed for voucher redemption. See [`DEMO_SEED_RUNNER.md`](DEMO_SEED_RUNNER.md) for runner details.

The authenticated Home dashboard is `GET /api/mobile/home`. It composes owner profile summary, wallet balances, upcoming trips, published games, approved Hunter places and active advertisement placements. It collects no analytics events and includes no contact, GPS or media data. The checked-in Swagger inventory contains 120 SQL-mode operations; it describes gated surfaces as well as enabled routes, so deployment feature gates still apply.

## Flutter connection

- API base URL is the origin only; routes already include `/api`.
- Android emulator for the current listener: `http://10.0.2.2:5055`. The emulator's `10.0.2.2` address forwards to the host loopback listener at `127.0.0.1:5055`. Set the Flutter `API_BASE_URL` to this origin; routes already include `/api`.
- Physical Android: the current loopback-only API is not reachable from a phone. For a later physical-device session, start the API with `--urls http://0.0.0.0:5055`, allow inbound TCP 5055 in Windows Firewall on the trusted local network, and set `API_BASE_URL` to `http://<laptop-LAN-IP>:5055`. Use HTTPS with a device-trusted certificate outside a local debug setup. No restart was done for this handoff.
- Local HTTP is for the isolated debug demo only. If Android blocks cleartext traffic, permit HTTP only in the debug manifest/network-security configuration; do not carry that allowance into release builds.
- Login is `POST /api/mobile/identity/login` with JSON `{ "email": "traveller1@demo.wayhunter.test", "password": "<entered securely>" }`. Keep the returned access token in memory and send `Authorization: Bearer <access-token>` to protected routes. Never put passwords, JWT signing secrets, or provider credentials in Flutter.
- Flutter web: use the deployed HTTPS origin and set `Cors:AllowedOrigins` to the exact web origin. Do not use wildcard CORS with credentials.
- Send `Authorization: Bearer <access-token>` on protected routes. Use `Idempotency-Key` for Game draft creation; send the returned `ETag` as `If-Match` for draft updates. Never store provider secrets in Flutter.
- Configure issuer/audience consistently between token issuer and API. Review response DTOs and the endpoint inventory before generating mobile models.

## Database and startup procedure

The selected local runtime database is `Wayhunter_Demo_Final` on `(localdb)\MSSQLLocalDB`. It is ONLINE/read-write, has all eight merged EF migrations applied through `20261123175746_M5GameDraftCreateIdempotency`, and has no pending model changes. No further migration or seed execution is needed for the verified demo state. `Wayhunter_Official` and the M5 test database are outside this target.

The independent API is already running at `http://127.0.0.1:5055`. Do not launch a second instance while this port is occupied. For a later local restart, the standalone launcher validates that the port is free, pins the connection string to `Wayhunter_Demo_Final`, reuses the current-user DPAPI voucher key, sets the Development session lifetime and uses `--no-launch-profile`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File tools/Wayhunter.DemoSeedRunner/StartDemoApi.ps1
```

For a future explicitly authorized seed run, `InvokeDemoSeedSecurely.ps1` performs a read-only catalog/migration preflight, securely prompts for the demo account password, and runs the deterministic seed runner twice with `--skip-voucher`. Do not use `--seed` against any other database or run it again against this already seeded catalog unless a deliberate rerun is required.

## Git review

PR #314 is merged as commit `1db9999`. This final runtime-hardening change is prepared separately on `task/final-demo-runtime-hardening`. See [`FINAL_CHANGED_FILES.md`](FINAL_CHANGED_FILES.md) for the merged implementation inventory and this branch's delta. No migration was added. Recommended commit title: `Harden demo seed and API verification scripts`.

Suggested PR title: `Finalize demo backend runtime and Flutter integration readiness`.

PR summary: add an explicit voucher-skip mode to the idempotent demo seed runner, correct Development session lifetime and PowerShell 5.1 HTTP verification behavior, add a safe standalone loopback API launcher, and update final runtime/Flutter handoff documentation. Verified runtime results: login 200, bearer-authenticated Home 200, anonymous Home 401, Health 200, and Swagger 120 operations. Limitations: voucher image/media, email, Maps, Gemini, weather, and notification providers remain unverified; refresh-token rotation and account-lockout policy remain unresolved.

## Remaining production-readiness blockers

1. M1 refresh-token rotation/replay protection and durable account lockout remain unimplemented pending policy/schema decisions.
2. Trusted private media/receipt verification is unavailable for M3/M4/M5; client media references are not accepted as verified evidence. Private voucher image delivery is not verified, and the demo voucher offer/inventory was intentionally skipped.
3. Resend, Google Places/Routes/Geocoding, Gemini, weather, Cloudinary and notification receiver credentials/delivery receipts are not configured or verified. These providers remain fail-closed.
4. No analytics ingestion is enabled because event schema, privacy, and retention policy are not approved.
5. The 58 previously skipped integration tests still need safe, bounded coverage or documented reasons. Do not enable legacy fixtures that create/drop fixed database names.
6. Physical Android access needs a deliberate API bind/firewall setup; current verified service is loopback only. Production must use HTTPS and deployment-grade secret storage.
