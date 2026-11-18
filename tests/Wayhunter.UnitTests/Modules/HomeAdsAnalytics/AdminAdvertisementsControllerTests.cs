
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdminAdvertisementsControllerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeRepository :
        IAdvertisementRepository,
        IAdvertisementWriteRepository
    {
        public AdvertisementDto? Advertisement { get; set; }

        public bool ThrowConflict { get; set; }

        public int WriteCount { get; private set; }

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> result =
                Advertisement is null
                    ? []
                    : [Advertisement];

            return Task.FromResult(result);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Advertisement?.Id == id
                    ? Advertisement
                    : null);
        }

        public Task<AdvertisementDto> CreateAsync(
            AdvertisementCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;

            Advertisement = new AdvertisementDto
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                ImageUrl = request.ImageUrl,
                DestinationUrl = request.DestinationUrl,
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                Placement = request.Placement,
                Status = AdvertisementStatus.Draft,
                IsEnabled = false
            };

            return Task.FromResult(Advertisement);
        }

        public Task<AdvertisementDto?> UpdateAsync(
            Guid id,
            AdvertisementUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;

            if (ThrowConflict)
            {
                throw new InvalidOperationException(
                    "Archived advertisements cannot be edited.");
            }

            if (Advertisement?.Id != id)
            {
                return Task.FromResult<AdvertisementDto?>(null);
            }

            Advertisement = Advertisement with
            {
                Title = request.Title,
                ImageUrl = request.ImageUrl,
                DestinationUrl = request.DestinationUrl,
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                Placement = request.Placement
            };

            // Fixed nullable return type
            return Task.FromResult<AdvertisementDto?>(Advertisement);
        }

        public Task<AdvertisementDto?> ChangeStatusAsync(
            Guid id,
            AdvertisementStatusUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;

            if (ThrowConflict)
            {
                throw new InvalidOperationException(
                    "Archived advertisements cannot be activated.");
            }

            if (Advertisement?.Id != id)
            {
                return Task.FromResult<AdvertisementDto?>(null);
            }

            Advertisement = Advertisement with
            {
                Status = request.Status!.Value,
                IsEnabled = request.Status ==
                    AdvertisementStatus.Active
            };

            // Fixed nullable return type
            return Task.FromResult<AdvertisementDto?>(Advertisement);
        }
    }

    private static AdvertisementsController CreateController(
        FakeRepository repository,
        bool authenticated = true,
        bool admin = true)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString("D"))
        };

        if (admin)
        {
            claims.Add(
                new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(
            claims,
            authenticated ? "TestAuthentication" : null);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Identity:AdministratorRole"] = "Admin"
                })
            .Build();

        var controller = new AdvertisementsController(
            repository,
            repository,
            configuration);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    private static AdvertisementCreateRequest ValidCreate() => new()
    {
        Title = "Home Advertisement",
        ImageUrl = "https://example.com/ad.jpg",
        DestinationUrl = "https://example.com",
        StartAtUtc = Start,
        EndAtUtc = Start.AddDays(5),
        Placement = AdvertisementPlacement.HomeBanner
    };

    private static AdvertisementUpdateRequest ValidUpdate() => new()
    {
        Title = "Updated Advertisement",
        ImageUrl = "https://example.com/updated.jpg",
        StartAtUtc = Start,
        EndAtUtc = Start.AddDays(5),
        Placement = AdvertisementPlacement.SponsoredPoi
    };

    private static AdvertisementDto ExistingAd() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Existing Advertisement",
        ImageUrl = "https://example.com/ad.jpg",
        StartAtUtc = Start,
        EndAtUtc = Start.AddDays(5),
        Placement = AdvertisementPlacement.HomeBanner,
        Status = AdvertisementStatus.Draft
    };

    [Fact]
    public async Task Admin_Create_Returns201()
    {
        var repository = new FakeRepository();
        var controller = CreateController(repository);

        var response = await controller.Create(
            ValidCreate(),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(
            response);

        var advertisement =
            Assert.IsType<AdvertisementDto>(created.Value);

        Assert.Equal(
            AdvertisementStatus.Draft,
            advertisement.Status);

        Assert.Equal(1, repository.WriteCount);
    }

    [Fact]
    public async Task NormalUser_Create_Returns403()
    {
        var repository = new FakeRepository();

        var controller = CreateController(
            repository,
            admin: false);

        var response = await controller.Create(
            ValidCreate(),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task AnonymousUser_Create_IsRejected()
    {
        var repository = new FakeRepository();

        var controller = CreateController(
            repository,
            authenticated: false);

        var response = await controller.Create(
            ValidCreate(),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task InvalidCreate_Returns400()
    {
        var repository = new FakeRepository();
        var controller = CreateController(repository);

        var request = ValidCreate() with
        {
            Title = " "
        };

        var response = await controller.Create(
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task Admin_GetAll_Returns200()
    {
        var repository = new FakeRepository
        {
            Advertisement = ExistingAd()
        };

        var controller = CreateController(repository);

        var response = await controller.GetAll(
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
    }

    [Fact]
    public async Task MissingAdvertisement_GetById_Returns404()
    {
        var controller = CreateController(
            new FakeRepository());

        var response = await controller.GetById(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
    }

    [Fact]
    public async Task InvalidId_GetById_Returns400()
    {
        var controller = CreateController(
            new FakeRepository());

        var response = await controller.GetById(
            Guid.Empty,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task Admin_Update_Returns200()
    {
        var advertisement = ExistingAd();

        var repository = new FakeRepository
        {
            Advertisement = advertisement
        };

        var controller = CreateController(repository);

        var response = await controller.Update(
            advertisement.Id,
            ValidUpdate(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(1, repository.WriteCount);
    }

    [Fact]
    public async Task MissingAdvertisement_Update_Returns404()
    {
        var controller = CreateController(
            new FakeRepository());

        var response = await controller.Update(
            Guid.NewGuid(),
            ValidUpdate(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
    }

    [Fact]
    public async Task ArchivedAdvertisement_Update_Returns409()
    {
        var repository = new FakeRepository
        {
            Advertisement = ExistingAd(),
            ThrowConflict = true
        };

        var controller = CreateController(repository);

        var response = await controller.Update(
            repository.Advertisement!.Id,
            ValidUpdate(),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response);
    }

    [Fact]
    public async Task Admin_ChangeStatus_Returns200()
    {
        var advertisement = ExistingAd();

        var repository = new FakeRepository
        {
            Advertisement = advertisement
        };

        var controller = CreateController(repository);

        var response = await controller.ChangeStatus(
            advertisement.Id,
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Active
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);

        var updated =
            Assert.IsType<AdvertisementDto>(ok.Value);

        Assert.Equal(
            AdvertisementStatus.Active,
            updated.Status);
    }

    [Fact]
    public async Task InvalidStatus_Returns400()
    {
        var repository = new FakeRepository();
        var controller = CreateController(repository);

        var response = await controller.ChangeStatus(
            Guid.NewGuid(),
            new AdvertisementStatusUpdateRequest
            {
                Status = (AdvertisementStatus)999
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task ArchivedAdvertisement_Activate_Returns409()
    {
        var repository = new FakeRepository
        {
            Advertisement = ExistingAd(),
            ThrowConflict = true
        };

        var controller = CreateController(repository);

        var response = await controller.ChangeStatus(
            repository.Advertisement!.Id,
            new AdvertisementStatusUpdateRequest
            {
                Status = AdvertisementStatus.Active
            },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response);
    }
}
