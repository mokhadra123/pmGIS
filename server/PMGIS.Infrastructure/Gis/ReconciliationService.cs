using Microsoft.EntityFrameworkCore;

using PMGIS.Infrastructure.Data;

namespace PMGIS.Infrastructure.Gis;

public sealed record OrphanFeature(long ObjectId, string? ProjectCode);
public sealed record OrphanProjectRow(int ProjectId, string ProjectCode, long ObjectId);
public sealed record CodeMismatch(long ObjectId, string DatabaseCode, string? LayerCode);

public sealed record ReconciliationReport
{
    // Features in the layer with no project row pointing at them.
    public required IReadOnlyList<OrphanFeature> OrphanFeatures { get; init; }

    // Project rows whose ObjectId no longer exists in the layer.
    public required IReadOnlyList<OrphanProjectRow> OrphanProjectRows { get; init; }

    // Rows and features that are linked but disagree on Project Code.
    public required IReadOnlyList<CodeMismatch> CodeMismatches { get; init; }

    public int FeaturesChecked { get; init; }
    public int ProjectsChecked { get; init; }
    public DateTimeOffset GeneratedOn { get; init; } = DateTimeOffset.UtcNow;

    public bool IsClean =>
        OrphanFeatures.Count == 0 && OrphanProjectRows.Count == 0 && CodeMismatches.Count == 0;
}

// Compares the feature layer against the database in both directions, as the brief requires.
public sealed class ReconciliationService(
    PmgisDbContext db,
    IFeatureServiceClient featureService)
{
    public async Task<ReconciliationReport> RunAsync(CancellationToken ct = default)
    {
        var features = await featureService.GetOwnedFeaturesAsync(ct);

        var rows = await db.Projects
            .Where(p => p.ObjectId != null)
            .Select(p => new { p.Id, p.ProjectCode, ObjectId = p.ObjectId!.Value })
            .ToListAsync(ct);

        var rowsByObjectId = rows.ToDictionary(r => r.ObjectId);
        var featuresByObjectId = features.ToDictionary(f => f.ObjectId);

        var orphanFeatures = features
            .Where(f => !rowsByObjectId.ContainsKey(f.ObjectId))
            .Select(f => new OrphanFeature(f.ObjectId, f.ProjectCode))
            .ToList();

        var orphanRows = rows
            .Where(r => !featuresByObjectId.ContainsKey(r.ObjectId))
            .Select(r => new OrphanProjectRow(r.Id, r.ProjectCode, r.ObjectId))
            .ToList();

        var mismatches = rows
            .Where(r => featuresByObjectId.TryGetValue(r.ObjectId, out var f) &&
                        !string.Equals(f.ProjectCode, r.ProjectCode, StringComparison.Ordinal))
            .Select(r => new CodeMismatch(
                r.ObjectId, r.ProjectCode, featuresByObjectId[r.ObjectId].ProjectCode))
            .ToList();

        return new ReconciliationReport
        {
            OrphanFeatures = orphanFeatures,
            OrphanProjectRows = orphanRows,
            CodeMismatches = mismatches,
            FeaturesChecked = features.Count,
            ProjectsChecked = rows.Count,
        };
    }
}
