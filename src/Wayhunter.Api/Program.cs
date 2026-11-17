
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

// Use SQL persistence only when the feature flag is enabled.
// Otherwise, retain the default InMemory repository.
builder.Services.AddConfiguredTravelPlanningPersistence(
    builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

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
