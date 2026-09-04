using Microsoft.EntityFrameworkCore;

using PMGIS.Domain.Rules;
using PMGIS.Infrastructure.Data;

namespace PMGIS.Api.Features.Projects.CheckProjectCode;

public sealed class CheckProjectCodeQueryHandler(PmgisDbContext db)
{
    public async Task<CodeAvailabilityResponse> HandleAsync(CheckProjectCodeQuery query, CancellationToken ct)
    {
        var wellFormed = ProjectCodeRules.IsCodeValid(query.Code);

        var taken = await db.Projects.AnyAsync(
            p => p.ProjectCode == query.Code &&
                 (query.ExcludeProjectId == null || p.Id != query.ExcludeProjectId), ct);

        return new CodeAvailabilityResponse(query.Code, wellFormed && !taken, wellFormed);
    }
}
