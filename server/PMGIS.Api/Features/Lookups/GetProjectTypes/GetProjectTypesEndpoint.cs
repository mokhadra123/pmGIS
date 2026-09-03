using PMGIS.Api.Features.Lookups.Shared;

namespace PMGIS.Api.Features.Lookups.GetProjectTypes;

public static class GetProjectTypesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/lookups/project-types", HandleAsync)
            .WithName("GetProjectTypes")
            .WithTags("Lookups")
            .WithSummary("Project Type coded-value domain.")
            .Produces<IReadOnlyList<LookupItem>>();

    private static async Task<IResult> HandleAsync(
        GetProjectTypesQueryHandler handler, CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(new GetProjectTypesQuery(), ct));
}
