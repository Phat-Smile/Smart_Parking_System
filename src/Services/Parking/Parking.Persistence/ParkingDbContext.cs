using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Persistence.Outbox;
using ParkingSystem.Parking.Persistence.Inbox;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "parking";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();

    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParkingDbContext).Assembly);
    }
}
