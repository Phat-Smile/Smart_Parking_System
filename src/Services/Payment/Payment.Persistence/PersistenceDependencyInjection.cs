using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Common.Messaging.Inbox;
using ParkingSystem.Payment.Persistence.Outbox;
using ParkingSystem.Payment.Persistence.Inbox;

namespace ParkingSystem.Payment.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Payment persistence requires ConnectionStrings:DefaultConnection (or ConnectionStrings__DefaultConnection). Configure it before starting the Payment API.");
        }

        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PaymentDbContext).Assembly.GetName().Name!);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PaymentDbContext.DefaultSchema);
            }));
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IInboxExecutionCoordinator, PaymentInboxExecutionCoordinator>();
        return services;
    }
}
