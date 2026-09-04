using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.GetProjects;

public sealed class GetProjectsQueryHandler(PmgisDbContext db)
{
    public Task<PagedResult<ProjectListItem>> HandleAsync(GetProjectsQuery query, CancellationToken ct) =>
        ProjectQueries.PageAsync(db, query.ToQuery(), ct);
}
