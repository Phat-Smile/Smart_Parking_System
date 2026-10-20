using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Infrastructure.VehicleLookup;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class LookupVehicleByPlateQueryHandlerTests
{
    private readonly ILicensePlateNormalizer _normalizer = new LicensePlateNormalizer();
    private readonly IVehicleLookupReader _reader = new StubVehicleLookupReader();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_returns_failure_when_license_plate_is_empty(string? emptyPlate)
    {
        var handler = new LookupVehicleByPlateQueryHandler(_reader, _normalizer);
        var query = new LookupVehicleByPlateQuery(emptyPlate!);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LookupVehicleErrors.EmptyPlate.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("51-A")]
    [InlineData("!@#$$%")]
    [InlineData("51A123")]
    public async Task Handle_returns_failure_when_plate_format_is_invalid(string invalidPlate)
    {
        var handler = new LookupVehicleByPlateQueryHandler(_reader, _normalizer);
        var query = new LookupVehicleByPlateQuery(invalidPlate);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LookupVehicleErrors.InvalidPlateFormat.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("51-A 123.45")]
    [InlineData("51a-12345")]
    [InlineData("51A12345")]
    public async Task Handle_returns_registered_car_when_formatted_plate_matches(string rawPlate)
    {
        var handler = new LookupVehicleByPlateQueryHandler(_reader, _normalizer);
        var query = new LookupVehicleByPlateQuery(rawPlate);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsRegistered);
        Assert.Equal(VehicleType.Car, result.Value.VehicleType);
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), result.Value.AccountId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), result.Value.VehicleId);
        Assert.True(result.Value.IsAccountActive);
    }

    [Theory]
    [InlineData("59-X1 123.45")]
    [InlineData("59x112345")]
    public async Task Handle_returns_registered_motorbike_when_formatted_plate_matches(string rawPlate)
    {
        var handler = new LookupVehicleByPlateQueryHandler(_reader, _normalizer);
        var query = new LookupVehicleByPlateQuery(rawPlate);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsRegistered);
        Assert.Equal(VehicleType.Motorbike, result.Value.VehicleType);
    }

    [Fact]
    public async Task Handle_returns_unregistered_when_valid_format_plate_is_not_in_registry()
    {
        var handler = new LookupVehicleByPlateQueryHandler(_reader, _normalizer);
        var query = new LookupVehicleByPlateQuery("51-B 999.99");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.IsRegistered);
        Assert.Null(result.Value.AccountId);
        Assert.Null(result.Value.VehicleId);
        Assert.Null(result.Value.VehicleType);
        Assert.False(result.Value.IsAccountActive);
    }
}
