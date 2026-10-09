using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Common.Messaging.Inbox;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Persistence.Accounts;
using ParkingSystem.Identity.Persistence.EmailVerifications;
using ParkingSystem.Identity.Persistence.Inbox;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Identity.Persistence.Profiles;

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
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        return services;
    }
}
