using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Behaviors;

namespace ParkingSystem.Identity.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
