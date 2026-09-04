using FluentValidation;

using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Projects.Shared;

// Rules for a single activity row.
public sealed class ProjectActivityInputValidator : AbstractValidator<ProjectActivityInput>
{
    public ProjectActivityInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;
        RuleLevelCascadeMode = CascadeMode.Continue;

        RuleFor(a => a.Name)
            .NotEmpty().WithMessage("Activity Name is required.")
            .MaximumLength(200);

        RuleFor(a => a.EndDate)
            .GreaterThanOrEqualTo(a => a.StartDate)
            .WithMessage("Activity End Date cannot be before its Start Date.");

        RuleFor(a => a.PercentComplete)
            .InclusiveBetween(0, 100)
            .WithMessage("% Complete must be between 0 and 100.");

        RuleFor(a => a.PercentComplete)
            .Equal(0).When(a => a.Status == ActivityStatus.Planned)
            .WithMessage("% Complete must be 0 while an activity is Planned.");

        RuleFor(a => a.PercentComplete)
            .Equal(100).When(a => a.Status == ActivityStatus.Completed)
            .WithMessage("% Complete must be 100 when an activity is Completed.");
    }
}
