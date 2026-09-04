using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PMGIS.Domain.Entities;
using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Gis.BackfillProjectFeatures;

// One-shot repair for rows that hold a location but never got a point in the layer.
public sealed class BackfillProjectFeaturesCommandHandler(
    PmgisDbContext db,
    IFeatureServiceClient featureService,
    IOptions<ArcGisOptions> options,
    ILogger<BackfillProjectFeaturesCommandHandler> logger)
{
    // Consecutive batch-level rejections after which the run gives up.
    private const int MaxConsecutiveBatchFailures = 3;

    // Failures echoed in the response; the count is always exact.
    private const int FailureSampleSize = 100;

    private readonly ArcGisOptions _options = options.Value;

    public async Task<BackfillProjectFeaturesResult> HandleAsync(
        BackfillProjectFeaturesCommand command, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var candidates = await Candidates().CountAsync(ct);

        logger.LogInformation(
            "Backfill starting: {Candidates} project rows have a location but no ObjectId.", candidates);

        if (candidates == 0)
        {
            return Empty(command, stopwatch);
        }

        // Adoption map. Reading the owned features once costs a handful of paged queries
        // and is what stops a re-run from doubling up points in a shared public layer.
        var existingBySourceId = (await featureService.GetOwnedFeaturesAsync(ct))
            .Where(f => f.SourceId.HasValue)
            .GroupBy(f => f.SourceId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        logger.LogInformation(
            "Layer already holds {Count} features owned by this application.", existingBySourceId.Count);

        var created = 0;
        var adopted = 0;
        var foreign = 0;
        var written = 0;
        var attempted = 0;
        var batches = 0;
        var failureCount = 0;
        var failures = new List<BackfillFailure>();
        var consecutiveBatchFailures = 0;
        string? abortReason = null;

        var cursor = 0;
        var remaining = command.MaxProjects > 0 ? command.MaxProjects : int.MaxValue;

        while (remaining > 0 && abortReason is null)
        {
            var take = Math.Min(command.BatchSize, remaining);

            // Keyset paging. The cursor matters even though updated rows drop out of the
            // filter: rows that fail keep matching it, and offset paging would loop.
            var batch = await Candidates()
                .Where(p => p.Id > cursor)
                .OrderBy(p => p.Id)
                .Take(take)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                break;
            }

            batches++;
            attempted += batch.Count;
            cursor = batch[^1].Id;
            remaining -= batch.Count;

            var toAdd = new List<Project>(batch.Count);
            var adds = new List<FeatureAdd>(batch.Count);

            foreach (var project in batch)
            {
                var sourceId = SourceIdFor(project.Id);

                // Adopt only a feature this application actually created. Occupying the
                // SOURCEID slot is not proof of ownership on a shared layer, where another
                // consumer may use the same range, so the project code has to agree too.
                if (existingBySourceId.TryGetValue(sourceId, out var existing))
                {
                    if (string.Equals(existing.ProjectCode, project.ProjectCode, StringComparison.Ordinal))
                    {
                        project.ObjectId = existing.ObjectId;
                        adopted++;
                        continue;
                    }

                    // Someone else's feature sits in this slot. Create our own rather than
                    // claim theirs; theirs stays an orphan in the reconciliation report.
                    logger.LogWarning(
                        "SOURCEID {SourceId} is held by feature {ObjectId} carrying code {LayerCode}, " +
                        "not {ProjectCode}. Creating a separate feature instead of adopting it.",
                        sourceId, existing.ObjectId, existing.ProjectCode ?? "(none)", project.ProjectCode);

                    foreign++;
                }

                toAdd.Add(project);
                adds.Add(new FeatureAdd(
                    project.ProjectCode,
                    sourceId,
                    new FeaturePoint(project.Longitude!.Value, project.Latitude!.Value)));
            }

            if (adds.Count > 0)
            {
                IReadOnlyList<FeatureEditResult> results;

                try
                {
                    results = await featureService.AddFeaturesAsync(adds, ct);
                    consecutiveBatchFailures = 0;
                }
                catch (Exception ex) when (
                    ex is FeatureServiceException or HttpRequestException or TaskCanceledException &&
                    !ct.IsCancellationRequested)
                {
                    consecutiveBatchFailures++;

                    logger.LogError(ex,
                        "Batch {Batch} rejected by the feature service ({Consecutive} in a row).",
                        batches, consecutiveBatchFailures);

                    foreach (var project in toAdd)
                    {
                        Fail(project, ex.Message);
                    }

                    if (consecutiveBatchFailures >= MaxConsecutiveBatchFailures)
                    {
                        abortReason =
                            $"Stopped after {consecutiveBatchFailures} consecutive batch failures: {ex.Message}";

                        logger.LogError("Backfill aborting. {Reason}", abortReason);
                    }

                    db.ChangeTracker.Clear();
                    continue;
                }

                // The results array is index-aligned with the adds; that index is the only
                // link between an assigned ObjectId and a project row.
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    var project = toAdd[i];

                    if (result.Success)
                    {
                        project.ObjectId = result.ObjectId;
                        created++;
                    }
                    else
                    {
                        Fail(project,
                            $"{result.Error?.Description ?? "no description"} (code {result.Error?.Code})");
                    }
                }
            }

            // Only rows that actually got an ObjectId are persisted. Anything else stays a
            // candidate for the next run.
            var linked = batch.Count(p => p.ObjectId.HasValue);

            if (linked > 0)
            {
                var strategy = db.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                });

                written += linked;
            }

            // The batch entities have served their purpose; drop them so a long run does
            // not accumulate thousands of tracked projects.
            db.ChangeTracker.Clear();

            logger.LogInformation(
                "Backfill batch {Batch}: {Linked}/{Size} linked (created {Created}, adopted {Adopted}), " +
                "{Written} of {Candidates} rows done, {Elapsed:0.0}s elapsed.",
                batches, linked, batch.Count, created, adopted, written, candidates,
                stopwatch.Elapsed.TotalSeconds);
        }

        stopwatch.Stop();

        logger.LogInformation(
            "Backfill finished in {Elapsed:0.0}s: {Created} created, {Adopted} adopted, " +
            "{Foreign} SOURCEID slots held by other consumers, {Written} ObjectIds written, " +
            "{Failures} failures.",
            stopwatch.Elapsed.TotalSeconds, created, adopted, foreign, written, failureCount);

        return new BackfillProjectFeaturesResult
        {
            Candidates = candidates,
            Attempted = attempted,
            FeaturesCreated = created,
            FeaturesAdopted = adopted,
            ObjectIdsWritten = written,
            FailureCount = failureCount,
            Failures = failures,
            BatchSize = command.BatchSize,
            BatchesProcessed = batches,
            ElapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            Aborted = abortReason is not null,
            AbortReason = abortReason,
        };

        void Fail(Project project, string reason)
        {
            failureCount++;

            if (failures.Count < FailureSampleSize)
            {
                failures.Add(new BackfillFailure(project.Id, project.ProjectCode, reason));
            }
        }
    }

    // SOURCEID is the project Id inside this application's namespace in the shared layer.
    private int SourceIdFor(int projectId) => _options.SourceIdBase + projectId;

    private IQueryable<Project> Candidates() =>
        db.Projects.Where(p => p.Latitude != null && p.Longitude != null && p.ObjectId == null);

    private static BackfillProjectFeaturesResult Empty(
        BackfillProjectFeaturesCommand command, Stopwatch stopwatch) => new()
        {
            Candidates = 0,
            Attempted = 0,
            FeaturesCreated = 0,
            FeaturesAdopted = 0,
            ObjectIdsWritten = 0,
            FailureCount = 0,
            Failures = [],
            BatchSize = command.BatchSize,
            BatchesProcessed = 0,
            ElapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
        };
}
