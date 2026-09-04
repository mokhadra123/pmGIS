using FluentValidation;

using PMGIS.Api.Features.Gis.BackfillProjectFeatures;
using PMGIS.Api.Features.Gis.GetReconciliationReport;

namespace PMGIS.Api.Features.Gis;

public static class GisFeature
{
    public static IServiceCollection AddGisFeature(this IServiceCollection services)
    {
        services.AddScoped<GetReconciliationReportQueryHandler>();
        services.AddScoped<BackfillProjectFeaturesCommandHandler>();

        services.AddScoped<IValidator<BackfillProjectFeaturesCommand>,
            BackfillProjectFeaturesCommandValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapGisFeature(this IEndpointRouteBuilder app)
    {
        GetReconciliationReportEndpoint.Map(app);
        BackfillProjectFeaturesEndpoint.Map(app);

        return app;
    }
}
