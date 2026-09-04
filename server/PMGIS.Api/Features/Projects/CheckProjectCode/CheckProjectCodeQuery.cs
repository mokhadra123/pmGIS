using Microsoft.AspNetCore.Mvc;

namespace PMGIS.Api.Features.Projects.CheckProjectCode;

// Backs the form's asynchronous uniqueness check on blur.
public sealed record CheckProjectCodeQuery
{
    [FromQuery] public string Code { get; init; } = string.Empty;

    // Set when editing, so a project does not collide with its own code.
    [FromQuery] public int? ExcludeProjectId { get; init; }
}

public sealed record CodeAvailabilityResponse(string ProjectCode, bool IsAvailable, bool IsWellFormed);
