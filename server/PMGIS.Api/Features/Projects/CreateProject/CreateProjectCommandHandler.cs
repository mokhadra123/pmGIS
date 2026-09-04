using Microsoft.EntityFrameworkCore;

using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Domain.Entities;
using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Projects.CreateProject;

// The feature is written first, because the database row needs the ObjectId the service assigns.
public sealed class CreateProjectCommandHandler(
    PmgisDbContext db,
    ProjectFeatureSync featureSync,
    ILogger<CreateProjectCommandHandler> logger)
{
    public async Task<(CreateProjectResponse? Response, WriteFailure? Failure)> HandleAsync(
        CreateProjectCommand command, int currentUserId, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(p => p.ProjectCode == command.ProjectCode, ct))
        {
            return (null, new WriteFailure(
                $"Project code {command.ProjectCode} is already in use.", nameof(command.ProjectCode)));
        }

        long? objectId = null;

        // Step 1: the feature, so we have an ObjectId to store.
        if (command.HasLocation)
        {
            try
            {
                objectId = await featureSync.AddAsync(
                    command.ProjectCode, command.Latitude!.Value, command.Longitude!.Value, ct);
            }
            catch (FeatureServiceException ex)
            {
                logger.LogError(ex, "Feature creation failed for {ProjectCode}", command.ProjectCode);
                return (null, new WriteFailure($"Could not store the project location: {ex.Message}"));
            }
        }

        // Step 2: the row and its activities, in one database transaction.
        //
        // Aspire configures a retrying execution strategy, which refuses user-initiated
        // transactions unless the whole unit is executed through it. Wrapping the block
        // means a transient database fault retries the entire transaction rather than
        // half-applying it.
        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            var now = DateTimeOffset.UtcNow;

            var project = new Project
            {
                ProjectCode = command.ProjectCode,
                Name = command.Name,
                Description = command.Description,
                ProjectTypeId = command.ProjectTypeId,
                Status = command.Status,
                StartDate = command.StartDate,
                EndDate = command.EndDate,
                Budget = command.Budget,
                OwnerUserId = command.OwnerUserId,
                ObjectId = objectId,
                Latitude = command.Latitude,
                Longitude = command.Longitude,
                CreatedByUserId = currentUserId,
                CreatedOn = now,
                LastModifiedByUserId = currentUserId,
                LastModifiedOn = now,
            };

            foreach (var activity in command.Activities)
            {
                project.Activities.Add(ActivityReconciler.ToEntity(activity, now));
            }

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                db.Projects.Add(project);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });

            return (new CreateProjectResponse(project.Id, project.ProjectCode, project.ObjectId), null);
        }
        catch (Exception ex)
        {
            // Compensating action: the feature exists but nothing references it.
            if (objectId is { } orphan)
            {
                logger.LogError(ex,
                    "Database write failed after creating feature {ObjectId}; deleting it to avoid an orphan.",
                    orphan);

                try
                {
                    await featureSync.DeleteAsync(orphan, ct);
                }
                catch (Exception cleanupEx)
                {
                    // Both writes failed. Log loudly: the reconciliation report is the
                    // safety net that will surface this feature.
                    logger.LogCritical(cleanupEx,
                        "Could not remove orphan feature {ObjectId}. Reconciliation will report it.", orphan);
                }
            }

            return (null, new WriteFailure("The project could not be saved. No changes were made."));
        }
    }
}
