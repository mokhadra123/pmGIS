using PMGIS.Api.Features.Lookups.GetActivityStatuses;
using PMGIS.Api.Features.Lookups.GetProjectStatuses;
using PMGIS.Api.Features.Lookups.GetProjectTypes;
using PMGIS.Api.Features.Lookups.GetUsers;

namespace PMGIS.Api.Features.Lookups;

public static class LookupsFeature
{
    public static IServiceCollection AddLookupsFeature(this IServiceCollection services)
    {
        services.AddScoped<GetProjectStatusesQueryHandler>();
        services.AddScoped<GetProjectTypesQueryHandler>();
        services.AddScoped<GetUsersQueryHandler>();
        services.AddScoped<GetActivityStatusesQueryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapLookupsFeature(this IEndpointRouteBuilder app)
    {
        GetProjectStatusesEndpoint.Map(app);
        GetProjectTypesEndpoint.Map(app);
        GetUsersEndpoint.Map(app);
        GetActivityStatusesEndpoint.Map(app);

        return app;
    }
}
