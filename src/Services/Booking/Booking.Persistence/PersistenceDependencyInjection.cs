using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Common.Messaging.Inbox;
using ParkingSystem.Booking.Persistence.Outbox;
using ParkingSystem.Booking.Persistence.Inbox;
using Npgsql;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Persistence.Reservations;

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

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<VehicleGroup>("booking.vehicle_group");
        dataSourceBuilder.MapEnum<ReservationType>("booking.reservation_type");
        dataSourceBuilder.MapEnum<ReservationStatus>("booking.reservation_status");
        dataSourceBuilder.MapEnum<CancelReason>("booking.cancel_reason");
        dataSourceBuilder.MapEnum<PrepaidOutcome>("booking.prepaid_outcome");
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
        {
            var ds = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(
                ds,
                npgsql =>
                {
                    npgsql.MapEnum<VehicleGroup>("vehicle_group", BookingDbContext.DefaultSchema);
                    npgsql.MapEnum<ReservationType>("reservation_type", BookingDbContext.DefaultSchema);
                    npgsql.MapEnum<ReservationStatus>("reservation_status", BookingDbContext.DefaultSchema);
                    npgsql.MapEnum<CancelReason>("cancel_reason", BookingDbContext.DefaultSchema);
                    npgsql.MapEnum<PrepaidOutcome>("prepaid_outcome", BookingDbContext.DefaultSchema);
                    npgsql.MigrationsAssembly(typeof(BookingDbContext).Assembly.GetName().Name!);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.DefaultSchema);
                });
        });
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IReservationArrivalReminderTracker, ReservationArrivalReminderTracker>();
        services.AddScoped<IArrivalHeldQuotaReader, ArrivalHeldQuotaReader>();
        services.AddScoped<IReservationCapacityCoordinator, ReservationCapacityCoordinator>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IInboxExecutionCoordinator, BookingInboxExecutionCoordinator>();
        return services;
    }
}
