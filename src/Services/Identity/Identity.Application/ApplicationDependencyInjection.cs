using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Behaviors;
using ParkingSystem.Identity.Application.Vehicles.Lookup;

namespace ParkingSystem.Identity.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<ILicensePlateNormalizer, LicensePlateNormalizer>();
        services.AddScoped<Accounts.IAuthSessionManager, Accounts.AuthSessionManager>();
        services.AddScoped<Accounts.Mfa.MfaLoginGate>();
        return services;
    }
}
