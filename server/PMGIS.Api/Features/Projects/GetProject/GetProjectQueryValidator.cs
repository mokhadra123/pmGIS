using FluentValidation;

namespace PMGIS.Api.Features.Projects.GetProject;

public sealed class GetProjectQueryValidator : AbstractValidator<GetProjectQuery>
{
    public GetProjectQueryValidator() =>
        RuleFor(q => q.Id).GreaterThan(0).WithMessage("Project id must be greater than zero.");
}
