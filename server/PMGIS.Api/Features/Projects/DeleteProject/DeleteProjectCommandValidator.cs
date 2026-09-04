using FluentValidation;

namespace PMGIS.Api.Features.Projects.DeleteProject;

public sealed class DeleteProjectCommandValidator : AbstractValidator<DeleteProjectCommand>
{
    public DeleteProjectCommandValidator() =>
        RuleFor(c => c.Id).GreaterThan(0).WithMessage("Project id must be greater than zero.");
}
