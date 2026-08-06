using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RepoNavAI.Application.Common.Behaviors;
using RepoNavAI.Application.Organizations;

namespace RepoNavAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IOrganizationAccess, OrganizationAccess>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
