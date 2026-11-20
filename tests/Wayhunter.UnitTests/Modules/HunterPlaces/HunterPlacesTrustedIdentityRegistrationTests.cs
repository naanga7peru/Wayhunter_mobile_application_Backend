
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterPlacesTrustedIdentityRegistrationTests
{
    [Fact]
    public void RegistersTrustedIdentityAccountReaderAdapter()
    {
        var services = new ServiceCollection();

        services.AddHunterPlacesTrustedIdentity();

        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(ITrustedIdentityAccountReader));

        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);

        Assert.Equal(
            typeof(AuthenticatedSessionAccountReaderAdapter),
            registration.ImplementationType);
    }

    [Fact]
    public void RegistersTrustedHunterIdentityResolver()
    {
        var services = new ServiceCollection();

        services.AddHunterPlacesTrustedIdentity();

        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionIdentityResolver));

        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);

        Assert.Equal(
            typeof(TrustedHunterCaptureSessionIdentityResolver),
            registration.ImplementationType);
    }

    [Fact]
    public void ResolvesServicesUsingExistingM1Reader()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IAuthenticatedSessionReader,
            FakeAuthenticatedSessionReader>();

        services.AddHunterPlacesTrustedIdentity();

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        var accountReader = scope.ServiceProvider
            .GetRequiredService<ITrustedIdentityAccountReader>();

        var resolver = scope.ServiceProvider
            .GetRequiredService<IHunterCaptureSessionIdentityResolver>();

        Assert.IsType<
            AuthenticatedSessionAccountReaderAdapter>(
            accountReader);

        Assert.IsType<
            TrustedHunterCaptureSessionIdentityResolver>(
            resolver);
    }

    private sealed class FakeAuthenticatedSessionReader
        : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<UserAccount?>(null);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IdentitySession?>(null);
        }
    }
}
