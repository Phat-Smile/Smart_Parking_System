using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationArrivalReminderTracker(BookingDbContext dbContext) : IReservationArrivalReminderTracker
{
    private static readonly ConcurrentDictionary<string, byte> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> HasBeenDispatchedAsync(Guid reservationId, string reminderType, CancellationToken cancellationToken)
    {
        var key = $"{reservationId:D}:{reminderType}";
        if (MemoryCache.ContainsKey(key))
            return true;

        var existsInOutbox = await dbContext.OutboxMessages.AsNoTracking().AnyAsync(
            m => m.CorrelationId == reservationId &&
                 m.EventType == "booking.reservation-arrival-reminder.v1" &&
                 m.Payload.Contains(reminderType),
            cancellationToken);

        if (existsInOutbox)
        {
            MemoryCache.TryAdd(key, 0);
            return true;
        }

        return false;
    }

    public Task MarkDispatchedAsync(Guid reservationId, string reminderType, CancellationToken cancellationToken)
    {
        var key = $"{reservationId:D}:{reminderType}";
        MemoryCache.TryAdd(key, 0);
        return Task.CompletedTask;
    }
}
