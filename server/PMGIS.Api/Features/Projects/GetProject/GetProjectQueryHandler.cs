using Microsoft.EntityFrameworkCore;

using PMGIS.Domain.Rules;
using PMGIS.Infrastructure.Data;

namespace PMGIS.Api.Features.Projects.GetProject;

public sealed class GetProjectQueryHandler(PmgisDbContext db)
{
    public async Task<ProjectDetailResponse?> HandleAsync(GetProjectQuery query, CancellationToken ct)
    {
        var project = await db.Projects
            .Include(p => p.ProjectType)
            .Include(p => p.Owner)
            .Include(p => p.LastModifiedBy)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.AssignedTo)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == query.Id, ct);

        if (project is null)
        {
            return null;
        }

        return new ProjectDetailResponse
        {
            Id = project.Id,
            ProjectCode = project.ProjectCode,
            Name = project.Name,
            Description = project.Description,
            ProjectTypeId = project.ProjectTypeId,
            ProjectTypeName = project.ProjectType?.Name,
            Status = project.Status,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Budget = project.Budget,
            OwnerUserId = project.OwnerUserId,
            OwnerName = project.Owner?.Name,
            ObjectId = project.ObjectId,
            Latitude = project.Latitude,
            Longitude = project.Longitude,
            DurationDays = project.DurationDays,
            Progress = ProjectProgress.Calculate(project.Activities),
            LastModifiedByName = project.LastModifiedBy?.Name,
            LastModifiedOn = project.LastModifiedOn,
            Activities = [.. project.Activities.Select(a => new ActivityResponse
            {
                Id = a.Id,
                Name = a.Name,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                Status = a.Status,
                AssignedToUserId = a.AssignedToUserId,
                AssignedToName = a.AssignedTo?.Name,
                PercentComplete = a.PercentComplete,
                DurationDays = a.DurationDays,
            })],
        };
    }
}
