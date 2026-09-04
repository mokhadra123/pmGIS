using FluentValidation;

namespace PMGIS.Api.Features.Projects.CheckProjectCode;

// Only presence is enforced.
public sealed class CheckProjectCodeQueryValidator : AbstractValidator<CheckProjectCodeQuery>
{
    public CheckProjectCodeQueryValidator() =>
        RuleFor(q => q.Code).NotEmpty().WithMessage("Project Code is required.");
}
