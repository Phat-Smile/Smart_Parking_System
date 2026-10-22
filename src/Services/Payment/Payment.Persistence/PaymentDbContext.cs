using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Persistence.Outbox;
using ParkingSystem.Payment.Persistence.Inbox;

namespace ParkingSystem.Payment.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "payment";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
