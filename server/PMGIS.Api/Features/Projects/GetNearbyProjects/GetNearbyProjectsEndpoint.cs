using PMGIS.Api.Common;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.GetNearbyProjects;

public static class GetNearbyProjectsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects/nearby", HandleAsync)
            .WithName("GetNearbyProjects")
            .WithTags("Projects")
            .WithSummary("Projects within a radius of a point, nearest first.")
            .WithValidation<GetNearbyProjectsQuery>()
            .Produces<IReadOnlyList<NearbyProject>>();

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetNearbyProjectsQuery query,
        GetNearbyProjectsQueryHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(query, ct));
}
