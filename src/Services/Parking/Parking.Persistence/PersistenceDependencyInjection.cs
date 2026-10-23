using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Messaging.Inbox;
using ParkingSystem.Parking.Persistence.Outbox;
using ParkingSystem.Parking.Persistence.Inbox;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Persistence.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence.ParkingLots;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Persistence.Auditing;
using ParkingSystem.Parking.Persistence.ParkingLayout;
using ParkingSystem.Parking.Persistence.Repositories;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

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
        services.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<ParkingLotCatalogReader>();
        services.AddScoped<IParkingLotCatalogReader>(provider => provider.GetRequiredService<ParkingLotCatalogReader>());
        services.AddScoped<IParkingLotArrivalSearch>(provider => provider.GetRequiredService<ParkingLotCatalogReader>());
        services.AddScoped<IParkingLotOpeningHoursRepository, ParkingLotOpeningHoursRepository>();
        services.AddScoped<IParkingLotDetailReader, ParkingLotDetailReader>();
        services.AddScoped<IRegisterParkingLotRepository, RegisterParkingLotRepository>();

        services.AddScoped<ParkingLotAssignmentRepository>();
        services.AddScoped<IParkingLotAssignmentRepository>(
            provider => provider.GetRequiredService<ParkingLotAssignmentRepository>());
        services.AddScoped<IParkingLotAccess>(
            provider => provider.GetRequiredService<ParkingLotAssignmentRepository>());
        services.AddScoped<IPricingPolicyRepository, PricingPolicyRepository>();
        services.AddScoped<ILotOwnershipService, LotOwnershipService>();
        services.AddScoped<IParkingUnitOfWork, ParkingUnitOfWork>();
        services.AddParkingLayout(configuration);
        services.AddScoped<IParkingLotApprovalRepository, ParkingLotApprovalRepository>();
        return services;
    }
}
