using PMGIS.Api.Features.Lookups.Shared;

namespace PMGIS.Api.Features.Lookups.GetActivityStatuses;

public static class GetActivityStatusesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/lookups/activity-statuses", HandleAsync)
            .WithName("GetActivityStatuses")
            .WithTags("Lookups")
            .WithSummary("ActivityStatus coded-value domain.")
            .Produces<IReadOnlyList<LookupItem>>();

    private static IResult HandleAsync(GetActivityStatusesQueryHandler handler) =>
        Results.Ok(handler.Handle(new GetActivityStatusesQuery()));
}
