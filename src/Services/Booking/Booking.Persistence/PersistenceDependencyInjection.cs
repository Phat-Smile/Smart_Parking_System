using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Common.Messaging.Inbox;
using ParkingSystem.Booking.Persistence.Outbox;
using ParkingSystem.Booking.Persistence.Inbox;

namespace ParkingSystem.Booking.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Booking persistence requires ConnectionStrings:DefaultConnection (or ConnectionStrings__DefaultConnection). Configure it before starting the Booking API.");
        }

        services.AddDbContext<BookingDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(BookingDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.DefaultSchema);
            }));
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IInboxExecutionCoordinator, BookingInboxExecutionCoordinator>();
        return services;
    }
}
