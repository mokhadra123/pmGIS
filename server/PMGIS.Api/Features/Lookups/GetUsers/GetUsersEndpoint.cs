namespace PMGIS.Api.Features.Lookups.GetUsers;

public static class GetUsersEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/lookups/users", HandleAsync)
            .WithName("GetUsers")
            .WithTags("Lookups")
            .WithSummary("Application users, for Project Owner and Assigned To.")
            .Produces<IReadOnlyList<UserLookupItem>>();

    private static async Task<IResult> HandleAsync(
        GetUsersQueryHandler handler, CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(new GetUsersQuery(), ct));
}
