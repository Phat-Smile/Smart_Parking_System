using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.Behaviors;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IHeldQuotaShortageCheck, HeldQuotaShortageCheck>();
        return services;
    }
}
