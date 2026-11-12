using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Notification.Application.Common.Messaging.Outbox;
using ParkingSystem.Notification.Application.Common.Messaging.Inbox;
using ParkingSystem.Notification.Persistence.Outbox;
using ParkingSystem.Notification.Persistence.Inbox;
using ParkingSystem.Notification.Persistence.CustomerNotifications;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Notification persistence requires ConnectionStrings:DefaultConnection (or ConnectionStrings__DefaultConnection). Configure it before starting the Notification API.");
        }

        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.DefaultSchema);
            }));
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IInboxExecutionCoordinator, NotificationInboxExecutionCoordinator>();
        services.AddScoped<ICustomerNotificationStore, CustomerNotificationStore>();
        return services;
    }
}
