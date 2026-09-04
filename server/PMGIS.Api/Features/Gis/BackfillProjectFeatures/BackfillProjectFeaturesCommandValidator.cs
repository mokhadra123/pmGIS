using FluentValidation;

namespace PMGIS.Api.Features.Gis.BackfillProjectFeatures;

public sealed class BackfillProjectFeaturesCommandValidator
    : AbstractValidator<BackfillProjectFeaturesCommand>
{
    public BackfillProjectFeaturesCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(c => c.BatchSize)
            .InclusiveBetween(1, 500)
            .WithMessage("Batch size must be between 1 and 500 features per applyEdits call.");

        RuleFor(c => c.MaxProjects)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Max projects cannot be negative. Use 0 for no limit.");
    }
}
