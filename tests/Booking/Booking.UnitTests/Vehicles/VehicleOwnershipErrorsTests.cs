using ParkingSystem.Booking.Application.Vehicles;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Vehicles;

public sealed class VehicleOwnershipErrorsTests
{
    [Fact]
    public void Error_codes_are_unique_and_follow_namespace_conventions()
    {
        var errors = new[]
        {
            VehicleOwnershipErrors.EmptyPlate,
            VehicleOwnershipErrors.InvalidPlateFormat,
            VehicleOwnershipErrors.VehicleNotRegistered,
            VehicleOwnershipErrors.VehicleOwnershipMismatch,
            VehicleOwnershipErrors.UnsupportedVehicleType,
            VehicleOwnershipErrors.AccountNotEligible,
            VehicleOwnershipErrors.ServiceUnavailable,
            VehicleOwnershipErrors.DeadlineExceeded,
            VehicleOwnershipErrors.RequestCancelled,
            VehicleOwnershipErrors.CommunicationFailed,
            VehicleOwnershipErrors.ServiceAuthFailed
        };

        var codes = errors.Select(e => e.Code).ToList();
        Assert.Equal(codes.Distinct().Count(), codes.Count);

        foreach (var error in errors)
        {
            Assert.StartsWith("booking.vehicle.", error.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }
    }
}

