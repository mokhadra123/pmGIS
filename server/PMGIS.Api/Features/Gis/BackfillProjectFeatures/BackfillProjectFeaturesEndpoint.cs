using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Gis.BackfillProjectFeatures;

public static class BackfillProjectFeaturesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/gis/backfill-features", HandleAsync)
            .WithName("BackfillProjectFeatures")
            .WithTags("GIS")
            .WithSummary("Creates layer points for project rows that have a location but no ObjectId.")
            .WithDescription(
                "Safe to re-run: rows that already carry an ObjectId are not candidates, and a " +
                "feature already present under a project SOURCEID is adopted rather than duplicated.")
            .WithValidation<BackfillProjectFeaturesCommand>()
            .Produces<BackfillProjectFeaturesResult>();

    private static async Task<IResult> HandleAsync(
        BackfillProjectFeaturesCommand? command,
        BackfillProjectFeaturesCommandHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(command ?? new BackfillProjectFeaturesCommand(), ct));
}
