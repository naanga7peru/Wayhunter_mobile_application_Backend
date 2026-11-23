
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Wayhunter.Api.Authorization;
using Wayhunter.Api.BackgroundServices;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Controllers.Mobile.Games;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Api.Services.TripSafety;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Wayhunter.Infrastructure.Integrations.HunterPlaces;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Application.Modules.HunterPlaces.Commands;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

var builder = WebApplication.CreateBuilder(args);

var enableDevelopmentDraftApi =
    builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>(
        "Games:EnableDevelopmentDraftHttpApi") &&
    builder.Configuration.GetValue<bool>(
        "Identity:EnableSharedAuthentication") &&
    !builder.Configuration.GetValue<bool>(
        "Games:EnableSqlPersistence");

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
        manager.FeatureProviders.Add(
            new GameDraftApiControllerFeatureProvider(
                enableDevelopmentDraftApi)));

if (enableDevelopmentDraftApi)
{
    builder.Services.AddScoped<GameDraftApplicationAdapter>();
    builder.Services.AddScoped<GameDraftAuthenticationFilter>();
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiErrorResponse.Create(
                "GAME_DRAFT_INVALID",
                "The draft request is invalid.",
                context.HttpContext.TraceIdentifier));
    });
}

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

// ==========================================
// M7 - Centralized Administrator Authorization
// ==========================================

builder.Services.AddAdvertisementAdministratorPolicy(
    builder.Configuration);

builder.Services.AddCoinsWalletAdministratorPolicy(
    builder.Configuration);

// ==========================================
// M7 - Advertisement Image Storage
// ==========================================

// Disabled unless server-side Cloudinary settings are supplied explicitly.
builder.Services.AddConfiguredAdvertisementImageStorage(
    builder.Configuration);
// ==========================================
// M1 - Identity Authentication
// ==========================================

var enableSharedAuthentication = builder.Configuration
    .GetValue<bool>("Identity:EnableSharedAuthentication");

if (enableSharedAuthentication)
{
    builder.Services.AddWayhunterIdentityPersistence(
        builder.Configuration);

    // The M4 status route uses the authoritative shared SQL store.
    // No in-memory fallback is registered for runtime requests.
    builder.Services.AddScoped<
        IHunterSubmissionStatusReader,
        SqlHunterSubmissionStatusReader>();
    builder.Services.AddScoped<HunterSubmissionStatusService>();
    builder.Services.AddScoped<IHunterDraftStore, SqlHunterDraftStore>();
    builder.Services.AddScoped<HunterDraftService>();
    builder.Services.AddScoped<IHunterEvidenceStore, SqlHunterEvidenceStore>();
    builder.Services.AddScoped<IHunterMediaReceiptVerifier, DisabledHunterMediaReceiptVerifier>();
    builder.Services.AddScoped<HunterEvidenceService>();
    builder.Services.AddScoped<IHunterReviewStore, SqlHunterReviewStore>();
    builder.Services.AddScoped<IHunterReviewService, HunterReviewService>();
    builder.Services.AddScoped<IHunterSubmissionStatusTransitionStore, SqlHunterSubmissionStatusTransitionStore>();
    builder.Services.AddScoped<HunterSubmissionStatusTransitionService>();

    // M4 Batch 2 authenticated capture-session runtime.
    builder.Services.AddScoped<
        IHunterCaptureSubmissionAccessReader,
        SqlHunterCaptureSubmissionAccessReader>();
    builder.Services.AddScoped<
        IHunterCaptureSessionStore,
        SqlHunterCaptureSessionStore>();
    builder.Services.AddScoped<HunterCaptureSubmissionOwnershipService>();
    builder.Services.AddScoped<HunterCaptureSessionIdentityResolutionService>();
    builder.Services.AddScoped<HunterCaptureSessionService>();
    builder.Services.AddScoped<HunterStartCaptureSessionHandler>();
    builder.Services.AddScoped<HunterStartAuthorizedCaptureSessionHandler>();
    builder.Services.AddScoped<
        IHunterCaptureSessionIdentityResolver,
        TrustedHunterCaptureSessionIdentityResolver>();

    builder.Services.AddWayhunterSessionAuthentication(
        builder.Configuration);

    builder.Services.AddCoinsWalletSqlReader(
        builder.Configuration);

    if (builder.Configuration.GetValue<bool>(
        "CoinsWallet:EnableSqlRewardWriter"))
    {
        builder.Services.AddCoinsWalletSqlRewardWriter(
            builder.Configuration);

        if (builder.Configuration.GetValue<bool>(
                "CoinsWallet:EnableSqlRewardOutboxStore") &&
            builder.Configuration.GetValue<bool>(
                "CoinsWallet:EnableRewardOutboxWorker"))
        {
            builder.Services.AddHostedService<
                CoinRewardOutboxWorker>();
        }
    }

    if (builder.Configuration.GetValue<bool>(
        "CoinsWallet:EnableSqlVoucherRuntime"))
    {
        builder.Services.AddCoinsWalletSqlVoucherRuntime(
            builder.Configuration);
    }

    var enableAdvertisementSqlPersistence =
        builder.Configuration.GetValue<bool>(
            "Advertisements:EnableSqlPersistence");

    if (enableAdvertisementSqlPersistence)
    {
        builder.Services.AddHomeAdsAnalyticsSqlPersistence(
            builder.Configuration);
    }
}
else
{
    builder.Services
        .AddAuthentication(
            DisabledIdentityAuthenticationHandler.SchemeName)
        .AddScheme<
            AuthenticationSchemeOptions,
            DisabledIdentityAuthenticationHandler>(
            DisabledIdentityAuthenticationHandler.SchemeName,
            _ => { });

    builder.Services.AddAuthorization();
}

// ==========================================
// M2 - Travel Planning
// ==========================================

builder.Services.AddTravelPlanningModule();

builder.Services.AddConfiguredTravelPlanningPersistence(
    builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

// ==========================================
// M5 - Games SQL Repository
// ==========================================

builder.Services.AddGamesApplicationServices();

builder.Services.AddConfiguredGamesPersistence(
    builder.Configuration,
    builder.Environment);

builder.Services.AddGamesReadApiServices(
    builder.Configuration);

// ==========================================
// M3 - GPS Consent
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripSharingConsentRepository>();

builder.Services.AddSingleton<
    ITripSharingConsentRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripSharingConsentRepository>());

builder.Services.AddSingleton<
    ITripLiveLocationAccessReader>(
    sp => sp.GetRequiredService<
        InMemoryTripSharingConsentRepository>());

builder.Services.AddScoped<
    TripSharingConsentService>();

// ==========================================
// M3 - Live Trip Session
// ==========================================

builder.Services.AddSingleton<
    InMemoryLiveTripSessionRepository>();

builder.Services.AddSingleton<
    ILiveTripSessionRepository>(
    sp => sp.GetRequiredService<
        InMemoryLiveTripSessionRepository>());

// ==========================================
// M3 - Trip Invitations
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripInvitationDecisionRepository>();

builder.Services.AddSingleton<
    ITripInvitationDecisionRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripInvitationDecisionRepository>());

// ==========================================
// M3 - Membership Revocation
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripMembershipRevocationRepository>();

builder.Services.AddSingleton<
    ITripMembershipRevocationRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripMembershipRevocationRepository>());

// ==========================================
// M3 - Hazard Backend
// ==========================================

builder.Services.AddSingleton<
    InMemoryHazardReportRepository>();

builder.Services.AddSingleton<
    IHazardReportRepository>(
    sp => sp.GetRequiredService<
        InMemoryHazardReportRepository>());

builder.Services.TryAddSingleton<
    IRoutesProvider,
    UnavailableHazardRoutesProvider>();

// ==========================================
// M3 - Configured SQL Persistence
// ==========================================

builder.Services.AddSingleton<InMemoryTripInvitationCreateStore>();
builder.Services.AddSingleton<ITripInvitationCreateStore>(sp => sp.GetRequiredService<InMemoryTripInvitationCreateStore>());
builder.Services.TryAddScoped<ITripInviteBlockRelationshipReader, UnconfiguredTripInviteBlockRelationshipReader>();
builder.Services.AddScoped<ITripInviteEligibilityAccess, UnavailableTripInviteEligibilityAccess>();
builder.Services.AddScoped<TripInvitationCreateService>();

builder.Services.AddConfiguredTripSafetyPersistence(
    builder.Configuration);

// ==========================================
// M3 - Notification Outbox Worker
// ==========================================

builder.Services
    .AddConfiguredTripSafetyNotificationOutboxWorker(
        builder.Configuration);

// ==========================================
// M3 - Application Services / Authorization
// ==========================================

builder.Services.AddScoped<
    LiveTripSessionService>();

builder.Services.AddScoped<
    TripInvitationDecisionService>();

builder.Services.AddScoped<
    TripMembershipRevocationService>();

builder.Services.AddScoped<
    TripSafetyTripAccessService>();

builder.Services.AddScoped<
    ITripSessionRecoveryAccessChecker>(
    sp => sp.GetRequiredService<
        TripSafetyTripAccessService>());

builder.Services.AddScoped<
    LiveTripSessionRecoveryService>();

builder.Services.AddScoped<
    TripHazardService>();

builder.Services.AddSingleton(new LiveTripAlertSettings());
builder.Services.AddSingleton<LiveTripAlertState>();
builder.Services.AddScoped<LiveTripSafetyAlerts>();

// ==========================================
// M3 - Optional SQL Hazard Expiry Worker
// ==========================================

var enableHazardExpiryWorker = builder.Configuration
    .GetValue<bool>("TripSafety:EnableHazardExpiryWorker");

var enableM3Sql = builder.Configuration
    .GetValue<bool>("TripSafety:EnableSqlPersistence");

var enableM2Sql = builder.Configuration
    .GetValue<bool>("TravelPlanning:EnableSqlPersistence");

if (enableHazardExpiryWorker &&
    enableM3Sql &&
    enableM2Sql)
{
    builder.Services.AddHostedService<
        HazardExpiryHostedService>();
}

// ==========================================
// M3 - SignalR Infrastructure
// ==========================================

builder.Services.AddSignalR();

builder.Services.AddSingleton<
    TripLocationSubscriberRegistry>();

builder.Services.AddScoped<
    TripLocationFanoutService>();

// NEW: shared cleanup service for successful
// finish, membership removal and GPS consent revoke.

builder.Services.AddSingleton<
    TripLocationSubscriptionCleanup>();

// ==========================================
// Development CORS
// ==========================================

if (builder.Environment.IsDevelopment())
{
    var origins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(
        options => options.AddPolicy(
            "LocalDevelopment",
            policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()));
}

// ==========================================
// Application Pipeline
// ==========================================



builder.Services.AddTripMemoryServices(builder.Configuration);
builder.Services.AddNotificationFoundation(builder.Configuration);
builder.Services.AddAdminSafetyMonitoring();
builder.Services.AddTripSafetyBatch2Services(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<
    Wayhunter.Api.Middleware.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDevelopment");

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<TripLocationHub>(
    "/hubs/trip-location");

app.Run();

public partial class Program
{
}
