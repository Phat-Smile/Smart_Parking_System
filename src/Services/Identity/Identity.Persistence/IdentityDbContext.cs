using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Identity.Persistence.Inbox;

namespace ParkingSystem.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "identity";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
