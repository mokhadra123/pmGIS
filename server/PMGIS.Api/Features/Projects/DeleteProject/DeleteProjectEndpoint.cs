using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Projects.DeleteProject;

public static class DeleteProjectEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/projects/{id:int}", HandleAsync)
            .WithName("DeleteProject")
            .WithTags("Projects")
            .WithSummary("Deletes a project, its activities and its map feature.")
            .WithValidation<DeleteProjectCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteProjectCommand command,
        DeleteProjectCommandHandler handler,
        CancellationToken ct)
    {
        var failure = await handler.HandleAsync(command, ct);

        return failure is null ? Results.NoContent() : failure.ToResult();
    }
}
