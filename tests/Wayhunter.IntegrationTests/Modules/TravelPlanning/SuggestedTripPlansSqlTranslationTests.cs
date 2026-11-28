using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TravelPlanning;

public sealed class SuggestedTripPlansSqlTranslationTests
{
    [Fact]
    public void EligibleSummaryProjection_TranslatesToSqlServerWithoutOpeningConnection()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=translation_only;User Id=none;Password=none;TrustServerCertificate=True")
            .Options;
        using var db = new WayhunterSharedDbContext(options);
        var reader = new SqlSuggestedTripPlansReader(db);
        var method = typeof(SqlSuggestedTripPlansReader).GetMethod(
            "EligibleTrips", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var trips = Assert.IsAssignableFrom<IQueryable<TripPlan>>(method.Invoke(reader, ["translation-test-user"]));
        var sql = trips.Select(trip => new
            {
                TripPlanId = trip.Id,
                trip.TripName,
                trip.StartDate,
                trip.EndDate,
                trip.BudgetLkr,
                trip.VehicleType,
                trip.Headcount,
                StopCount = db.TripStops.Count(stop => stop.TripPlanId == trip.Id),
                CompletedAtUtc = db.LiveTripSessions
                    .Where(session => session.TripPlanId == trip.Id &&
                                      session.Status == LiveTripStatus.Finished &&
                                      session.FinishedAtUtc.HasValue &&
                                      session.FinishedAtUtc.Value >= session.StartedAtUtc)
                    .Select(session => session.FinishedAtUtc)
                    .Max()
            })
            .OrderByDescending(plan => plan.CompletedAtUtc)
            .ThenBy(plan => plan.TripPlanId)
            .Take(5)
            .ToQueryString();
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TOP(@p)", sql.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

}
