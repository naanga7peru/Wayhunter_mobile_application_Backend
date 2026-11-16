using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

var builder = WebApplication.CreateBuilder(args);

// Authentication and Admin RBAC integration
// are owned by M1.
// Do not treat this starter as production auth.

builder.Services.AddControllers();

builder.Services.AddTravelPlanningModule();

builder.Services.AddSingleton(TimeProvider.System);

// M3-MS04-T01 + T02 shared in-memory state.
//
// The same instance backs both:
// - Explicit GPS consent changes
// - SignalR authorization reads
//
// Production persistence integration is pending.

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

// SignalR infrastructure.

builder.Services.AddSignalR();

builder.Services.AddSingleton<
    TripLocationSubscriberRegistry>();

builder.Services.AddScoped<
    TripLocationFanoutService>();

// Development CORS configuration.

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

var app = builder.Build();

app.UseMiddleware<
    Wayhunter.Api.Middleware.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalDevelopment");
}

app.MapControllers();

// [Authorize] requires the real M1
// authentication scheme before live use.

app.MapHub<TripLocationHub>(
    "/hubs/trip-location");

app.Run();

public partial class Program
{
}
