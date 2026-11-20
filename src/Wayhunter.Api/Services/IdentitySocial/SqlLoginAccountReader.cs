using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SqlLoginAccountReader : ILoginAccountReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlLoginAccountReader(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<UserAccount?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        _db.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);
}
