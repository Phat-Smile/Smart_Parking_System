using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence.Outbox;
using ParkingSystem.Booking.Persistence.Inbox;

namespace ParkingSystem.Booking.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "booking";

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.HasPostgresEnum<VehicleGroup>(DefaultSchema, "vehicle_group");
        modelBuilder.HasPostgresEnum<ReservationType>(DefaultSchema, "reservation_type");
        modelBuilder.HasPostgresEnum<ReservationStatus>(DefaultSchema, "reservation_status");
        modelBuilder.HasPostgresEnum<CancelReason>(DefaultSchema, "cancel_reason");
        modelBuilder.HasPostgresEnum<PrepaidOutcome>(DefaultSchema, "prepaid_outcome");
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
