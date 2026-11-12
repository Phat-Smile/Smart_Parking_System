using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class SearchCustomerLotsTests
{
    private static readonly SearchCustomerLotsQueryValidator Validator = new();

    [Fact]
    public void Accepts_vehicle_price_and_availability_filters()
    {
        Assert.True(Validator.Validate(new SearchCustomerLotsQuery(null, null, 5, VehicleType.Car,
            50000, true, "price")).IsValid);
    }

    [Fact]
    public void Rejects_invalid_coordinates_price_and_distance_sort()
    {
        Assert.False(Validator.Validate(new SearchCustomerLotsQuery(10m, null, 5, null,
            null, false, "recommended")).IsValid);
        Assert.False(Validator.Validate(new SearchCustomerLotsQuery(null, null, 5, null,
            -1, false, "price")).IsValid);
        Assert.False(Validator.Validate(new SearchCustomerLotsQuery(null, null, 5, null,
            null, false, "distance")).IsValid);
    }
}
