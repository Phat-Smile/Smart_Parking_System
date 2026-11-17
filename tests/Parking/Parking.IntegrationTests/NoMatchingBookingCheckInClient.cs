using ParkingSystem.Parking.Application.Common.Booking;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Test-only Booking contract: these scenarios do not have a matching reservation.
internal sealed class NoMatchingBookingCheckInClient : IBookingCheckInClient, IBookingNoSlotReporter
{
    public Task<Guid?> ReportNoSuitableSlotAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset arrivedAt,
        CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);

    public Task<CheckedInReservation?> CheckInReservationAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset entryTime,
        Guid sessionId,
        CancellationToken cancellationToken) => Task.FromResult<CheckedInReservation?>(null);
}
