
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// Provider-neutral contract for map availability.
// Actual interactive map rendering belongs to Flutter.
public interface IMapsProvider
{
    Task<MapProviderStatus> CheckStatusAsync(
        CancellationToken cancellationToken = default);
}

// Contains no API keys or private configuration.
public sealed record MapProviderStatus(
    string ProviderName,
    bool IsConfigured,
    bool IsAvailable,
    string? FailureReason
);
