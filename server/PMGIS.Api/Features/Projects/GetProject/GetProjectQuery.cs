using Microsoft.AspNetCore.Mvc;

using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Projects.GetProject;

public sealed record GetProjectQuery
{
    [FromRoute] public int Id { get; init; }
}

public sealed record ProjectDetailResponse
{
    public required int Id { get; init; }
    public required string ProjectCode { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int? ProjectTypeId { get; init; }
    public string? ProjectTypeName { get; init; }
    public required ProjectStatus Status { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? Budget { get; init; }
    public int? OwnerUserId { get; init; }
    public string? OwnerName { get; init; }
    public long? ObjectId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? DurationDays { get; init; }

    // Average % complete weighted by activity duration.
    public double Progress { get; init; }

    public string? LastModifiedByName { get; init; }
    public DateTimeOffset LastModifiedOn { get; init; }
    public IReadOnlyList<ActivityResponse> Activities { get; init; } = [];
}

public sealed record ActivityResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public required ActivityStatus Status { get; init; }
    public int AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public int PercentComplete { get; init; }
    public int DurationDays { get; init; }
}
