using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ParkingSystem.Notification.Infrastructure.Messaging;
using ParkingSystem.Notification.Infrastructure.Messaging.Outbox;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Infrastructure.Consumers;
using ParkingSystem.Notification.Infrastructure.Email;

namespace ParkingSystem.Notification.Infrastructure;

public static class DependencyInjection
{
    // Without an SMTP host the console sender is used; it writes the whole email, OTP code included, to the logs.
    public static bool UsesConsoleEmailSender(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var smtpHost = configuration["SMTP_HOST"] ?? configuration["Smtp:Host"];
        return string.IsNullOrWhiteSpace(smtpHost) || smtpHost.Equals("console", StringComparison.OrdinalIgnoreCase);
    }

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
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
        services.AddHostedService<OutboxDispatcher>();
        if (UsesConsoleEmailSender(configuration))
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        services.AddIdempotentConsumer<UserVerificationOtpIssuedConsumer>("notification.user-verification-otp-issued.v1");
        services.AddIdempotentConsumer<PasswordResetOtpIssuedConsumer>("notification.password-reset-otp-issued.v1");
        services.AddIdempotentConsumer<OwnerApplicationApprovedConsumer>("notification.owner-application-approved.v1");
        services.AddIdempotentConsumer<OwnerApplicationRejectedConsumer>("notification.owner-application-rejected.v1");
        services.AddIdempotentConsumer<WalletTopUpNotificationConsumer>("notification.wallet-top-up.v1");
        services.AddIdempotentConsumer<ParkingSessionClosedNotificationConsumer>("notification.parking-session-closed.v1");
        services.AddIdempotentConsumer<ParkingSessionOpenedNotificationConsumer>("notification.parking-session-opened.v1");
        services.AddIdempotentConsumer<ReservationConfirmedNotificationConsumer>("notification.reservation-confirmed.v1");
        services.AddHealthChecks()
            .AddCheck<RabbitMqAvailabilityHealthCheck>("rabbitmq-amqp", tags: ["ready", "rabbitmq"]);
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<UserVerificationOtpIssuedConsumer>();
            bus.AddConsumer<PasswordResetOtpIssuedConsumer>();
            bus.AddConsumer<OwnerApplicationApprovedConsumer>();
            bus.AddConsumer<OwnerApplicationRejectedConsumer>();
            bus.AddConsumer<WalletTopUpNotificationConsumer>();
            bus.AddConsumer<ParkingSessionClosedNotificationConsumer>();
            bus.AddConsumer<ParkingSessionOpenedNotificationConsumer>();
            bus.AddConsumer<ReservationConfirmedNotificationConsumer>();
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

        return services;
    }
}
