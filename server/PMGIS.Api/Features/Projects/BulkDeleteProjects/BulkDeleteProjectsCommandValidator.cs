using FluentValidation;

namespace PMGIS.Api.Features.Projects.BulkDeleteProjects;

public sealed class BulkDeleteProjectsCommandValidator : AbstractValidator<BulkDeleteProjectsCommand>
{
    public BulkDeleteProjectsCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(c => c.ProjectIds)
            .NotEmpty().WithMessage("Select at least one project to delete.");

        RuleForEach(c => c.ProjectIds)
            .GreaterThan(0).WithMessage("Project ids must be greater than zero.");
    }
}
