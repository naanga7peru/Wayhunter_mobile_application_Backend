
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
/// SQL-backed reader for trusted Hunter submission data.
/// Reads ownership and status from backend persistence.
/// Does not accept client-provided owner information.
/// </summary>
public sealed class SqlHunterCaptureSubmissionAccessReader
    : IHunterCaptureSubmissionAccessReader
{
    private readonly WayhunterSharedDbContext _dbContext;

    public SqlHunterCaptureSubmissionAccessReader(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task<HunterCaptureSubmissionAccessSnapshot?>
        FindBySubmissionIdAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext
            .Set<HunterSubmission>()
            .AsNoTracking()
            .Where(x => x.Id == submissionId)
            .Select(x =>
                new HunterCaptureSubmissionAccessSnapshot
                {
                    SubmissionId = x.Id,
                    OwnerUserId = x.UserId,
                    Status = x.Status
                })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
