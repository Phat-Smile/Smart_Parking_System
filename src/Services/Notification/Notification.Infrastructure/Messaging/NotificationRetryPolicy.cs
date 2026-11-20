using MassTransit;

namespace ParkingSystem.Notification.Infrastructure.Messaging;

public static class NotificationRetryPolicy
{
    public const int DefaultRetryCount = 5;
    public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultMaxInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultIntervalDelta = TimeSpan.FromSeconds(5);

    public static void Configure(
        IRetryConfigurator configurator,
        int retryCount = DefaultRetryCount,
        TimeSpan? minInterval = null,
        TimeSpan? maxInterval = null,
        TimeSpan? intervalDelta = null)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.Exponential(
            retryCount,
            minInterval ?? DefaultMinInterval,
            maxInterval ?? DefaultMaxInterval,
            intervalDelta ?? DefaultIntervalDelta);
    }
}
