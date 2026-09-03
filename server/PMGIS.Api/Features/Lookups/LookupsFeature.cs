using PMGIS.Api.Features.Lookups.GetProjectStatuses;
using PMGIS.Api.Features.Lookups.GetProjectTypes;

namespace PMGIS.Api.Features.Lookups;

public static class LookupsFeature
{
    public static IServiceCollection AddLookupsFeature(this IServiceCollection services)
    {
        services.AddScoped<GetProjectStatusesQueryHandler>();
        services.AddScoped<GetProjectTypesQueryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapLookupsFeature(this IEndpointRouteBuilder app)
    {
        GetProjectStatusesEndpoint.Map(app);
        GetProjectTypesEndpoint.Map(app);

        return app;
    }
}
