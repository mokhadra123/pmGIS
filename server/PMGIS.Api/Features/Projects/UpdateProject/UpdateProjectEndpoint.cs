using PMGIS.Api.Common;
using PMGIS.Api.Features.Projects.Shared;

namespace PMGIS.Api.Features.Projects.UpdateProject;

public static class UpdateProjectEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/projects/{id:int}", HandleAsync)
            .WithName("UpdateProject")
            .WithTags("Projects")
            .WithSummary("Updates a project, reconciles its activities and syncs its map feature.")
            .WithValidation<UpdateProjectCommand>()
            .Produces<UpdateProjectResponse>()
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        int id,
        UpdateProjectCommand command,
        UpdateProjectCommandHandler handler,
        CancellationToken ct)
    {
        var (response, failure) = await handler.HandleAsync(id, command, CurrentUser.Id, ct);

        return failure is null ? Results.Ok(response) : failure.ToResult();
    }
}
