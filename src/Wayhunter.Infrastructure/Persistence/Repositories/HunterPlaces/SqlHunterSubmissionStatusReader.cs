using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

/// <summary>
/// SQL-backed, read-only Hunter submission projection.
/// Not registered for runtime use until M4 identity/persistence approval.
/// </summary>
public sealed class SqlHunterSubmissionStatusReader
    : IHunterSubmissionStatusReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterSubmissionStatusReader(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (submissionId == Guid.Empty)
        {
            return null;
        }

        return await _db.HunterSubmissions
            .AsNoTracking()
            .Where(x => x.Id == submissionId)
            .Select(x => new HunterSubmissionStatusSnapshot
            {
                SubmissionId = x.Id,
                OwnerUserId = x.UserId,
                PlaceName = x.PlaceName,
                District = x.District,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                SubmittedAtUtc = x.SubmittedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
