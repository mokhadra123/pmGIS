using Microsoft.EntityFrameworkCore;
using PMGIS.Api.Features.Lookups.Shared;
using PMGIS.Infrastructure.Data;

namespace PMGIS.Api.Features.Lookups.GetProjectTypes;

public sealed class GetProjectTypesQueryHandler(PmgisDbContext db)
{
    public async Task<IReadOnlyList<LookupItem>> HandleAsync(GetProjectTypesQuery query, CancellationToken ct) =>
        await db.ProjectTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new LookupItem(t.Id, t.Code, t.Name))
            .ToListAsync(ct);
}
