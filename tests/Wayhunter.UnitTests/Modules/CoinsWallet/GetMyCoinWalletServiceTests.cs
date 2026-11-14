
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Controllers.Mobile.CoinsWallet;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Queries;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class GetMyCoinWalletServiceTests
{
    private static ClaimsPrincipal CreateUser(
        string userId = "user-001")
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId)
            },
            "UnitTest");

        return new ClaimsPrincipal(identity);
    }

    private static CoinWalletSnapshot CreateSnapshot(
        string ownerId = "user-001")
    {
        return new CoinWalletSnapshot(
            ownerId,
            new CoinWalletBalance(
                10100,
                10000,
                12000));
    }

    [Fact]
    public async Task AnonymousUser_IsRejected()
    {
        var reader = new FakeReader(CreateSnapshot());
        var service = new GetMyCoinWalletService(reader);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMineAsync(
                new ClaimsPrincipal()));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task NullUser_IsRejected()
    {
        var reader = new FakeReader(CreateSnapshot());
        var service = new GetMyCoinWalletService(reader);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMineAsync(null));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task MissingUserId_IsRejected()
    {
        var identity = new ClaimsIdentity(
            authenticationType: "UnitTest");

        var reader = new FakeReader(CreateSnapshot());
        var service = new GetMyCoinWalletService(reader);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMineAsync(
                new ClaimsPrincipal(identity)));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Owner_CanReadOwnBalance()
    {
        var reader = new FakeReader(CreateSnapshot());
        var service = new GetMyCoinWalletService(reader);

        var result = await service.GetMineAsync(
            CreateUser());

        Assert.NotNull(result);
        Assert.Equal(10100, result.PostedCoins);
        Assert.Equal(10000, result.ReservedCoins);
        Assert.Equal(100, result.SpendableCoins);
        Assert.Equal(12000, result.LifetimeEarnedCoins);

        Assert.Equal(
            "user-001",
            reader.RequestedOwnerId);
    }

    [Fact]
    public async Task SubClaim_CanIdentifyOwner()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", "user-001")
            },
            "UnitTest");

        var reader = new FakeReader(CreateSnapshot());
        var service = new GetMyCoinWalletService(reader);

        var result = await service.GetMineAsync(
            new ClaimsPrincipal(identity));

        Assert.NotNull(result);
        Assert.Equal(
            "user-001",
            reader.RequestedOwnerId);
    }

    [Fact]
    public async Task MissingWallet_ReturnsNull()
    {
        var reader = new FakeReader(null);
        var service = new GetMyCoinWalletService(reader);

        var result = await service.GetMineAsync(
            CreateUser());

        Assert.Null(result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task WrongOwnerSnapshot_IsNeverReturned()
    {
        var reader = new FakeReader(
            CreateSnapshot("user-002"));

        var service = new GetMyCoinWalletService(reader);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetMineAsync(
                CreateUser("user-001")));
    }

    [Fact]
    public async Task Controller_Anonymous_Returns401()
    {
        var controller = CreateController(
            new FakeReader(CreateSnapshot()),
            new ClaimsPrincipal());

        var result = await controller.GetMine(
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(
            result.Result);
    }

    [Fact]
    public async Task Controller_Owner_Returns200()
    {
        var controller = CreateController(
            new FakeReader(CreateSnapshot()),
            CreateUser());

        var result = await controller.GetMine(
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(
            result.Result);

        var wallet =
            Assert.IsType<MyCoinWalletResponse>(ok.Value);

        Assert.Equal(100, wallet.SpendableCoins);
    }

    [Fact]
    public async Task Controller_MissingWallet_Returns404()
    {
        var controller = CreateController(
            new FakeReader(null),
            CreateUser());

        var result = await controller.GetMine(
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(
            result.Result);
    }

    [Fact]
    public void Controller_RequiresAuthorization()
    {
        Assert.True(
            Attribute.IsDefined(
                typeof(MyCoinWalletController),
                typeof(AuthorizeAttribute)));
    }

    private static MyCoinWalletController CreateController(
        ICoinWalletReader reader,
        ClaimsPrincipal user)
    {
        var service = new GetMyCoinWalletService(reader);

        return new MyCoinWalletController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };
    }

    // Fake repository only for unit testing.
    // No database, migration or external API used.
    private sealed class FakeReader : ICoinWalletReader
    {
        private readonly CoinWalletSnapshot? _snapshot;

        public int Calls { get; private set; }

        public string? RequestedOwnerId { get; private set; }

        public FakeReader(CoinWalletSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<CoinWalletSnapshot?> GetByOwnerUserIdAsync(
            string ownerUserId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            RequestedOwnerId = ownerUserId;

            return Task.FromResult(_snapshot);
        }
    }
}
