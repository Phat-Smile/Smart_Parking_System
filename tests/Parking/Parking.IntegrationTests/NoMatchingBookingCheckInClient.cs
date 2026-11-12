using ParkingSystem.Parking.Application.Common.Booking;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Test-only Booking contract: these scenarios do not have a matching reservation.
internal sealed class NoMatchingBookingCheckInClient : IBookingCheckInClient
{
    public Task<CheckedInReservation?> CheckInReservationAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset entryTime,
        Guid sessionId,
        CancellationToken cancellationToken) => Task.FromResult<CheckedInReservation?>(null);
}
