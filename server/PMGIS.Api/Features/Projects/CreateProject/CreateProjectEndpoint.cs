using PMGIS.Api.Common;
using PMGIS.Api.Features.Projects.Shared;

namespace PMGIS.Api.Features.Projects.CreateProject;

public static class CreateProjectEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/projects", HandleAsync)
            .WithName("CreateProject")
            .WithTags("Projects")
            .WithSummary("Creates a project, its activities and its map feature.")
            .WithValidation<CreateProjectCommand>()
            .Produces<CreateProjectResponse>(StatusCodes.Status201Created);

    private static async Task<IResult> HandleAsync(
        CreateProjectCommand command,
        CreateProjectCommandHandler handler,
        CancellationToken ct)
    {
        var (response, failure) = await handler.HandleAsync(command, CurrentUser.Id, ct);

        return failure is null
            ? Results.Created($"/api/projects/{response!.Id}", response)
            : failure.ToResult();
    }
}
