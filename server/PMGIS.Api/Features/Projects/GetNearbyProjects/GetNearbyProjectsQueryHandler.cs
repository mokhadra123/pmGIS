using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.GetNearbyProjects;

public sealed class GetNearbyProjectsQueryHandler(PmgisDbContext db)
{
    public Task<IReadOnlyList<NearbyProject>> HandleAsync(GetNearbyProjectsQuery query, CancellationToken ct) =>
        ProjectQueries.NearbyAsync(db, query.Latitude, query.Longitude, query.RadiusKm, query.EffectiveLimit, ct);
}
