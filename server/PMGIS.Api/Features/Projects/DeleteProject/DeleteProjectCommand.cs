using Microsoft.AspNetCore.Mvc;

namespace PMGIS.Api.Features.Projects.DeleteProject;

public sealed record DeleteProjectCommand
{
    [FromRoute] public int Id { get; init; }
}
