using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Gis.GetReconciliationReport;

public sealed class GetReconciliationReportQueryHandler(ReconciliationService service)
{
    public Task<ReconciliationReport> HandleAsync(GetReconciliationReportQuery query, CancellationToken ct) =>
        service.RunAsync(ct);
}
