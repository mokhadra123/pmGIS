using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Gis.GetReconciliationReport;

public static class GetReconciliationReportEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/gis/reconciliation", HandleAsync)
            .WithName("GetReconciliationReport")
            .WithTags("GIS")
            .WithSummary("Features with no project row, and project rows whose ObjectId is gone.")
            .Produces<ReconciliationReport>();

    private static async Task<IResult> HandleAsync(
        GetReconciliationReportQueryHandler handler, CancellationToken ct) =>
        Results.Ok(await handler.HandleAsync(new GetReconciliationReportQuery(), ct));
}
