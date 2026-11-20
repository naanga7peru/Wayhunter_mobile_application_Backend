using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
namespace Wayhunter.Api.Extensions;
public static class TripSafetyBatch2ServiceCollectionExtensions
{
    public static IServiceCollection AddTripSafetyBatch2Services(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.RemoveAll<ITripSafetyCheckInStore>();
        services.AddScoped<ITripSafetyCheckInStore, UnavailableTripSafetyCheckInStore>();
        services.RemoveAll<ITripFinishedReminderGuard>();
        if (configuration.GetValue<bool>("TripSafety:EnableSqlPersistence"))
            services.AddScoped<ITripFinishedReminderGuard, EfTripFinishedReminderGuard>();
        else
            services.AddScoped<ITripFinishedReminderGuard, UnavailableTripFinishedReminderGuard>();
        services.AddScoped<TripSafeCheckInService>();
        return services;
    }
}
public sealed class UnavailableTripFinishedReminderGuard : ITripFinishedReminderGuard
{
    public Task<bool> CanDispatchAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Durable trip finish reminder guard is unavailable.");
    }
}
