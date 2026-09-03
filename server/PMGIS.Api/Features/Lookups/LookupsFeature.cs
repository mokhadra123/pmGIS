using PMGIS.Api.Features.Lookups.GetProjectStatuses;

namespace PMGIS.Api.Features.Lookups;

public static class LookupsFeature
{
    public static IServiceCollection AddLookupsFeature(this IServiceCollection services)
    {
        services.AddScoped<GetProjectStatusesQueryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapLookupsFeature(this IEndpointRouteBuilder app)
    {
        GetProjectStatusesEndpoint.Map(app);

        return app;
    }
}
