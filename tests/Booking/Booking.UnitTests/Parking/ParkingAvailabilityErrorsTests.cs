using ParkingSystem.Booking.Application.Parking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Parking;

public sealed class ParkingAvailabilityErrorsTests
{
    [Fact]
    public void Error_codes_are_unique_and_follow_namespace_conventions()
    {
        var errors = new[]
        {
            ParkingAvailabilityErrors.EmptyLotId,
            ParkingAvailabilityErrors.LotNotFound,
            ParkingAvailabilityErrors.ServiceUnavailable,
            ParkingAvailabilityErrors.DeadlineExceeded,
            ParkingAvailabilityErrors.RequestCancelled,
            ParkingAvailabilityErrors.InvalidResponse,
            ParkingAvailabilityErrors.CommunicationFailed
        };

        var codes = errors.Select(e => e.Code).ToList();
        Assert.Equal(codes.Distinct().Count(), codes.Count);

        foreach (var error in errors)
        {
            Assert.StartsWith("booking.parking.", error.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }
    }
}
