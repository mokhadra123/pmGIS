using Microsoft.EntityFrameworkCore;

using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Projects.DeleteProject;

// Rows are removed, then the feature, and only then is the transaction committed.
public sealed class DeleteProjectCommandHandler(
    PmgisDbContext db,
    ProjectFeatureSync featureSync,
    ILogger<DeleteProjectCommandHandler> logger)
{
    public async Task<WriteFailure?> HandleAsync(DeleteProjectCommand command, CancellationToken ct)
    {
        var project = await db.Projects
            .Include(p => p.Activities)
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct);

        if (project is null)
        {
            return WriteFailure.NotFound;
        }

        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                // Cascade delete removes the activities; both go in the same transaction.
                db.Projects.Remove(project);
                await db.SaveChangesAsync(ct);

                if (project.ObjectId is { } objectId)
                {
                    await featureSync.DeleteAsync(objectId, ct);
                }

                await transaction.CommitAsync(ct);
            });

            return null;
        }
        catch (FeatureServiceException ex)
        {
            logger.LogError(ex, "Feature delete failed for project {ProjectId}; database rolled back.", command.Id);
            return new WriteFailure($"Could not delete the project location: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delete failed for project {ProjectId}.", command.Id);
            return new WriteFailure("The project could not be deleted. No changes were made.");
        }
    }
}
