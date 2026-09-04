using FluentValidation;

namespace PMGIS.Api.Features.Projects.GetNearbyProjects;

public sealed class GetNearbyProjectsQueryValidator : AbstractValidator<GetNearbyProjectsQuery>
{
    public GetNearbyProjectsQueryValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(q => q.RadiusKm)
            .GreaterThan(0).WithMessage("Radius must be greater than zero.");

        RuleFor(q => q.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(q => q.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");

        RuleFor(q => q.Limit)
            .InclusiveBetween(1, 1000).When(q => q.Limit.HasValue)
            .WithMessage("Limit must be between 1 and 1000.");
    }
}
