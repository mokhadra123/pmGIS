using PMGIS.Domain.Entities;
using PMGIS.Domain.Rules;

namespace PMGIS.Api.Features.Projects.Shared;

// Brings the stored activities in line with the posted rows in a single pass.
public static class ActivityReconciler
{
    public static Activity ToEntity(ProjectActivityInput row, DateTimeOffset now) => new()
    {
        Name = row.Name,
        StartDate = row.StartDate,
        EndDate = row.EndDate,
        Status = row.Status,
        AssignedToUserId = row.AssignedToUserId,
        PercentComplete = ActivityStatusTransitions
            .NormalizePercentComplete(row.Status, row.PercentComplete),
        CreatedOn = now,
        LastModifiedOn = now,
    };

    public static void Reconcile(
        Project project,
        IReadOnlyList<ProjectActivityInput> incoming,
        int currentUserId,
        DateTimeOffset now)
    {
        var incomingIds = incoming.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();

        foreach (var existing in project.Activities.Where(a => !a.IsDeleted))
        {
            if (incomingIds.Contains(existing.Id))
            {
                continue;
            }

            existing.IsDeleted = true;
            existing.DeletedByUserId = currentUserId;
            existing.DeletedOn = now;
            existing.LastModifiedOn = now;
        }

        foreach (var row in incoming)
        {
            if (row.Id is { } id)
            {
                var existing = project.Activities.FirstOrDefault(a => a.Id == id);

                if (existing is null)
                {
                    continue;
                }

                existing.Name = row.Name;
                existing.StartDate = row.StartDate;
                existing.EndDate = row.EndDate;
                existing.Status = row.Status;
                existing.AssignedToUserId = row.AssignedToUserId;
                existing.PercentComplete = ActivityStatusTransitions
                    .NormalizePercentComplete(row.Status, row.PercentComplete);
                existing.LastModifiedOn = now;
            }
            else
            {
                project.Activities.Add(ToEntity(row, now));
            }
        }
    }
}
