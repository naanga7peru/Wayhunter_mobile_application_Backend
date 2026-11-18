
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Wayhunter.Api.Controllers.Mobile.TravelPlanning;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TripPlansControllerTests
{
    private static readonly byte[] TestRowVersion =
    {
        1, 2, 3, 4, 5, 6, 7, 8
    };

    private static CreateTripPlanDto CreateRequest()
    {
        return new CreateTripPlanDto(
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);
    }

    private static TripPlan CreateTrip()
    {
        var trip = new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);

        SetRowVersion(trip, TestRowVersion);

        return trip;
    }

    private static void SetRowVersion(
        TripPlan trip,
        byte[] rowVersion)
    {
        var property = typeof(TripPlan).GetProperty(
            nameof(TripPlan.RowVersion),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        property.SetValue(
            trip,
            rowVersion.ToArray());
    }

    private static TripPlansController CreateController(
        FakeRepository repository,
        string? userId = "user-001")
    {
        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId)
                },
                "TestAuthentication");

        var controller = new TripPlansController(
            new CreateTripPlanService(repository),
            new GetTripPlanService(repository),
            new UpdateTripPlanService(repository),
            new CancelTripPlanService(repository));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    // EXISTING TEST 1
    [Fact]
    public async Task Create_ValidUser_ReturnsCreated()
    {
        var repository = new FakeRepository();
        var controller = CreateController(repository);

        var response = await controller.Create(
            CreateRequest(),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(
            response.Result);

        Assert.Equal(201, created.StatusCode);

        var trip = Assert.IsType<TripPlanResponseDto>(
            created.Value);

        Assert.Equal("Jaffna Tour", trip.TripName);

        var saved = await repository.GetByIdAsync(
            trip.TripPlanId);

        Assert.NotNull(saved);
        Assert.Equal("user-001", saved.OwnerUserId);
    }

    // EXISTING TEST 2
    [Fact]
    public async Task Create_NoUser_ReturnsUnauthorized()
    {
        var repository = new FakeRepository();

        var controller = CreateController(
            repository,
            userId: null);

        var response = await controller.Create(
            CreateRequest(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            response.Result);
    }

    // EXISTING TEST 3
    [Fact]
    public async Task Get_ValidOwner_ReturnsOk()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(repository);

        var response = await controller.GetById(
            trip.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var result = Assert.IsType<TripPlanResponseDto>(
            ok.Value);

        Assert.Equal(trip.Id, result.TripPlanId);
        Assert.Equal(TestRowVersion, result.RowVersion);
    }

    // EXISTING TEST 4
    [Fact]
    public async Task Get_DifferentOwner_ReturnsNotFound()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(
            repository,
            "user-002");

        var response = await controller.GetById(
            trip.Id,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(
            response.Result);
    }

    // EXISTING TEST 5
    [Fact]
    public async Task Update_ValidOwner_ReturnsOk()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(repository);

        var request = new UpdateTripPlanDto(
            "Updated Trip",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 24),
            20000m,
            "Bus",
            4)
        {
            ExpectedRowVersion = TestRowVersion.ToArray()
        };

        var response = await controller.Update(
            trip.Id,
            request,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var result = Assert.IsType<TripPlanResponseDto>(
            ok.Value);

        Assert.Equal("Updated Trip", result.TripName);
        Assert.Equal(20000m, result.BudgetLkr);
        Assert.Equal("Bus", result.VehicleType);
        Assert.Equal(4, result.Headcount);
        Assert.Equal(TestRowVersion, result.RowVersion);
    }

    // EXISTING TEST 6
    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(repository);

        var staleRowVersion = new byte[]
        {
            8, 7, 6, 5, 4, 3, 2, 1
        };

        var request = new UpdateTripPlanDto(
            "Updated Trip",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 24),
            20000m,
            "Bus",
            4)
        {
            ExpectedRowVersion = staleRowVersion
        };

        var response = await controller.Update(
            trip.Id,
            request,
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(
            response.Result);

        Assert.Equal(409, conflict.StatusCode);

        Assert.Equal("Jaffna Tour", trip.TripName);
        Assert.Equal(15000m, trip.BudgetLkr);
        Assert.Equal(TestRowVersion, trip.RowVersion);
    }

    // EXISTING TEST 7
    [Fact]
    public async Task Cancel_ValidOwner_ReturnsNoContent()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(repository);

        var response = await controller.Cancel(
            trip.Id,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(response);

        Assert.Equal(
            TripDraftStatus.Cancelled,
            trip.DraftStatus);
    }

    // EXISTING TEST 8
    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsConflict()
    {
        var repository = new FakeRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var controller = CreateController(repository);

        await controller.Cancel(
            trip.Id,
            CancellationToken.None);

        var response = await controller.Cancel(
            trip.Id,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response);
    }

    // NEW TEST 9
    [Fact]
    public async Task GetMyPlans_NoUser_ReturnsUnauthorized()
    {
        var repository = new FakeRepository();
        var controller = CreateController(
            repository,
            userId: null);

        var response = await controller.GetMyPlans(
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            response.Result);
    }

    // NEW TEST 10
    [Fact]
    public async Task GetMyPlans_EmptyRepository_ReturnsOkEmptyList()
    {
        var repository = new FakeRepository();
        var controller = CreateController(repository);

        var response = await controller.GetMyPlans(
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var plans = Assert.IsAssignableFrom<
            IReadOnlyList<TripPlanResponseDto>>(ok.Value);

        Assert.Empty(plans);
    }

    // NEW TEST 11
    [Fact]
    public async Task GetMyPlans_ReturnsOnlyAuthenticatedUsersTrips()
    {
        var repository = new FakeRepository();

        var ownTrip = CreateTrip();

        var otherTrip = new TripPlan(
            "user-002",
            "Other User Trip",
            new DateOnly(2026, 11, 23),
            new DateOnly(2026, 11, 25),
            10000m,
            "Bus",
            2);

        await repository.AddAsync(ownTrip);
        await repository.AddAsync(otherTrip);

        var controller = CreateController(
            repository,
            "user-001");

        var response = await controller.GetMyPlans(
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var plans = Assert.IsAssignableFrom<
            IReadOnlyList<TripPlanResponseDto>>(ok.Value);

        var plan = Assert.Single(plans);

        Assert.Equal(ownTrip.Id, plan.TripPlanId);
        Assert.Equal("Jaffna Tour", plan.TripName);
        Assert.Equal(TestRowVersion, plan.RowVersion);

        Assert.DoesNotContain(
            plans,
            p => p.TripPlanId == otherTrip.Id);
    }

    // NEW TEST 12
    [Fact]
    public async Task GetMyPlans_ReturnsLatestTripFirst()
    {
        var repository = new FakeRepository();

        var olderTrip = CreateTrip();

        var newerTrip = new TripPlan(
            "user-001",
            "December Tour",
            new DateOnly(2026, 12, 10),
            new DateOnly(2026, 12, 12),
            20000m,
            "Car",
            4);

        await repository.AddAsync(olderTrip);
        await repository.AddAsync(newerTrip);

        var controller = CreateController(repository);

        var response = await controller.GetMyPlans(
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            response.Result);

        var plans = Assert.IsAssignableFrom<
            IReadOnlyList<TripPlanResponseDto>>(ok.Value);

        Assert.Equal(2, plans.Count);
        Assert.Equal(newerTrip.Id, plans[0].TripPlanId);
        Assert.Equal(olderTrip.Id, plans[1].TripPlanId);
    }

    private sealed class FakeRepository
        : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _plans = new();

        public Task<TripPlan?> GetByIdAsync(
            Guid tripPlanId,
            CancellationToken cancellationToken = default)
        {
            _plans.TryGetValue(tripPlanId, out var trip);
            return Task.FromResult(trip);
        }

        public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
            string ownerUserId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TripPlan> result = _plans.Values
                .Where(p => p.OwnerUserId == ownerUserId)
                .ToList();

            return Task.FromResult(result);
        }

        public Task AddAsync(
            TripPlan tripPlan,
            CancellationToken cancellationToken = default)
        {
            _plans.Add(tripPlan.Id, tripPlan);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TripPlan tripPlan,
            CancellationToken cancellationToken = default)
        {
            _plans[tripPlan.Id] = tripPlan;
            return Task.CompletedTask;
        }
    }
}
