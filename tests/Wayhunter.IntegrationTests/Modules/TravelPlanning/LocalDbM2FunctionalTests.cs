using System.Net.Http.Headers;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Integrations.Ai;
using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Integrations.Weather;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    private const string Trips = "/api/mobile/trip-plans";

    [LocalDbM2Fact]
    public Task VerifyM2Operations()
    {
        var catalog = Environment.GetEnvironmentVariable("WAYHUNTER_M2_CATALOG");
        Assert.True(catalog is not null && Regex.IsMatch(catalog, @"\AWayhunter_M2_Test_[a-f0-9]{32}\z"),
            "Use a freshly created dedicated M2 catalog.");
        var providers = new M2Providers();
        var fault = new CopyCommitFault();
        return RunIsolatedAsync(catalog!, migrateFreshCatalog: true,
            (client, db, capture, clock, _) =>
            {
                clock.Offset = TimeSpan.Zero;
                return VerifyM2Async(client, db, capture, providers, fault);
            },
            services =>
            {
                services.ConfigureDbContext<WayhunterSharedDbContext>(options => options.AddInterceptors(fault));
                Replace<IAiTripGenerationProvider>(services, providers);
                services.RemoveAll<GoogleRoutesProvider>();
                services.RemoveAll<GooglePlacePickerProvider>();
                services.RemoveAll<OpenWeatherMapProvider>();
                Replace<IRoutesProvider>(services, providers);
                services.RemoveAll<PreviewTripRouteService>();
                services.AddScoped(_ => new PreviewTripRouteService(providers));
                Replace<IPlacesProvider>(services, providers);
                Replace<IPlaceDetailsProvider>(services, providers);
                Replace<INearbyPlacesProvider>(services, providers);
                Replace<IWeatherProvider>(services, providers);
                Replace<IWeatherForecastProvider>(services, providers);
                services.PostConfigure<AiTripProviderOptions>(options =>
                {
                    options.Enabled = true;
                    options.ApiKey = "synthetic";
                    options.Model = "synthetic";
                    options.MaxRequestsPerUserPerDay = 4;
                });
                services.PostConfigure<MapsProviderOptions>(options =>
                {
                    options.EnablePlaces = options.EnableRoutes = true;
                    options.PlacesServerApiKey = options.RoutesServerApiKey = "synthetic";
                });
            });
    }

    private static void Replace<T>(IServiceCollection services, T provider) where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(provider);
    }

    private async Task VerifyM2Async(HttpClient client, WayhunterSharedDbContext db, Capture capture, M2Providers fake, CopyCommitFault fault)
    {
        var owner = await RegisterAsync("Planner");
        var member = await RegisterAsync("Member");
        var outsider = await RegisterAsync("Outsider");
        DateOnly start = new(2026, 12, 1);
        var draft = new CreateTripPlanDto("M2 functional trip", start, start.AddDays(1), 25000, "Car", 2)
        {
            InitialStops = [new("Fort", "fake-fort", 6.9344, 79.8428), new("Galle Face", "fake-galle", 6.9271, 79.8447)]
        };
        var observed = new Dictionary<(HttpMethod, string), bool>();
        void Record(HttpMethod method, string route, int expected, int actual)
        {
            var key = (method, route);
            lock (observed)
                observed[key] = observed.GetValueOrDefault(key, true) && expected == actual;
            output.WriteLine($"{method} {route}: expected {expected}, actual {actual}.");
        }
        async Task<T?> Probe<T>(HttpMethod method, string route, int expected, string suffix = "", object? body = null,
            byte[]? version = null, string? key = null)
        {
            using var request = new HttpRequestMessage(method, Trips + suffix);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{Convert.ToBase64String(version)}\"");
            if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            using var response = await client.SendAsync(request);
            Record(method, route, expected, (int)response.StatusCode);
            if (expected is >= 200 and < 300 and not 204 && typeof(T) != typeof(object))
            {
                Assert.Equal(expected, (int)response.StatusCode);
                return await response.Content.ReadFromJsonAsync<T>();
            }
            return default;
        }
        Task ProbeStatus(HttpMethod verb, string route, int expected, string suffix = "", object? body = null,
            byte[]? version = null, string? key = null) =>
            Probe<object>(verb, route, expected, suffix, body, version, key);
        void Authenticate(string? token) => client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        var get = HttpMethod.Get;
        var post = HttpMethod.Post;
        var put = HttpMethod.Put;
        var delete = HttpMethod.Delete;
        const string item = "/{tripPlanId}";
        const string stops = item + "/stops";
        const string order = stops + "/order";
        const string stop = stops + "/{stopId}";
        const string suggested = "/suggested";
        const string suggestedItem = suggested + item;
        const string copy = "/suggested/{sourceTripPlanId}/copy";
        const string consent = item + "/reuse-consent";
        const string routePreview = "/routes/preview";
        const string ai = "/ai/generate";
        const string nearby = "/nearby";
        const string place = "/places/{providerPlaceId}";
        const string search = "/places/search";
        const string weather = "/weather";
        const string forecast = "/weather/forecast";
        const string coordinates = "?latitude=6.93&longitude=79.84";
        using (var swagger = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json"))
        {
            var operation = swagger!.RootElement.GetProperty("paths").GetProperty(Trips + copy).GetProperty("post");
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" && parameter.GetProperty("in").GetString() == "header");
            Assert.Contains("Idempotency-Key", operation.GetProperty("description").GetString());
        }
        var sample = "/" + Guid.NewGuid().ToString("D");
        (HttpMethod Verb, string Route, string Url)[] endpoints =
        [
            (get, "", ""), (post, "", ""), (get, item, sample), (put, item, sample), (delete, item, sample),
            (post, stops, sample + "/stops"), (put, order, sample + "/stops/order"), (delete, stop, sample + "/stops" + sample),
            (get, suggested, suggested), (get, suggestedItem, suggested + sample), (post, copy, suggested + sample + "/copy"),
            (post, routePreview, routePreview), (post, ai, ai), (get, nearby, nearby),
            (get, place, "/places/fake-fort"), (get, search, search), (get, weather, weather), (get, forecast, forecast),
            (put, consent, sample + "/reuse-consent")
        ];
        foreach (string? token in new[] { null, "invalid.jwt.token" })
        {
            Authenticate(token);
            foreach (var endpoint in endpoints)
                await ProbeStatus(endpoint.Verb, endpoint.Route, 401, endpoint.Url);
        }

        Authenticate(owner.Token);
        await ProbeStatus(post, "", 400, body: draft with { BudgetLkr = -1 });
        var created = Assert.IsType<TripPlanResponseDto>(await Probe<TripPlanResponseDto>(post, "", 201, body: draft));
        var id = created.TripPlanId;
        var url = "/" + id;
        Assert.Equal(8, created.RowVersion.Length);
        Assert.Equal(2, await db.TripStops.CountAsync(x => x.TripPlanId == id));
        Assert.True(await db.Trips.AnyAsync(x => x.Id == id && x.OwnerUserId == owner.Id && x.BudgetLkr == 25000));
        var list = await Probe<JsonElement>(get, "", 200);
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("tripPlanId").GetGuid() == id);
        await ProbeStatus(get, item, 200, url);
        var update = new UpdateTripPlanDto("Updated trip", start, start.AddDays(1), 30000, "Car", 2)
            { ExpectedRowVersion = created.RowVersion };
        await ProbeStatus(put, item, 400, url, update with { BudgetLkr = -1 });
        Authenticate(outsider.Token);
        Assert.Empty((await Probe<JsonElement>(get, "", 200)).EnumerateArray());
        await ProbeStatus(get, item, 404, url);
        await ProbeStatus(put, item, 404, url, update);
        await ProbeStatus(delete, item, 404, url);
        var third = new AddTripStopDto("Museum", "fake-museum", 6.91, 79.86, created.RowVersion);
        await ProbeStatus(post, stops, 404, url + "/stops", third);
        await ProbeStatus(put, order, 404, url + "/stops/order", new ReorderTripStopsDto(created.Stops.Select(x => x.StopId).ToArray(), created.RowVersion));
        await ProbeStatus(delete, stop, 404, url + "/stops/" + created.Stops[0].StopId, version: created.RowVersion);

        Authenticate(owner.Token);
        // Race two updates carrying the same SQL concurrency token.
        async Task<int> ConcurrentUpdate()
        {
            using var response = await client.PutAsJsonAsync(Trips + url, update);
            return (int)response.StatusCode;
        }
        var race = (await Task.WhenAll(ConcurrentUpdate(), ConcurrentUpdate())).Order().ToArray();
        Record(put, item, 200, race[0]);
        Record(put, item, 409, race[1]);
        Assert.True(await db.Trips.AnyAsync(x => x.Id == id && x.BudgetLkr == 30000 && x.TripName == "Updated trip"));
        var updated = (await Probe<TripPlanResponseDto>(get, item, 200, url))!;
        await ProbeStatus(post, stops, 409, url + "/stops", third);
        await ProbeStatus(post, stops, 400, url + "/stops", third with { Latitude = 100, ExpectedRowVersion = updated.RowVersion });
        var added = (await Probe<TripPlanResponseDto>(post, stops, 200, url + "/stops", third with { ExpectedRowVersion = updated.RowVersion }))!;
        Assert.Equal(3, await db.TripStops.CountAsync(x => x.TripPlanId == id));
        var desired = added.Stops.Reverse().Select(x => x.StopId).ToArray();
        await ProbeStatus(put, order, 400, url + "/stops/order", new ReorderTripStopsDto([desired[0], desired[0]], added.RowVersion));
        await ProbeStatus(put, order, 409, url + "/stops/order", new ReorderTripStopsDto(desired, updated.RowVersion));
        var reordered = (await Probe<TripPlanResponseDto>(put, order, 200, url + "/stops/order", new ReorderTripStopsDto(desired, added.RowVersion)))!;
        Assert.Equal(desired, await SqlOrder());
        var removedUrl = url + "/stops/" + desired[1];
        await ProbeStatus(delete, stop, 400, removedUrl);
        await ProbeStatus(delete, stop, 409, removedUrl, version: added.RowVersion);
        Assert.Equal(desired, await SqlOrder());
        var removed = (await Probe<TripPlanResponseDto>(delete, stop, 200, removedUrl, version: reordered.RowVersion))!;
        Assert.Equal(new[] { desired[0], desired[2] }, await SqlOrder());
        Assert.Equal(new[] { 0, 1 }, removed.Stops.Select(x => x.OrderIndex));

        var time = DateTimeOffset.UtcNow;
        var invitation = TripInvitation.Create(Guid.NewGuid(), id, owner.Id, member.Id, time);
        invitation.Accept(time);
        var membership = TripMember.FromAcceptedInvitation(Guid.NewGuid(), invitation);
        var session = LiveTripSession.Start(Guid.NewGuid(), id, time);
        session.Finish(time.AddMinutes(1));
        db.TripInvitations.Add(invitation);
        db.TripMembers.Add(membership);
        await db.SaveChangesAsync();
        var copyUrl = suggested + url + "/copy";
        var consentUrl = url + "/reuse-consent";
        Authenticate(owner.Token);
        await ProbeStatus(put, consent, 400, consentUrl, new { });
        await ProbeStatus(put, consent, 204, consentUrl, new { allowed = true });
        Authenticate(member.Token);
        await ProbeStatus(post, copy, 404, copyUrl, key: "incomplete");
        Authenticate(owner.Token);
        await ProbeStatus(put, consent, 204, consentUrl, new { allowed = false });
        db.LiveTripSessions.Add(session);
        db.TripMemories.Add(new Wayhunter.Infrastructure.Persistence.Models.TripSafety.TripMemoryRow
        {
            MemoryId = Guid.NewGuid(), TripPlanId = id, OwnerUserId = owner.Id,
            CreateOperationId = Guid.NewGuid(), Caption = "Synthetic private memory",
            CreatedAtUtc = time, UpdatedAtUtc = time
        });
        await db.SaveChangesAsync();
        Authenticate(outsider.Token);
        Assert.Empty((await Probe<JsonElement>(get, suggested, 200, suggested)).EnumerateArray());
        await ProbeStatus(get, suggestedItem, 404, suggested + url);
        await ProbeStatus(post, copy, 404, copyUrl, key: "outsider");
        await ProbeStatus(put, consent, 404, consentUrl, new { allowed = true });
        Authenticate(owner.Token);
        Assert.Empty((await Probe<JsonElement>(get, suggested, 200, suggested)).EnumerateArray());
        Authenticate(member.Token);
        await ProbeStatus(get, suggested, 400, suggested + "?limit=0");
        var suggestions = await Probe<JsonElement>(get, suggested, 200, suggested);
        Assert.Equal(id, Assert.Single(suggestions.EnumerateArray()).GetProperty("tripPlanId").GetGuid());
        var suggestion = await Probe<JsonElement>(get, suggestedItem, 200, suggested + url);
        Assert.DoesNotContain(suggestion.EnumerateObject(), x => x.Name is "ownerUserId" or "email" or "sessionId");
        await ProbeStatus(post, copy, 400, copyUrl);
        await ProbeStatus(post, copy, 400, copyUrl, key: "invalid key");
        using (var duplicateHeader = new HttpRequestMessage(post, Trips + copyUrl))
        {
            duplicateHeader.Headers.TryAddWithoutValidation("Idempotency-Key", new[] { "first-key", "second-key" });
            using var response = await client.SendAsync(duplicateHeader);
            Record(post, copy, 400, (int)response.StatusCode);
        }
        await ProbeStatus(post, copy, 400, suggested + "/" + Guid.Empty + "/copy", key: "empty-source");
        await ProbeStatus(post, copy, 404, suggested + sample + "/copy", key: "missing-source");
        var beforeCopy = await db.Trips.CountAsync();
        await ProbeStatus(post, copy, 404, copyUrl, key: "missing-consent");
        Assert.Equal(beforeCopy, await db.Trips.CountAsync());
        Authenticate(owner.Token);
        await ProbeStatus(put, consent, 204, consentUrl, new { allowed = true });
        Assert.True(await db.Trips.AnyAsync(trip => trip.Id == id && trip.AllowsSuggestedPlanReuse));
        var sourceBefore = await db.Trips.AsNoTracking().SingleAsync(trip => trip.Id == id);
        Authenticate(member.Token);

        const string operationKey = "simultaneous-copy";
        Task<CopiedSuggestedTripPlanDto?> Copy(string key) =>
            Probe<CopiedSuggestedTripPlanDto>(post, copy, 201, copyUrl, key: key);
        var copies = await Task.WhenAll(Copy(operationKey), Copy(operationKey));
        var draftCopy = Assert.IsType<CopiedSuggestedTripPlanDto>(copies[0]);
        Assert.Equal(draftCopy.TripPlanId, copies[1]!.TripPlanId);
        Assert.Equal(draftCopy.TripPlanId, (await Copy(operationKey))!.TripPlanId);
        await ProbeStatus(post, copy, 409, suggested + sample + "/copy", key: operationKey);

        Assert.NotEqual(id, draftCopy.TripPlanId);
        Assert.Equal(id, draftCopy.SourceTripPlanId);
        Assert.Equal(8, draftCopy.RowVersion.Length);
        Assert.Equal(removed.Stops.Select(x => x.Name), draftCopy.Stops.Select(x => x.Name));
        Assert.Equal(removed.Stops.Select(x => (x.PlaceId, x.Latitude, x.Longitude)),
            draftCopy.Stops.Select(x => (x.PlaceId, x.Latitude, x.Longitude)));
        Assert.Equal(removed.Stops.Select(x => x.OrderIndex), draftCopy.Stops.Select(x => x.OrderIndex));
        Assert.Empty(removed.Stops.Select(x => x.StopId).Intersect(draftCopy.Stops.Select(x => x.StopId)));
        Assert.Equal(beforeCopy + 1, await db.Trips.CountAsync());
        var attribution = db.Set<Wayhunter.Infrastructure.Persistence.Models.TravelPlanning.TripPlanCopyAttributionRow>();
        Assert.True(await attribution.AnyAsync(row => row.TripPlanId == draftCopy.TripPlanId &&
            row.SourceTripPlanId == id && row.SourceOwnerUserId == owner.Id && row.RequesterUserId == member.Id));
        Assert.True(await db.Trips.AnyAsync(trip => trip.Id == draftCopy.TripPlanId && trip.OwnerUserId == member.Id &&
            trip.DraftStatus == TripDraftStatus.Draft && !trip.AllowsSuggestedPlanReuse && trip.BudgetLkr == 30000));
        Assert.False(await db.LiveTripSessions.AnyAsync(x => x.TripPlanId == draftCopy.TripPlanId));
        Assert.False(await db.TripMembers.AnyAsync(x => x.TripPlanId == draftCopy.TripPlanId));
        Assert.False(await db.TripInvitations.AnyAsync(x => x.TripPlanId == draftCopy.TripPlanId));
        Assert.False(await db.TripMemories.AnyAsync(x => x.TripPlanId == draftCopy.TripPlanId));
        Assert.True(await db.TripMemories.AnyAsync(x => x.TripPlanId == id && x.Caption == "Synthetic private memory"));
        var privacy = await Probe<JsonElement>(post, copy, 201, copyUrl, key: operationKey);
        Assert.DoesNotContain(privacy.EnumerateObject(), x => x.Name is "sourceOwnerUserId" or "ownerUserId" or "memories" or "sessionId");

        var countAfterCopy = await db.Trips.CountAsync();
        var stopsAfterCopy = await db.TripStops.CountAsync();
        var attributionAfterCopy = await attribution.CountAsync();
        fault.BeforeCommit = true;
        await ProbeStatus(post, copy, 500, copyUrl, key: "rollback-copy");
        Assert.False(fault.BeforeCommit);
        Assert.Equal(countAfterCopy, await db.Trips.CountAsync());
        Assert.Equal(stopsAfterCopy, await db.TripStops.CountAsync());
        Assert.Equal(attributionAfterCopy, await attribution.CountAsync());
        Assert.NotEqual(Guid.Empty, (await Copy("rollback-copy"))!.TripPlanId);
        fault.AfterCommit = true;
        var recovered = (await Copy("ambiguous-copy"))!;
        Assert.False(fault.AfterCommit);
        Assert.Equal(recovered.TripPlanId, (await Copy("ambiguous-copy"))!.TripPlanId);
        Assert.Equal(countAfterCopy + 2, await db.Trips.CountAsync());
        Assert.Equal(attributionAfterCopy + 2, await attribution.CountAsync());
        var sourceAfter = await db.Trips.AsNoTracking().SingleAsync(trip => trip.Id == id);
        Assert.Equal(sourceBefore.RowVersion, sourceAfter.RowVersion);
        Assert.Equal(removed.Stops.Select(x => x.StopId), await SqlOrder());

        Authenticate(owner.Token);
        await ProbeStatus(put, consent, 204, consentUrl, new { allowed = false });
        Authenticate(member.Token);
        await ProbeStatus(post, copy, 404, copyUrl, key: "revoked-consent");
        await ProbeStatus(post, copy, 404, copyUrl, key: operationKey);
        await ProbeStatus(get, item, 200, "/" + draftCopy.TripPlanId);
        Authenticate(owner.Token);
        await ProbeStatus(put, consent, 204, consentUrl, new { allowed = true });
        membership.Remove(time.AddMinutes(2));
        await db.SaveChangesAsync();
        Authenticate(member.Token);
        await ProbeStatus(get, suggestedItem, 404, suggested + url);
        await ProbeStatus(post, copy, 404, copyUrl, key: "removed-member");
        var cancellable = (await Probe<TripPlanResponseDto>(post, "", 201, body: draft))!;
        await ProbeStatus(delete, item, 204, "/" + cancellable.TripPlanId);
        Assert.True(await db.Trips.AnyAsync(x => x.Id == cancellable.TripPlanId && x.DraftStatus == TripDraftStatus.Cancelled));

        Authenticate(owner.Token);
        int tripCount = await db.Trips.CountAsync();
        int stopCount = await db.TripStops.CountAsync();
        var waypoints = new RoutePreviewRequest([new(6.93, 79.84), new(6.92, 79.85)]);
        await ProbeStatus(post, routePreview, 400, routePreview, new RoutePreviewRequest([]));
        var geometry = await Probe<JsonElement>(post, routePreview, 200, routePreview, waypoints);
        Assert.Equal(2, geometry.GetProperty("routeGeometry").GetArrayLength());
        Assert.False(geometry.GetProperty("trafficAware").GetBoolean());
        fake.InvalidRoute = true;
        await ProbeStatus(post, routePreview, 502, routePreview, waypoints);
        fake.InvalidRoute = false;
        fake.RouteUnavailable = true;
        await ProbeStatus(post, routePreview, 503, routePreview, waypoints);
        var itinerary = new AiTripItineraryRequest("Colombo", "Galle", start, start.AddDays(1), 10000, "Car", 2, null);
        await ProbeStatus(post, ai, 400, ai, itinerary with { BudgetLkr = -1 });
        var proposal = await Probe<JsonElement>(post, ai, 200, ai, itinerary);
        Assert.True(proposal.GetProperty("requiresUserReview").GetBoolean());
        await ProbeStatus(post, ai, 502, ai, itinerary with { Preferences = "malformed" });
        await ProbeStatus(post, ai, 502, ai, itinerary with { Preferences = "failure" });
        await ProbeStatus(post, ai, 504, ai, itinerary with { Preferences = "timeout" });
        await ProbeStatus(post, ai, 429, ai, itinerary);
        await ProbeStatus(get, nearby, 400, nearby + coordinates + "&category=invalid");
        var nearbyResult = await Probe<JsonElement>(get, nearby, 200, nearby + coordinates + "&category=food");
        Assert.False(nearbyResult.GetProperty("hunterVerified").GetBoolean());
        Assert.Single(nearbyResult.GetProperty("places").EnumerateArray());
        await ProbeStatus(get, search, 400, search);
        var placesResult = await Probe<JsonElement>(get, search, 200, search + "?query=Fort");
        Assert.False(Assert.Single(placesResult.EnumerateArray()).GetProperty("isVerified").GetBoolean());
        await ProbeStatus(get, place, 400, "/places/bad.id");
        await ProbeStatus(get, place, 404, "/places/missing");
        var details = await Probe<JsonElement>(get, place, 200, "/places/fake-fort");
        Assert.False(details.GetProperty("isVerified").GetBoolean());
        await ProbeStatus(get, weather, 400, weather);
        var weatherResult = await Probe<JsonElement>(get, weather, 200, weather + coordinates);
        Assert.Equal("fresh", weatherResult.GetProperty("status").GetString());
        await ProbeStatus(get, forecast, 400, forecast);
        var forecastResult = await Probe<JsonElement>(get, forecast, 200, forecast + coordinates);
        Assert.Single(forecastResult.GetProperty("forecasts").EnumerateArray());
        Assert.Equal(tripCount, await db.Trips.CountAsync());
        Assert.Equal(stopCount, await db.TripStops.CountAsync());

        Assert.Equal(19, observed.Count);
        Assert.True(observed.Values.All(result => result), "See the recorded HTTP responses.");

        Task<Guid[]> SqlOrder() => db.TripStops.Where(x => x.TripPlanId == id)
            .OrderBy(x => x.OrderIndex).Select(x => x.Id).ToArrayAsync();

        async Task<(string Id, string Token)> RegisterAsync(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            var password = Guid.NewGuid().ToString("N") + "A!";
            using var registration = await client.PostAsJsonAsync("/api/mobile/identity/register",
                new { displayName = name, email, password, confirmPassword = password });
            Assert.Equal(201, (int)registration.StatusCode);
            using var verify = await client.PostAsJsonAsync("/api/mobile/identity/verify-otp",
                new { email, code = capture.Codes.Last() });
            Assert.Equal(204, (int)verify.StatusCode);
            using var login = await client.PostAsJsonAsync("/api/mobile/identity/login", new { email, password });
            Assert.Equal(200, (int)login.StatusCode);
            using var result = (await login.Content.ReadFromJsonAsync<JsonDocument>())!;
            return (result.RootElement.GetProperty("userId").GetString()!, result.RootElement.GetProperty("accessToken").GetString()!);
        }
    }

    private sealed class CopyCommitFault : DbTransactionInterceptor
    {
        public bool BeforeCommit { get; set; }
        public bool AfterCommit { get; set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (BeforeCommit)
            {
                BeforeCommit = false;
                throw new InvalidOperationException("Synthetic pre-commit failure.");
            }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (AfterCommit)
            {
                AfterCommit = false;
                throw new TimeoutException("Synthetic lost commit acknowledgement.");
            }
            return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
        }
    }

    private sealed class M2Providers : IAiTripGenerationProvider, IRoutesProvider, IPlacesProvider,
        IPlaceDetailsProvider, INearbyPlacesProvider, IWeatherProvider, IWeatherForecastProvider
    {
        public bool InvalidRoute { get; set; }
        public bool RouteUnavailable { get; set; }

        public Task<string> GenerateItineraryJsonAsync(AiTripItineraryRequest request, CancellationToken cancellationToken) =>
            request.Preferences switch
            {
                "malformed" => Task.FromResult("{"),
                "failure" => Task.FromException<string>(new HttpRequestException()),
                "timeout" => Task.FromException<string>(new TimeoutException()),
                _ => Task.FromResult("""{"days":[{"date":"2026-12-01","stops":[{"placeName":"Fort","providerPlaceId":null,"arrivalTime":null,"estimatedCostLkr":500}]}],"estimatedTotalCostLkr":500,"requiresUserReview":true}""")
            };

        public Task<RouteProviderResult> CalculateRouteAsync(IReadOnlyList<RouteWaypoint> orderedStops, CancellationToken cancellationToken) =>
            Task.FromResult(RouteUnavailable
                ? new RouteProviderResult(false, [], null, null, null, "synthetic unavailable")
                : new RouteProviderResult(true, InvalidRoute ? [] : orderedStops, 5, 15, "synthetic", null));

        public Task<PlacesProviderResult> SearchAsync(string query, CancellationToken cancellationToken) => Places();
        public Task<PlacesProviderResult> GetDetailsAsync(string providerPlaceId, CancellationToken cancellationToken) =>
            providerPlaceId == "missing" ? Task.FromResult(new PlacesProviderResult(true, [], null)) : Places();
        public Task<PlacesProviderResult> SearchNearbyAsync(double latitude, double longitude, int radiusMeters,
            string searchTerm, CancellationToken cancellationToken) => Places();

        private static Task<PlacesProviderResult> Places() =>
            Task.FromResult(new PlacesProviderResult(true, [new("fake-fort", "Fort", 6.93, 79.84, "synthetic")], null));

        public Task<WeatherProviderResult> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken) =>
            Task.FromResult(new WeatherProviderResult(true,
                new WeatherObservation(latitude, longitude, 28, "Clear", DateTimeOffset.UtcNow, "synthetic"), null));

        public Task<IReadOnlyList<WeatherForecastPoint>?> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WeatherForecastPoint>?>([new(DateTimeOffset.UtcNow.AddHours(3), 29, "Clear")]);
    }
}

public sealed class LocalDbM2FactAttribute : FactAttribute
{
    public LocalDbM2FactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAYHUNTER_LOCAL_M2_FUNCTIONAL") != "1")
            Skip = "Opt in with WAYHUNTER_LOCAL_M2_FUNCTIONAL=1 and a fresh dedicated LocalDB catalog.";
    }
}
