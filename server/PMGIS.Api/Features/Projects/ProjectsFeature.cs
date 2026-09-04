using FluentValidation;

using PMGIS.Api.Features.Projects.BulkDeleteProjects;
using PMGIS.Api.Features.Projects.CheckProjectCode;
using PMGIS.Api.Features.Projects.CreateProject;
using PMGIS.Api.Features.Projects.DeleteProject;
using PMGIS.Api.Features.Projects.ExportProjects;
using PMGIS.Api.Features.Projects.GetNearbyProjects;
using PMGIS.Api.Features.Projects.GetProject;
using PMGIS.Api.Features.Projects.GetProjects;
using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Api.Features.Projects.UpdateProject;

namespace PMGIS.Api.Features.Projects;

// The Projects slices, listed once.
public static class ProjectsFeature
{
    public static IServiceCollection AddProjectsFeature(this IServiceCollection services)
    {
        services.AddScoped<ProjectFeatureSync>();

        services.AddScoped<GetProjectsQueryHandler>();
        services.AddScoped<GetProjectQueryHandler>();
        services.AddScoped<ExportProjectsQueryHandler>();
        services.AddScoped<GetNearbyProjectsQueryHandler>();
        services.AddScoped<CheckProjectCodeQueryHandler>();
        services.AddScoped<CreateProjectCommandHandler>();
        services.AddScoped<UpdateProjectCommandHandler>();
        services.AddScoped<DeleteProjectCommandHandler>();
        services.AddScoped<BulkDeleteProjectsCommandHandler>();

        services.AddScoped<IValidator<GetProjectsQuery>, GetProjectsQueryValidator>();
        services.AddScoped<IValidator<GetProjectQuery>, GetProjectQueryValidator>();
        services.AddScoped<IValidator<ExportProjectsQuery>, ExportProjectsQueryValidator>();
        services.AddScoped<IValidator<GetNearbyProjectsQuery>, GetNearbyProjectsQueryValidator>();
        services.AddScoped<IValidator<CheckProjectCodeQuery>, CheckProjectCodeQueryValidator>();
        services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectCommandValidator>();
        services.AddScoped<IValidator<UpdateProjectCommand>, UpdateProjectCommandValidator>();
        services.AddScoped<IValidator<DeleteProjectCommand>, DeleteProjectCommandValidator>();
        services.AddScoped<IValidator<BulkDeleteProjectsCommand>, BulkDeleteProjectsCommandValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapProjectsFeature(this IEndpointRouteBuilder app)
    {
        // Order matters for the literal segments: /export, /nearby and /code-available
        // must be registered before {id:int} would otherwise shadow them. Routing scores
        // literals above constrained parameters, so this is belt and braces — but it also
        // reads in the order a person would look for them.
        GetProjectsEndpoint.Map(app);
        ExportProjectsEndpoint.Map(app);
        GetNearbyProjectsEndpoint.Map(app);
        CheckProjectCodeEndpoint.Map(app);
        GetProjectEndpoint.Map(app);

        CreateProjectEndpoint.Map(app);
        UpdateProjectEndpoint.Map(app);
        DeleteProjectEndpoint.Map(app);
        BulkDeleteProjectsEndpoint.Map(app);

        return app;
    }
}
