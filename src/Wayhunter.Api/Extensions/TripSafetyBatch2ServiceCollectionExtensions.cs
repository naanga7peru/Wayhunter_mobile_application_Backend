using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Services.TripSafety;
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
        var useSql = configuration.GetValue<bool>("TripSafety:EnableSqlPersistence");
        if (useSql)
        {
            services.AddScoped<ITripSafetyCheckInStore, UnavailableTripSafetyCheckInStore>();
            services.AddScoped<ITripFinishedReminderGuard, EfTripFinishedReminderGuard>();
        }
        else
        {
            services.AddSingleton<InMemoryTripSafetyCheckInStore>();
            services.AddSingleton<ITripSafetyCheckInStore>(sp => sp.GetRequiredService<InMemoryTripSafetyCheckInStore>());
            services.AddSingleton<InMemoryTripFinishedReminderGuard>();
            services.AddSingleton<ITripFinishedReminderGuard>(sp => sp.GetRequiredService<InMemoryTripFinishedReminderGuard>());
        }
        services.AddScoped<TripSafeCheckInService>();
        services.AddScoped<TripSafeSignalRNotifier>();
        services.AddScoped<TripFinishReminderFilter>();
        return services;
    }
}
