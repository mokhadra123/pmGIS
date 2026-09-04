using FluentValidation;

namespace PMGIS.Api.Features.Projects.GetProjects;

// Guards paging and the date range.
public sealed class GetProjectsQueryValidator : AbstractValidator<GetProjectsQuery>
{
    public GetProjectsQueryValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).When(q => q.Page.HasValue)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 500).When(q => q.PageSize.HasValue)
            .WithMessage("Page Size must be between 1 and 500.");

        RuleFor(q => q.DateTo)
            .GreaterThanOrEqualTo(q => q.DateFrom)
            .When(q => q.DateFrom.HasValue && q.DateTo.HasValue)
            .WithMessage("Date To cannot be before Date From.");
    }
}
