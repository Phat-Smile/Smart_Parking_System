using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Messaging.Inbox;
using ParkingSystem.Parking.Persistence.Outbox;
using ParkingSystem.Parking.Persistence.Inbox;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence.ParkingLots;

namespace ParkingSystem.Parking.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Parking persistence requires ConnectionStrings:DefaultConnection (or ConnectionStrings__DefaultConnection). Configure it before starting the Parking API.");
        }

        services.AddDbContext<ParkingDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ParkingDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ParkingDbContext.DefaultSchema);
            }));
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IInboxExecutionCoordinator, ParkingInboxExecutionCoordinator>();
        services.AddScoped<IParkingLotCatalogReader, ParkingLotCatalogReader>();
        services.AddScoped<IParkingLotDetailReader, ParkingLotDetailReader>();
        return services;
    }
}
