using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Application.Vehicles.DeleteVehicle;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Vehicles;
using ParkingSystem.Tests.Identity.UnitTests.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class DeleteVehicleCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    [Fact]
    public async Task DeleteVehicle_removes_vehicle_when_owned_by_current_user()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now);
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Add(vehicleId, OwnerId, "51A12345", VehicleDomainType.Car, Now);

        var vehicles = new FakeVehicles();
        vehicles.Add(vehicle);

        var handler = new DeleteVehicleCommandHandler(new FakeAccounts(account), vehicles);

        var result = await handler.Handle(new DeleteVehicleCommand(OwnerId, vehicleId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Phương tiện đã được xóa thành công.", result.Value!.Message);
        Assert.Empty(vehicles.Items);
    }

    [Fact]
    public async Task DeleteVehicle_rejects_when_vehicle_belongs_to_another_user_idor_attack()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now);
        var vehicleId = Guid.NewGuid();
        var vehicleOfOtherUser = Vehicle.Add(vehicleId, OtherUserId, "51B99999", VehicleDomainType.Car, Now);

        var vehicles = new FakeVehicles();
        vehicles.Add(vehicleOfOtherUser);

        var handler = new DeleteVehicleCommandHandler(new FakeAccounts(account), vehicles);

        var result = await handler.Handle(new DeleteVehicleCommand(OwnerId, vehicleId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeleteVehicleErrors.VehicleForbidden.Code, result.Error!.Code);
        Assert.Single(vehicles.Items);
    }

    [Fact]
    public async Task DeleteVehicle_returns_error_when_vehicle_does_not_exist()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now);
        var vehicles = new FakeVehicles();

        var handler = new DeleteVehicleCommandHandler(new FakeAccounts(account), vehicles);

        var result = await handler.Handle(new DeleteVehicleCommand(OwnerId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeleteVehicleErrors.VehicleNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task DeleteVehicle_returns_error_when_account_does_not_exist()
    {
        var vehicles = new FakeVehicles();
        var handler = new DeleteVehicleCommandHandler(new FakeAccounts(null), vehicles);

        var result = await handler.Handle(new DeleteVehicleCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeleteVehicleErrors.AccountNotFound.Code, result.Error!.Code);
    }
}
