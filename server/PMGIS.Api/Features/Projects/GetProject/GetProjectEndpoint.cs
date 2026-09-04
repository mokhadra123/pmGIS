using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Projects.GetProject;

public static class GetProjectEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects/{id:int}", HandleAsync)
            .WithName("GetProject")
            .WithTags("Projects")
            .WithSummary("One project with its activities and calculated progress.")
            .WithValidation<GetProjectQuery>()
            .Produces<ProjectDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetProjectQuery query,
        GetProjectQueryHandler handler,
        CancellationToken ct)
    {
        var project = await handler.HandleAsync(query, ct);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }
}
