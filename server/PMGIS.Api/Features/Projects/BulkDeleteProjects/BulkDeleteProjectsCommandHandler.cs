using PMGIS.Api.Features.Projects.DeleteProject;

namespace PMGIS.Api.Features.Projects.BulkDeleteProjects;

// Deletes each project through the single-delete handler and reports per-project outcomes.
public sealed class BulkDeleteProjectsCommandHandler(DeleteProjectCommandHandler deleteProject)
{
    public async Task<BulkDeleteResult> HandleAsync(BulkDeleteProjectsCommand command, CancellationToken ct)
    {
        var deleted = new List<int>();
        var failed = new List<BulkDeleteFailure>();

        foreach (var id in command.ProjectIds.Distinct())
        {
            var failure = await deleteProject.HandleAsync(new DeleteProjectCommand { Id = id }, ct);

            if (failure is null)
            {
                deleted.Add(id);
            }
            else
            {
                failed.Add(new BulkDeleteFailure(id, failure.Message));
            }
        }

        return new BulkDeleteResult { Deleted = deleted, Failed = failed };
    }
}
