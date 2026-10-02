using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ParkingSystem.Payment.Infrastructure.Messaging;

public sealed class RabbitMqAvailabilityHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var rabbitMq = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.Host,
            Port = rabbitMq.Port,
            UserName = rabbitMq.Username,
            Password = rabbitMq.Password,
            VirtualHost = rabbitMq.VirtualHost,
            RequestedConnectionTimeout = ConnectionTimeout,
            HandshakeContinuationTimeout = ConnectionTimeout,
            ContinuationTimeout = ConnectionTimeout
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectionTimeout);
        Task<IConnection>? connectionTask = null;
        try
        {
            connectionTask = factory.CreateConnectionAsync("ParkingSystem.Readiness", timeout.Token);
            await using var connection = await connectionTask.WaitAsync(ConnectionTimeout, cancellationToken);
            return connection.IsOpen ? HealthCheckResult.Healthy("RabbitMQ AMQP connection is open.") : HealthCheckResult.Unhealthy("RabbitMQ AMQP connection is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (connectionTask is not null)
                _ = DisposeConnectionWhenAvailableAsync(connectionTask);

            return HealthCheckResult.Unhealthy("RabbitMQ AMQP connection is unavailable.");
        }
    }

    private static async Task DisposeConnectionWhenAvailableAsync(Task<IConnection> connectionTask)
    {
        try
        {
            await using var connection = await connectionTask;
        }
        catch (Exception)
        {
        }
    }
}
