using System.Globalization;

using CsvHelper;

using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.ExportProjects;

// Streams the filtered set as CSV so neither side has to hold the whole table.
public sealed class ExportProjectsQueryHandler(PmgisDbContext db)
{
    public async Task WriteCsvAsync(ExportProjectsQuery query, HttpResponse response, CancellationToken ct)
    {
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition =
            $"attachment; filename=projects-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        await using var writer = new StreamWriter(response.Body);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("Project Name");
        csv.WriteField("Project Code");
        csv.WriteField("Project Type");
        csv.WriteField("Start Date");
        csv.WriteField("End Date");
        csv.WriteField("Status");
        csv.WriteField("Activities");
        csv.WriteField("Duration (days)");
        csv.WriteField("Last Modified By");
        csv.WriteField("Last Modified On");
        await csv.NextRecordAsync();

        await foreach (var row in ProjectQueries.StreamAsync(db, query.ToQuery(), ct))
        {
            csv.WriteField(row.Name);
            csv.WriteField(row.ProjectCode);
            csv.WriteField(row.ProjectTypeName);
            csv.WriteField(row.StartDate?.ToString("yyyy-MM-dd"));
            csv.WriteField(row.EndDate?.ToString("yyyy-MM-dd"));
            csv.WriteField(row.Status.ToString());
            csv.WriteField(row.ActivityCount);
            csv.WriteField(row.DurationDays);
            csv.WriteField(row.LastModifiedByName);
            csv.WriteField(row.LastModifiedOn.ToString("u"));
            await csv.NextRecordAsync();
        }

        await csv.FlushAsync();
    }
}
