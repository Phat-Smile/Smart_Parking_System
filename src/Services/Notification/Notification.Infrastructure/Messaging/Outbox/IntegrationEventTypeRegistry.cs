using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ParkingSystem.Notification.Infrastructure.Messaging.Outbox;

public interface IIntegrationEventTypeResolver
{
    Type? Resolve(string eventType, int eventVersion);
}

public sealed record IntegrationEventRegistration(string EventType, int EventVersion, Type EventClrType);

public sealed class IntegrationEventTypeRegistry : IIntegrationEventTypeResolver
{
    private readonly IReadOnlyDictionary<(string EventType, int EventVersion), Type> _eventTypes;

    public IntegrationEventTypeRegistry(IEnumerable<IntegrationEventRegistration> registrations)
    {
        var eventTypes = new Dictionary<(string EventType, int EventVersion), Type>();
        foreach (var registration in registrations)
        {
            if (!eventTypes.TryAdd((registration.EventType, registration.EventVersion), registration.EventClrType))
                throw new InvalidOperationException($"Integration event '{registration.EventType}' version {registration.EventVersion} is registered more than once.");
        }

        _eventTypes = eventTypes;
    }

    public Type? Resolve(string eventType, int eventVersion) =>
        _eventTypes.TryGetValue((eventType, eventVersion), out var eventClrType) ? eventClrType : null;
}

public static class IntegrationEventRegistrationExtensions
{
    public static IServiceCollection AddIntegrationEvent<TEvent>(this IServiceCollection services, string eventType, int eventVersion)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventVersion);
        services.AddIntegrationEventRegistry();
        services.AddSingleton(new IntegrationEventRegistration(eventType, eventVersion, typeof(TEvent)));
        return services;
    }

    internal static IServiceCollection AddIntegrationEventRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<IntegrationEventTypeRegistry>();
        services.TryAddSingleton<IIntegrationEventTypeResolver>(
            serviceProvider => serviceProvider.GetRequiredService<IntegrationEventTypeRegistry>());
        return services;
    }
}

