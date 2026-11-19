
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfHazardReportRepository
    : IHazardReportRepository
{
    private readonly WayhunterSharedDbContext _context;

    public EfHazardReportRepository(
        WayhunterSharedDbContext context)
    {
        _context = context ??
            throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hazard);

        await _context.HazardReports.AddAsync(
            hazard,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<HazardReport?> GetByIdAsync(
        Guid hazardId,
        CancellationToken cancellationToken = default)
    {
        return _context.HazardReports
            .AsTracking()
            .SingleOrDefaultAsync(
                x => x.Id == hazardId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<HazardReport>>
        GetActiveAtAsync(
            DateTimeOffset serverTimeUtc,
            CancellationToken cancellationToken = default)
    {
        var now = serverTimeUtc.ToUniversalTime();

        return await _context.HazardReports
            .AsNoTracking()
            .Where(x =>
                (x.Status == HazardStatus.Reported ||
                 x.Status == HazardStatus.Verified) &&
                x.ReportedAtUtc <= now &&
                x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.ReportedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HazardReport>>
        GetDueForExpiryAsync(
            DateTimeOffset serverTimeUtc,
            int maxResults,
            CancellationToken cancellationToken = default)
    {
        if (maxResults < 1 || maxResults > 1000)
        {
            throw new ArgumentException(
                "Batch size must be between 1 and 1000.");
        }

        var now = serverTimeUtc.ToUniversalTime();

        return await _context.HazardReports
            .AsTracking()
            .Where(x =>
                (x.Status == HazardStatus.Reported ||
                 x.Status == HazardStatus.Verified) &&
                x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hazard);

        // Lifecycle updates must use a tracked entity so
        // EF can enforce the original Status concurrency token.
        if (_context.Entry(hazard).State ==
            EntityState.Detached)
        {
            throw new InvalidOperationException(
                "Hazard must be loaded by this SQL context.");
        }

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Hazard was changed by another request.");
        }
    }
}
