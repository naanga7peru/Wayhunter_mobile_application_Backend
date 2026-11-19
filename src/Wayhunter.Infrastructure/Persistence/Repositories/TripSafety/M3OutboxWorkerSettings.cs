
using System;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class M3OutboxWorkerSettings
{
    public int MaxAttempts { get; set; } = 6;

    public int BaseRetrySeconds { get; set; } = 30;

    public int MaxRetrySeconds { get; set; } = 3600;

    public int LeaseSeconds { get; set; } = 120;

    public int PollSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 20;

    public void Validate()
    {
        if (MaxAttempts < 1 || MaxAttempts > 100)
        {
            throw new InvalidOperationException(
                "M3 MaxAttempts must be between 1 and 100.");
        }

        if (BaseRetrySeconds < 1 ||
            MaxRetrySeconds < BaseRetrySeconds ||
            MaxRetrySeconds > 86400)
        {
            throw new InvalidOperationException(
                "Invalid M3 retry backoff configuration.");
        }

        if (LeaseSeconds < 60 || LeaseSeconds > 3600)
        {
            throw new InvalidOperationException(
                "M3 lease must be between 60 and 3600 seconds.");
        }

        if (PollSeconds < 5 || PollSeconds > 3600)
        {
            throw new InvalidOperationException(
                "M3 poll interval must be between 5 and 3600 seconds.");
        }

        if (BatchSize < 1 || BatchSize > 100)
        {
            throw new InvalidOperationException(
                "M3 batch size must be between 1 and 100.");
        }
    }
}

public static class M3OutboxRetryPolicy
{
    public static TimeSpan CalculateDelay(
        int deliveryAttempts,
        int baseSeconds,
        int maxSeconds)
    {
        if (deliveryAttempts < 1 ||
            baseSeconds < 1 ||
            maxSeconds < baseSeconds)
        {
            throw new ArgumentException(
                "Invalid retry calculation arguments.");
        }

        // 30, 60, 120, 240 ... capped.
        // This avoids long-running Math.Pow overflow.
        var delay = (long)baseSeconds;

        for (var attempt = 1; attempt < deliveryAttempts; attempt++)
        {
            delay = Math.Min(
                delay * 2,
                (long)maxSeconds);
        }

        return TimeSpan.FromSeconds(
            Math.Min(delay, (long)maxSeconds));
    }
}
