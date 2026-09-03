using PMGIS.Api.Features.Lookups.Shared;

namespace PMGIS.Api.Features.Lookups.GetProjectStatuses;

public static class GetProjectStatusesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/lookups/project-statuses", HandleAsync)
            .WithName("GetProjectStatuses")
            .WithTags("Lookups")
            .WithSummary("ProjectStatus coded-value domain.")
            .Produces<IReadOnlyList<LookupItem>>();

    private static IResult HandleAsync(GetProjectStatusesQueryHandler handler) =>
        Results.Ok(handler.Handle(new GetProjectStatusesQuery()));
}
