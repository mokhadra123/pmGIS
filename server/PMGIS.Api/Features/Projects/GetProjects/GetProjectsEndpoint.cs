using PMGIS.Api.Common;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.GetProjects;

public static class GetProjectsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects", HandleAsync)
            .WithName("GetProjects")
            .WithTags("Projects")
            .WithSummary("Filtered, sorted, paged Projects List.")
            .WithValidation<GetProjectsQuery>()
            .Produces<PagedResult<ProjectListItem>>();

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetProjectsQuery query,
        GetProjectsQueryHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(query, ct));
}
