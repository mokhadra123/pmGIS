using FluentValidation;

using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Domain.Rules;

namespace PMGIS.Api.Features.Projects.CreateProject;

// Server-side enforcement of the rules the form also applies in the browser.
public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;
        RuleLevelCascadeMode = CascadeMode.Continue;

        RuleFor(p => p.ProjectCode)
            .NotEmpty().WithMessage("Project Code is required.")
            .Matches(ProjectCodeRules.Pattern)
            .WithMessage("Project Code must be three uppercase letters, a hyphen and four digits, for example ABC-0000.");

        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Project Name is required.")
            .MaximumLength(200);

        RuleFor(p => p.Description)
            .MaximumLength(500)
            .WithMessage("Description is limited to 500 characters.");

        RuleFor(p => p.Budget)
            .GreaterThanOrEqualTo(0).When(p => p.Budget.HasValue)
            .WithMessage("Budget cannot be negative.");

        RuleFor(p => p.EndDate)
            .Must((command, end) => end > command.StartDate)
            .When(p => p.StartDate.HasValue && p.EndDate.HasValue)
            .WithMessage("End Date must be later than Start Date.");

        RuleForEach(p => p.Activities).SetValidator(new ProjectActivityInputValidator());

        // Reported against the collection rather than a single row, because the offending
        // rows are identified individually in the message.
        RuleFor(p => p.Activities).Custom((activities, context) =>
        {
            var command = context.InstanceToValidate;
            ActivityWindowRule.Check(activities, command.StartDate, command.EndDate, context.AddFailure);
        });
    }
}
