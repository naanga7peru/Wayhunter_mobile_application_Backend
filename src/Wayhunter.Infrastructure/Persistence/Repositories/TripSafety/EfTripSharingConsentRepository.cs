using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripSharingConsentRepository
    : ITripSharingConsentRepository,
      ITripLiveLocationAccessReader
{
    private readonly WayhunterSharedDbContext _dbContext;

    public EfTripSharingConsentRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<GpsConsentDto> SetMyConsentAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        bool grant,
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid trip and session are required.");
        }

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid UTC server time is required.",
                nameof(serverTimeUtc));
        }

        var userId = authenticatedUserId.Trim();
        var now = serverTimeUtc.ToUniversalTime();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var session = await _dbContext.LiveTripSessions
            .SingleOrDefaultAsync(
                x => x.SessionId == sessionId,
                cancellationToken);

        if (session is null)
        {
            throw new KeyNotFoundException(
                "Live trip session was not found.");
        }

        if (session.TripPlanId != tripPlanId)
        {
            throw new UnauthorizedAccessException(
                "Session belongs to another trip.");
        }

        if (session.Status == LiveTripStatus.Finished)
        {
            throw new InvalidOperationException(
                "Finished trips cannot change GPS consent.");
        }

        var member = await _dbContext.TripMembers
            .SingleOrDefaultAsync(
                x =>
                    x.TripPlanId == tripPlanId &&
                    x.MemberUserId == userId &&
                    x.IsActive,
                cancellationToken);

        if (member is null ||
            member.TripPlanId != tripPlanId ||
            !string.Equals(
                member.MemberUserId,
                userId,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Active accepted membership is required.");
        }

        if (now < member.JoinedAtUtc ||
            now < session.StartedAtUtc)
        {
            throw new InvalidOperationException(
                "Consent time cannot precede joining or trip start.");
        }

        var consent = await _dbContext.TripGpsConsents
            .SingleOrDefaultAsync(
                x =>
                    x.SessionId == sessionId &&
                    x.UserId == userId,
                cancellationToken);

        if (consent is null)
        {
            consent = TripGpsConsent.CreateDisabled(
                sessionId,
                userId);

            _dbContext.TripGpsConsents.Add(consent);
        }

        if (grant)
        {
            consent.Grant(now);
        }
        else
        {
            consent.Revoke(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return GpsConsentDto.FromDomain(consent);
    }

    public async Task EnsureAllowedAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip and session IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        var userId = authenticatedUserId.Trim();

        var session = await _dbContext.LiveTripSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SessionId == sessionId,
                cancellationToken);

        var member = await _dbContext.TripMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.TripPlanId == tripPlanId &&
                    x.MemberUserId == userId &&
                    x.IsActive,
                cancellationToken);

        var consent = await _dbContext.TripGpsConsents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.SessionId == sessionId &&
                    x.UserId == userId,
                cancellationToken);

        var authorized =
            member is not null &&
            member.IsActive &&
            member.TripPlanId == tripPlanId &&
            string.Equals(
                member.MemberUserId,
                userId,
                StringComparison.Ordinal);

        GroupLiveLocationPublishValidator.EnsureCanPublish(
            session,
            consent,
            tripPlanId,
            userId,
            authorized);
    }
}
