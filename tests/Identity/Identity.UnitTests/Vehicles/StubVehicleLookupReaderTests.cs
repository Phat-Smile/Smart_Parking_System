using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Infrastructure.VehicleLookup;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class StubVehicleLookupReaderTests
{
    private readonly StubVehicleLookupReader _reader = new();

    [Fact]
    public async Task FindByPlateAsync_returns_registered_car_for_deterministic_plate()
    {
        var result = await _reader.FindByPlateAsync("51A12345", CancellationToken.None);

        Assert.True(result.IsRegistered);
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), result.AccountId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), result.VehicleId);
        Assert.Equal("Nguyễn Văn An", result.OwnerFullName);
        Assert.Equal(VehicleType.Car, result.VehicleType);
        Assert.True(result.IsAccountActive);
    }

    [Fact]
    public async Task FindByPlateAsync_returns_registered_motorbike_for_deterministic_plate()
    {
        var result = await _reader.FindByPlateAsync("59X112345", CancellationToken.None);

        Assert.True(result.IsRegistered);
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000002"), result.AccountId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000002"), result.VehicleId);
        Assert.Equal("Trần Thị Bình", result.OwnerFullName);
        Assert.Equal(VehicleType.Motorbike, result.VehicleType);
        Assert.True(result.IsAccountActive);
    }

    [Theory]
    [InlineData("51a12345")]
    [InlineData("59x112345")]
    public async Task FindByPlateAsync_is_case_insensitive(string plate)
    {
        var result = await _reader.FindByPlateAsync(plate, CancellationToken.None);

        Assert.True(result.IsRegistered);
        Assert.NotNull(result.AccountId);
        Assert.NotNull(result.VehicleId);
    }

    [Theory]
    [InlineData("51B99999")]
    [InlineData("29A11111")]
    [InlineData("UNKNOWN")]
    public async Task FindByPlateAsync_returns_unregistered_for_unknown_plate(string unknownPlate)
    {
        var result = await _reader.FindByPlateAsync(unknownPlate, CancellationToken.None);

        Assert.False(result.IsRegistered);
        Assert.Null(result.AccountId);
        Assert.Null(result.VehicleId);
        Assert.Null(result.OwnerFullName);
        Assert.Null(result.VehicleType);
        Assert.False(result.IsAccountActive);
    }

    [Fact]
    public async Task FindByPlateAsync_respects_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _reader.FindByPlateAsync("51A12345", cts.Token));
    }
}
