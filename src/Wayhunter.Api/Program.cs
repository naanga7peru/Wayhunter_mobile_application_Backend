
using Microsoft.AspNetCore.Authentication;

using Wayhunter.Api.Authorization;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

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

    // M6 - SQL Reward Writer (opt-in).
    // Default: false.
    // No automatic migrations or worker.

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

// SQL persistence is opt-in.
// Existing InMemory behavior remains the default.

builder.Services.AddConfiguredTravelPlanningPersistence(
    builder.Configuration);

// Shared trusted server clock.

builder.Services.AddSingleton(TimeProvider.System);

// ==========================================
// M5 - Games SQL Repository
// ==========================================

// SQL Game CRUD is opt-in.

builder.Services.AddConfiguredGamesPersistence(
    builder.Configuration);

// ==========================================
// M3 - Trip Safety / GPS Consent
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
// M3 - InMemory Session Foundation
// ==========================================

// Preserve default in-memory behavior.
// Shared SQL registration may override the interface.

builder.Services.AddSingleton<
    InMemoryLiveTripSessionRepository>();

builder.Services.AddSingleton<
    ILiveTripSessionRepository>(
    sp => sp.GetRequiredService<
        InMemoryLiveTripSessionRepository>());

// ==========================================
// M3 - InMemory Invitation Foundation
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripInvitationDecisionRepository>();

builder.Services.AddSingleton<
    ITripInvitationDecisionRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripInvitationDecisionRepository>());

// ==========================================
// M3 - InMemory Membership Foundation
// ==========================================

builder.Services.AddSingleton<
    InMemoryTripMembershipRevocationRepository>();

builder.Services.AddSingleton<
    ITripMembershipRevocationRepository>(
    sp => sp.GetRequiredService<
        InMemoryTripMembershipRevocationRepository>());

// ==========================================
// M3 - Configured SQL Persistence
// ==========================================

// TripSafety:EnableSqlPersistence
//
// Default: false.
// When true, the M3 SQL registration extension
// replaces the corresponding repository interfaces.
//
// No automatic migrations.

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
