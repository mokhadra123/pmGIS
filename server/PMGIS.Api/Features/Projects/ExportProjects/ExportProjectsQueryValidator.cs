using FluentValidation;

namespace PMGIS.Api.Features.Projects.ExportProjects;

public sealed class ExportProjectsQueryValidator : AbstractValidator<ExportProjectsQuery>
{
    public ExportProjectsQueryValidator()
    {
        RuleFor(q => q.DateTo)
            .GreaterThanOrEqualTo(q => q.DateFrom)
            .When(q => q.DateFrom.HasValue && q.DateTo.HasValue)
            .WithMessage("Date To cannot be before Date From.");
    }
}
