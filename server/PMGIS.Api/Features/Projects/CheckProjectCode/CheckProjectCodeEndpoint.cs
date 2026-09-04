using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Projects.CheckProjectCode;

public static class CheckProjectCodeEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects/code-available", HandleAsync)
            .WithName("CheckProjectCode")
            .WithTags("Projects")
            .WithSummary("Whether a project code is well formed and still free.")
            .WithValidation<CheckProjectCodeQuery>()
            .Produces<CodeAvailabilityResponse>();

    private static async Task<IResult> HandleAsync(
        [AsParameters] CheckProjectCodeQuery query,
        CheckProjectCodeQueryHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(query, ct));
}
