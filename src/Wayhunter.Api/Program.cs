
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Wayhunter.Api.Authorization;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ==========================================
// M7 - Centralized Administrator Authorization
// ==========================================

// Register the Advertisement Administrator policy.
// This does not grant Administrator privileges.
// Actual trusted M1 role resolution remains pending.

builder.Services.AddAdvertisementAdministratorPolicy(
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

    builder.Services.AddWayhunterSessionAuthentication(
        builder.Configuration);

    // ==========================================
    // M6 - Authenticated Wallet SQL Read
    // ==========================================

    builder.Services.AddCoinsWalletSqlReader(
        builder.Configuration);

    if (builder.Configuration.GetValue<bool>(
        "CoinsWallet:EnableSqlRewardWriter"))
    {
        builder.Services.AddCoinsWalletSqlRewardWriter(
            builder.Configuration);
    }

    // ==========================================
    // M7 - Advertisement SQL Runtime
    // ==========================================

    // Advertisement SQL persistence is separately opt-in.
    // Default: false.
    //
    // Enabling M1 shared authentication must not
    // automatically activate M7 SQL persistence.
    //
    // No migrations or database schema changes
    // are executed during service registration.

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

builder.Services.AddConfiguredGamesPersistence(
    builder.Configuration);

// ==========================================
// M3 - GPS Consent (Existing)
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
// M3 - Live Trip Session (Task 3)
// ==========================================

builder.Services.AddSingleton<
    InMemoryLiveTripSessionRepository>();

builder.Services.AddSingleton<
    ILiveTripSessionRepository>(
    sp => sp.GetRequiredService<
        InMemoryLiveTripSessionRepository>());

// ==========================================
// M3 - Trip Invitations (Task 3)
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripInvitationDecisionRepository>();

builder.Services.AddSingleton<
    ITripInvitationDecisionRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripInvitationDecisionRepository>());

// ==========================================
// M3 - Membership Revocation (Task 3)
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripMembershipRevocationRepository>();

builder.Services.AddSingleton<
    ITripMembershipRevocationRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripMembershipRevocationRepository>());

// ==========================================
// M3 - Hazard Backend (Task 4)
// ==========================================

// InMemory persistence is for unit/demo use.
// SQL registration below overrides this interface.

builder.Services.AddSingleton<
    InMemoryHazardReportRepository>();

builder.Services.AddSingleton<
    IHazardReportRepository>(
    sp => sp.GetRequiredService<
        InMemoryHazardReportRepository>());

// The M2 route provider is still an integration
// dependency. Never fake straight-line road geometry.
//
// An actual M2 IRoutesProvider registration,
// when supplied, takes priority over this fallback.

builder.Services.TryAddSingleton<
    IRoutesProvider,
    UnavailableHazardRoutesProvider>();

// ==========================================
// M3 - Configured SQL Persistence
// ==========================================

builder.Services.AddConfiguredTripSafetyPersistence(
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

// Hazard Reporting, Verification,
// Resolution, Route Query and Expiry

builder.Services.AddScoped<
    TripHazardService>();

// ==========================================
// M3 - Optional SQL Hazard Expiry Worker
// ==========================================

// Default: OFF.
//
// Enable only after approved SQL migration,
// with M2 and M3 SQL sharing the same database.

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

var app = builder.Build();

app.UseMiddleware<
    Wayhunter.Api.Middleware.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDevelopment");
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
