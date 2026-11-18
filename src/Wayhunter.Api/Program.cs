
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;

using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// API Controllers
// ==========================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ==========================================
// Team Leader - Swagger / OpenAPI
// ==========================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WAYHUNTER Backend API",
        Version = "v1",
        Description =
            "WAYHUNTER mobile application backend. " +
            "API documentation for M1-M7 modules."
    });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Enter the JWT access token. " +
                "Do not include the Bearer prefix."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer", document)] = []
        });
});

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

    // ==========================================
    // M6 - SQL Reward Writer (Opt-In)
    // ==========================================

    if (builder.Configuration.GetValue<bool>(
        "CoinsWallet:EnableSqlRewardWriter"))
    {
        builder.Services.AddCoinsWalletSqlRewardWriter(
            builder.Configuration);
    }

    // ==========================================
    // M7 - Advertisement SQL Runtime
    // ==========================================

    builder.Services.AddHomeAdsAnalyticsSqlPersistence(
        builder.Configuration);
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
// M3 - Configured SQL Persistence
// ==========================================

builder.Services.AddConfiguredTripSafetyPersistence(
    builder.Configuration);

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

// ==========================================
// Swagger - Development Only
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "WAYHUNTER Backend API v1");

        options.DocumentTitle =
            "WAYHUNTER API Documentation";
    });

    app.UseCors("LocalDevelopment");
}

// ==========================================
// Authentication / Authorization
// ==========================================

app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// API Routes
// ==========================================

app.MapControllers();

app.MapHub<TripLocationHub>(
    "/hubs/trip-location");

app.Run();

public partial class Program
{
}
