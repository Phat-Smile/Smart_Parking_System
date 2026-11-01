using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Persistence.Outbox;
using ParkingSystem.Parking.Persistence.Inbox;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence.Auditing;
using ParkingSystem.Parking.Persistence.ParkingLayout;

namespace ParkingSystem.Parking.Persistence;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "parking";

    public DbSet<PricingPolicy> PricingPolicies => Set<PricingPolicy>();

    public DbSet<ReservationDepositPolicy> ReservationDepositPolicies => Set<ReservationDepositPolicy>();
    public DbSet<ReservationRatioPolicy> ReservationRatioPolicies => Set<ReservationRatioPolicy>();
    public DbSet<ReservationAvailabilityPolicy> ReservationAvailabilityPolicies => Set<ReservationAvailabilityPolicy>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();

    public DbSet<GateReading> GateReadings => Set<GateReading>();

    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();

    public DbSet<ParkingLotAssignment> ParkingLotAssignments => Set<ParkingLotAssignment>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<ParkingLayoutDocument> LayoutDocuments => Set<ParkingLayoutDocument>();

    public DbSet<Floor> Floors => Set<Floor>();

    public DbSet<Zone> Zones => Set<Zone>();

    public DbSet<ParkingSlot> ParkingSlots => Set<ParkingSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParkingDbContext).Assembly);
    }
}
