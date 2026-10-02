using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Parking.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventTypeResolver eventTypeResolver,
    IClock clock,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxDispatcherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("The Outbox dispatcher is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await DispatchIterationAsync(stoppingToken) > 0)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox polling iteration failed; retrying after {PollingInterval}.", _options.PollingInterval);
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }

    private async Task<int> DispatchIterationAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var nowUtc = clock.UtcNow;
        var claimed = await store.ClaimPendingAsync(
            Guid.NewGuid(), nowUtc, nowUtc + _options.LeaseDuration, _options.BatchSize, cancellationToken);

        foreach (var record in claimed)
            await DispatchOneAsync(store, publisher, record, cancellationToken);
        return claimed.Count;
    }

    private async Task DispatchOneAsync(
        IOutboxStore store,
        IIntegrationEventPublisher publisher,
        OutboxDispatchRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            var eventClrType = eventTypeResolver.Resolve(record.EventType, record.EventVersion);
            if (eventClrType is null)
                throw new InvalidOperationException($"No integration event is registered for '{record.EventType}' version {record.EventVersion}.");

            var message = JsonSerializer.Deserialize(record.Payload, eventClrType)
                ?? throw new JsonException("The Outbox payload deserialized to null.");
            await publisher.PublishAsync(message, eventClrType, record, cancellationToken);

            if (await store.MarkProcessedAsync(record.Id, record.LockId, clock.UtcNow, cancellationToken))
            {
                logger.LogDebug(
                    "Published Outbox message {OutboxMessageId} ({EventType} v{EventVersion}) with retry count {RetryCount}.",
                    record.Id, record.EventType, record.EventVersion, record.RetryCount);
            }
            else
            {
                logger.LogWarning(
                    "Outbox message {OutboxMessageId} ({EventType} v{EventVersion}) was published after its lease ownership changed.",
                    record.Id, record.EventType, record.EventVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = SanitizeError(exception);
            var nextRetryCount = record.RetryCount + 1;
            var updated = await store.MarkFailedAsync(
                record.Id, record.LockId, clock.UtcNow + CalculateRetryDelay(nextRetryCount), error, cancellationToken);

            if (updated)
                logger.LogWarning(
                    "Outbox dispatch failed for {OutboxMessageId} ({EventType} v{EventVersion}) on retry {RetryCount}: {Error}",
                    record.Id, record.EventType, record.EventVersion, nextRetryCount, error);
            else
                logger.LogWarning(
                    "Outbox dispatch failed for {OutboxMessageId} ({EventType} v{EventVersion}), but lease ownership changed. Error: {Error}",
                    record.Id, record.EventType, record.EventVersion, error);
        }
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        var delay = _options.BaseRetryDelay;
        for (var attempt = 1; attempt < retryCount && delay < _options.MaxRetryDelay; attempt++)
            delay = delay.Ticks >= _options.MaxRetryDelay.Ticks / 2
                ? _options.MaxRetryDelay
                : TimeSpan.FromTicks(delay.Ticks * 2);
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }

    private static string SanitizeError(Exception exception)
    {
        var message = exception.Message.ReplaceLineEndings(" ");
        message = Regex.Replace(message, @"(?i)(password|passwd|pwd|secret|token|connection string)\s*[:=]\s*[^;\s,]+", "$1=[redacted]");
        message = Regex.Replace(message, @"(?i)(amqps?://)[^/@\s]+@", "$1[redacted]@");
        if (message.Length > 450)
            message = message[..450];
        return $"{exception.GetType().Name}: {message}";
    }
}

