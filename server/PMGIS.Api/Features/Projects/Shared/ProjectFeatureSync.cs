using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PMGIS.Domain.Entities;
using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Projects.Shared;

// The feature-layer half of a project write, shared by the create and update slices.
public sealed class ProjectFeatureSync(
    PmgisDbContext db,
    IFeatureServiceClient featureService,
    IOptions<ArcGisOptions> options)
{
    private readonly ArcGisOptions _options = options.Value;

    // SOURCEID namespaces this application's features inside the shared sample layer.
    public async Task<int> NextSourceIdAsync(CancellationToken ct)
    {
        var baseId = _options.SourceIdBase;

        var highWaterMark = await db.Database
            .SqlQueryRaw<int>(
                """
                SELECT GREATEST(
                    COALESCE((SELECT MAX("Id") FROM "Projects"), 0),
                    COALESCE(pg_sequence_last_value(pg_get_serial_sequence('"Projects"', 'Id')), 0)
                )::int AS "Value"
                """)
            .SingleAsync(ct);

        return baseId + highWaterMark + 1;
    }

    // Adds a point for a project that does not yet have one, and returns its ObjectId.
    public async Task<long> AddAsync(string projectCode, double latitude, double longitude, CancellationToken ct)
    {
        var sourceId = await NextSourceIdAsync(ct);
        return await featureService.AddFeatureAsync(
            projectCode, sourceId, new FeaturePoint(longitude, latitude), ct);
    }

    public Task DeleteAsync(long objectId, CancellationToken ct) =>
        featureService.DeleteFeatureAsync(objectId, ct);

    // Updates in place, adds, or deletes the point so the ObjectId is never reissued.
    public async Task SyncAsync(Project project, double? latitude, double? longitude, CancellationToken ct)
    {
        var hasLocation = latitude.HasValue && longitude.HasValue;

        switch (project.ObjectId, hasLocation)
        {
            case ({ } objectId, true):
                await featureService.UpdateFeatureAsync(
                    objectId, project.ProjectCode,
                    new FeaturePoint(longitude!.Value, latitude!.Value), ct);
                break;

            case (null, true):
                project.ObjectId = await AddAsync(
                    project.ProjectCode, latitude!.Value, longitude!.Value, ct);
                await db.SaveChangesAsync(ct);
                break;

            case ({ } removed, false):
                await featureService.DeleteFeatureAsync(removed, ct);
                project.ObjectId = null;
                await db.SaveChangesAsync(ct);
                break;

            case (null, false):
                break;
        }
    }
}
