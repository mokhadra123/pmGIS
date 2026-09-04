using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Projects.BulkDeleteProjects;

public static class BulkDeleteProjectsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/projects/bulk-delete", HandleAsync)
            .WithName("BulkDeleteProjects")
            .WithTags("Projects")
            .WithSummary("Deletes the selected projects and reports each outcome.")
            .WithValidation<BulkDeleteProjectsCommand>()
            .Produces<BulkDeleteResult>();

    private static async Task<IResult> HandleAsync(
        BulkDeleteProjectsCommand command,
        BulkDeleteProjectsCommandHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(command, ct));
}
