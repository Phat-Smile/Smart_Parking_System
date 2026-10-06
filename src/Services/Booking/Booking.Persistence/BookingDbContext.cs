using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence.Outbox;
using ParkingSystem.Booking.Persistence.Inbox;

namespace ParkingSystem.Booking.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    internal const string DefaultSchema = "booking";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationCapacity> ReservationCapacities => Set<ReservationCapacity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
