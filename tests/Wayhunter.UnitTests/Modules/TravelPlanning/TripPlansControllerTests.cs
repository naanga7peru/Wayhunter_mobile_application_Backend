
using System;
using System.Collections.Generic;
using System.Linq;
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
    private static CreateTripPlanDto CreateRequest()
    {
        return new CreateTripPlanDto(
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
    }

    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
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
    }

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
            4
        );

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
    }

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

        Assert.IsType<ConflictObjectResult>(
            response);
    }

    private sealed class FakeRepository
        : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan>
            _plans = new();

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
            IReadOnlyList<TripPlan> result =
                _plans.Values
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
