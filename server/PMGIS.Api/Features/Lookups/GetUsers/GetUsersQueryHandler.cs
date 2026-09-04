using Microsoft.EntityFrameworkCore;

using PMGIS.Infrastructure.Data;

namespace PMGIS.Api.Features.Lookups.GetUsers;

public sealed class GetUsersQueryHandler(PmgisDbContext db)
{
    public async Task<IReadOnlyList<UserLookupItem>> HandleAsync(GetUsersQuery query, CancellationToken ct) =>
        await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new UserLookupItem(u.Id, u.Name, u.Email))
            .ToListAsync(ct);
}
