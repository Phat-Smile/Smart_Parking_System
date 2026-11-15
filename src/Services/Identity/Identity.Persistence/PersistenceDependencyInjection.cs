using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Admin;
using ParkingSystem.Identity.Application.Accounts.Mfa;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Common.Interfaces;
using ParkingSystem.Identity.Application.Common.Messaging.Inbox;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Persistence.Accounts;
using ParkingSystem.Identity.Persistence.Accounts.Admin;
using ParkingSystem.Identity.Persistence.Auditing;
using ParkingSystem.Identity.Persistence.EmailVerifications;
using ParkingSystem.Identity.Persistence.Inbox;
using ParkingSystem.Identity.Persistence.Mfa;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Identity.Persistence.Profiles;
using ParkingSystem.Identity.Persistence.Repositories;
using ParkingSystem.Identity.Persistence.Vehicles;

namespace ParkingSystem.Identity.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Identity persistence requires ConnectionStrings:DefaultConnection (or ConnectionStrings__DefaultConnection). Configure it before starting the Identity API.");
        }

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.DefaultSchema);
            }));
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IInboxExecutionCoordinator, IdentityInboxExecutionCoordinator>();
        services.AddScoped<AccountRepository>();
        services.AddScoped<IAccountRepository>(provider => provider.GetRequiredService<AccountRepository>());
        services.AddScoped<IGoogleAccountRepository>(provider => provider.GetRequiredService<AccountRepository>());
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<AdminAccountQueries>();
        services.AddScoped<IAdminAccountQueries>(provider => provider.GetRequiredService<AdminAccountQueries>());
        services.AddScoped<IOwnerApplicationQueries>(provider => provider.GetRequiredService<AdminAccountQueries>());
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IOwnerDocumentRepository, OwnerDocumentRepository>();
        services.AddScoped<ParkingSystem.Identity.Application.Profiles.CustomerMedia.ICustomerMediaStore,
            ParkingSystem.Identity.Persistence.Profiles.CustomerMediaStore>();
        services.AddScoped<IMfaRepository, MfaRepository>();
        services.AddScoped<IIdentityAuditLogQueryRepository, IdentityAuditLogQueryRepository>();
        services.AddScoped<IIdentityAuditLog, IdentityAuditLog>();

        // Register DB-backed vehicle lookup when provider = "Database"; Infrastructure registers the Stub otherwise.
        if (string.Equals(configuration["VehicleLookup:Provider"], "Database", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IVehicleLookupReader, DbVehicleLookupReader>();

        return services;
    }
}
