using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Infrastructure.Messaging.Inbox;

namespace ParkingSystem.Parking.Infrastructure;

public static class IdempotentConsumerRegistrationExtensions
{
    public static IServiceCollection AddIdempotentConsumer<TConsumer>(this IServiceCollection services, string consumerKey)
        where TConsumer : class, IConsumer
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(consumerKey) || consumerKey.Length > 512)
            throw new ArgumentException("ConsumerKey must contain between 1 and 512 characters.", nameof(consumerKey));

        var definitionServiceType = typeof(IConsumerDefinition<TConsumer>);
        if (services.Any(descriptor => descriptor.ServiceType == definitionServiceType))
            throw new InvalidOperationException($"An idempotent consumer definition is already registered for '{typeof(TConsumer).Name}'.");

        var registryDescriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(InboxConsumerKeyRegistry));
        InboxConsumerKeyRegistry registry;
        if (registryDescriptor?.ImplementationInstance is InboxConsumerKeyRegistry existingRegistry)
        {
            registry = existingRegistry;
        }
        else if (registryDescriptor is null)
        {
            registry = new InboxConsumerKeyRegistry();
            services.AddSingleton(registry);
        }
        else
        {
            throw new InvalidOperationException("InboxConsumerKeyRegistry must be registered as a singleton instance before consumers are added.");
        }

        registry.Register(typeof(TConsumer), consumerKey);
        services.AddSingleton<IConsumerDefinition<TConsumer>>(new InboxConsumerDefinition<TConsumer>(consumerKey));
        return services;
    }
}

public sealed class InboxConsumerKeyRegistry
{
    private readonly Dictionary<Type, string> _keysByConsumerType = [];
    private readonly Dictionary<string, Type> _consumerTypesByKey = new(StringComparer.Ordinal);

    public void Register(Type consumerType, string consumerKey)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerKey);
        if (consumerKey.Length > 512)
            throw new ArgumentException("ConsumerKey cannot exceed 512 characters.", nameof(consumerKey));
        if (_keysByConsumerType.ContainsKey(consumerType))
            throw new InvalidOperationException($"Consumer type '{consumerType.Name}' already has an Inbox ConsumerKey registration.");
        if (_consumerTypesByKey.TryGetValue(consumerKey, out var existingType))
            throw new InvalidOperationException($"Inbox ConsumerKey '{consumerKey}' is already registered for '{existingType.Name}'.");

        _keysByConsumerType.Add(consumerType, consumerKey);
        _consumerTypesByKey.Add(consumerKey, consumerType);
    }
}