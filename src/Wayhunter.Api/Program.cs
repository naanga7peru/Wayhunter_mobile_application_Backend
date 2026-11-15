
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

var builder = WebApplication.CreateBuilder(args);

// Starter only:
// M1 must implement real authentication and Admin RBAC.
builder.Services.AddControllers();

builder.Services.AddTravelPlanningModule();

builder.Services.AddSingleton(TimeProvider.System);

// M3-MS04-T01:
// Trip sharing consent in-memory foundation.
//
// This repository is not connected to the canonical
// invitation, membership and session persistence.
//
// Production auth and SQL integration remain pending.

builder.Services.AddSingleton<
    ITripSharingConsentRepository,
    InMemoryTripSharingConsentRepository>();

builder.Services.AddScoped<TripSharingConsentService>();

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

app.Run();

// Exposed for future integration tests.
public partial class Program
{
}
