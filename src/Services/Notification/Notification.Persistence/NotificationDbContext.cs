using Microsoft.EntityFrameworkCore;
using ParkingSystem.Notification.Persistence.Outbox;
using ParkingSystem.Notification.Persistence.Inbox;
using ParkingSystem.Notification.Persistence.CustomerNotifications;

namespace ParkingSystem.Notification.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "notification";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
