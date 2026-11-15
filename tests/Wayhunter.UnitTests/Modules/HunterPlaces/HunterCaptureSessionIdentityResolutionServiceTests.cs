
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSessionIdentityResolutionServiceTests
{
    private sealed class FakeIdentityResolver
        : IHunterCaptureSessionIdentityResolver
    {
        public Guid? ResolvedHunterId { get; set; } = Guid.NewGuid();

        public int ResolveCount { get; private set; }

        public string? LastUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<Guid?> ResolveSessionHunterIdAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            LastUserId = authenticatedUserId;
            LastToken = cancellationToken;

            return Task.FromResult(ResolvedHunterId);
        }
    }

    private static (
        HunterCaptureSessionIdentityResolutionService Service,
        FakeIdentityResolver Resolver)
        CreateService()
    {
        var resolver = new FakeIdentityResolver();

        var service =
            new HunterCaptureSessionIdentityResolutionService(
                resolver);

        return (service, resolver);
    }

    [Fact]
    public void NullResolver_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterCaptureSessionIdentityResolutionService(
                null!));
    }

    [Fact]
    public async Task ValidMapping_ShouldReturnHunterId()
    {
        var (service, resolver) = CreateService();

        var expectedHunterId = Guid.NewGuid();
        resolver.ResolvedHunterId = expectedHunterId;

        var result = await service.ResolveRequiredAsync(
            "hunter-001");

        Assert.Equal(expectedHunterId, result);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal("hunter-001", resolver.LastUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedUserId_ShouldDeny(
        string? authenticatedUserId)
    {
        var (service, resolver) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResolveRequiredAsync(
                authenticatedUserId));

        Assert.Equal(0, resolver.ResolveCount);
    }

    [Fact]
    public async Task MissingMapping_ShouldDeny()
    {
        var (service, resolver) = CreateService();

        resolver.ResolvedHunterId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResolveRequiredAsync(
                "hunter-001"));

        Assert.Equal(1, resolver.ResolveCount);
    }

    [Fact]
    public async Task EmptyGuidMapping_ShouldDeny()
    {
        var (service, resolver) = CreateService();

        resolver.ResolvedHunterId = Guid.Empty;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResolveRequiredAsync(
                "hunter-001"));

        Assert.Equal(1, resolver.ResolveCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotResolve()
    {
        var (service, resolver) = CreateService();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ResolveRequiredAsync(
                "hunter-001",
                cts.Token));

        Assert.Equal(0, resolver.ResolveCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var (service, resolver) = CreateService();

        using var cts = new CancellationTokenSource();

        await service.ResolveRequiredAsync(
            "hunter-001",
            cts.Token);

        Assert.Equal(cts.Token, resolver.LastToken);
        Assert.Equal(1, resolver.ResolveCount);
    }

    [Fact]
    public async Task UserId_ShouldBeForwardedUnchanged()
    {
        var (service, resolver) = CreateService();

        var authenticatedUserId =
            Guid.NewGuid().ToString("D");

        await service.ResolveRequiredAsync(
            authenticatedUserId);

        Assert.Equal(
            authenticatedUserId,
            resolver.LastUserId);

        Assert.Equal(1, resolver.ResolveCount);
    }
}
