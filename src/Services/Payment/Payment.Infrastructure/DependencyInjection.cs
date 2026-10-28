using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Infrastructure.Messaging;
using ParkingSystem.Payment.Infrastructure.Messaging.Outbox;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Infrastructure.Gateways;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Infrastructure.Adapters.Parking;

namespace ParkingSystem.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host is required.")
            .Validate(options => options.Port is > 0 and <= 65535, "RabbitMq:Port must be between 1 and 65535.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "RabbitMq:Username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMq:Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost), "RabbitMq:VirtualHost is required.")
            .ValidateOnStart();

        services.Configure<MassTransitHostOptions>(options => options.WaitUntilStarted = false);
        services.AddOptions<OutboxDispatcherOptions>()
            .Bind(configuration.GetSection(OutboxDispatcherOptions.SectionName))
            .Validate(options => options.PollingInterval > TimeSpan.Zero, "Outbox:PollingInterval must be positive.")
            .Validate(options => options.BatchSize > 0, "Outbox:BatchSize must be positive.")
            .Validate(options => options.LeaseDuration > TimeSpan.Zero, "Outbox:LeaseDuration must be positive.")
            .Validate(options => options.BaseRetryDelay > TimeSpan.Zero, "Outbox:BaseRetryDelay must be positive.")
            .Validate(options => options.MaxRetryDelay >= options.BaseRetryDelay, "Outbox:MaxRetryDelay must be greater than or equal to Outbox:BaseRetryDelay.")
            .ValidateOnStart();
        services.AddSingleton<IClock, SystemClock>();
        services.AddIntegrationEventRegistry();
        services.AddIntegrationEvent<WalletTopUpCompletedIntegrationEvent>(
            "payment.wallet-topup-completed.v1", 1);
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHealthChecks()
            .AddCheck<RabbitMqAvailabilityHealthCheck>("rabbitmq-amqp", tags: ["ready", "rabbitmq"]);
        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);
            bus.UsingRabbitMq((context, rabbit) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                rabbit.Host(options.Host, (ushort)options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });
                rabbit.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IPaymentGateway, VnpayPaymentGateway>();
        services.AddScoped<IParkingSessionGateway, StubParkingSessionGateway>();
        return services;
    }
}
