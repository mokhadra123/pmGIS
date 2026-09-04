using Microsoft.EntityFrameworkCore;

using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Domain.Entities;
using PMGIS.Domain.Rules;
using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;

namespace PMGIS.Api.Features.Projects.UpdateProject;

// The feature is written before the commit, so a feature failure rolls the database back.
public sealed class UpdateProjectCommandHandler(
    PmgisDbContext db,
    ProjectFeatureSync featureSync,
    ILogger<UpdateProjectCommandHandler> logger)
{
    public async Task<(UpdateProjectResponse? Response, WriteFailure? Failure)> HandleAsync(
        int projectId, UpdateProjectCommand command, int currentUserId, CancellationToken ct)
    {
        var project = await db.Projects
            .Include(p => p.Activities)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
        {
            return (null, WriteFailure.NotFound);
        }

        if (!string.Equals(project.ProjectCode, command.ProjectCode, StringComparison.Ordinal) &&
            await db.Projects.AnyAsync(p => p.ProjectCode == command.ProjectCode && p.Id != projectId, ct))
        {
            return (null, new WriteFailure(
                $"Project code {command.ProjectCode} is already in use.", nameof(command.ProjectCode)));
        }

        // The status state machine can only be checked here: it compares each row against
        // the status actually stored, which the request validator cannot see.
        var transitionErrors = InvalidTransitions(project, command.Activities);

        if (transitionErrors.Count > 0)
        {
            return (null, WriteFailure.Validation(transitionErrors));
        }

        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            var now = DateTimeOffset.UtcNow;

            project.ProjectCode = command.ProjectCode;
            project.Name = command.Name;
            project.Description = command.Description;
            project.ProjectTypeId = command.ProjectTypeId;
            project.Status = command.Status;
            project.StartDate = command.StartDate;
            project.EndDate = command.EndDate;
            project.Budget = command.Budget;
            project.OwnerUserId = command.OwnerUserId;
            project.Latitude = command.Latitude;
            project.Longitude = command.Longitude;
            project.LastModifiedByUserId = currentUserId;
            project.LastModifiedOn = now;

            ActivityReconciler.Reconcile(project, command.Activities, currentUserId, now);

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                await db.SaveChangesAsync(ct);

                // The feature write happens before the commit, so a feature failure
                // rolls the database back rather than leaving the two stores disagreeing.
                await featureSync.SyncAsync(project, command.Latitude, command.Longitude, ct);

                await transaction.CommitAsync(ct);
            });

            return (new UpdateProjectResponse(project.Id, project.ProjectCode, project.ObjectId), null);
        }
        catch (FeatureServiceException ex)
        {
            logger.LogError(ex, "Feature update failed for project {ProjectId}; database rolled back.", projectId);
            return (null, new WriteFailure($"Could not update the project location: {ex.Message}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed for project {ProjectId}.", projectId);
            return (null, new WriteFailure("The project could not be saved. No changes were made."));
        }
    }

    // Forbidden status moves, keyed by the indexed field name the form binds to.
    private static Dictionary<string, string[]> InvalidTransitions(
        Project project, IReadOnlyList<ProjectActivityInput> incoming)
    {
        var stored = project.Activities
            .Where(a => !a.IsDeleted)
            .ToDictionary(a => a.Id, a => a.Status);

        var errors = new Dictionary<string, string[]>();

        for (var i = 0; i < incoming.Count; i++)
        {
            var row = incoming[i];

            if (row.Id is not { } id ||
                !stored.TryGetValue(id, out var from) ||
                ActivityStatusTransitions.CanTransition(from, row.Status))
            {
                continue;
            }

            errors[$"Activities[{i}].Status"] =
                [$"\"{row.Name}\" cannot move from {from} to {row.Status}."];
        }

        return errors;
    }
}
