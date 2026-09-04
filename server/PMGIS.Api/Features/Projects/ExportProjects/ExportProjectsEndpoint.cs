using PMGIS.Api.Common;

namespace PMGIS.Api.Features.Projects.ExportProjects;

public static class ExportProjectsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects/export", HandleAsync)
            .WithName("ExportProjects")
            .WithTags("Projects")
            .WithSummary("The current filter and sort, streamed as CSV.")
            .WithValidation<ExportProjectsQuery>();

    private static Task HandleAsync(
        [AsParameters] ExportProjectsQuery query,
        ExportProjectsQueryHandler handler,
        HttpContext http,
        CancellationToken ct) =>
        handler.WriteCsvAsync(query, http.Response, ct);
}
