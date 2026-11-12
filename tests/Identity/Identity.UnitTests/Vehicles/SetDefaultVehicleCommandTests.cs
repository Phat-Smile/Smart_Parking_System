using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Application.Vehicles.AddVehicle;
using ParkingSystem.Identity.Application.Vehicles.DeleteVehicle;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Application.Vehicles.SetDefaultVehicle;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Vehicles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class SetDefaultVehicleCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();

    [Fact]
    public async Task First_vehicle_added_is_automatically_marked_as_default()
    {
        var vehicles = new FakeVehicles();
        var handler = CreateAddHandler(vehicles);

        var first = await handler.Handle(new AddVehicleCommand(AccountId, "51A11111", "Car", "Xe 1"), TestContext.Current.CancellationToken);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value!.IsDefault);

        var second = await handler.Handle(new AddVehicleCommand(AccountId, "51A22222", "Car", "Xe 2"), TestContext.Current.CancellationToken);
        Assert.True(second.IsSuccess);
        Assert.False(second.Value!.IsDefault);
    }

    [Fact]
    public async Task Setting_vehicle_as_default_unsets_previous_default()
    {
        var vehicles = new FakeVehicles();
        var addHandler = CreateAddHandler(vehicles);

        var first = await addHandler.Handle(new AddVehicleCommand(AccountId, "51A11111", "Car", "Xe 1"), TestContext.Current.CancellationToken);
        var second = await addHandler.Handle(new AddVehicleCommand(AccountId, "51A22222", "Car", "Xe 2"), TestContext.Current.CancellationToken);

        var setHandler = CreateSetDefaultHandler(vehicles);
        var result = await setHandler.Handle(new SetDefaultVehicleCommand(AccountId, second.Value!.VehicleId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        var v1 = vehicles.Items.First(v => v.Id == first.Value!.VehicleId);
        var v2 = vehicles.Items.First(v => v.Id == second.Value!.VehicleId);

        Assert.False(v1.IsDefault);
        Assert.True(v2.IsDefault);
    }

    [Fact]
    public async Task Setting_non_existent_vehicle_returns_not_found()
    {
        var vehicles = new FakeVehicles();
        var setHandler = CreateSetDefaultHandler(vehicles);

        var result = await setHandler.Handle(new SetDefaultVehicleCommand(AccountId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(SetDefaultVehicleErrors.VehicleNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Setting_other_users_vehicle_returns_not_found()
    {
        var vehicles = new FakeVehicles();
        var vehicleOfOther = Vehicle.Add(Guid.NewGuid(), OtherAccountId, "51B99999", VehicleDomainType.Car, Now);
        vehicles.Items.Add(vehicleOfOther);

        var setHandler = CreateSetDefaultHandler(vehicles);
        var result = await setHandler.Handle(new SetDefaultVehicleCommand(AccountId, vehicleOfOther.Id), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(SetDefaultVehicleErrors.VehicleNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Deleting_default_vehicle_promotes_next_remaining_vehicle()
    {
        var vehicles = new FakeVehicles();
        var addHandler = CreateAddHandler(vehicles);

        var first = await addHandler.Handle(new AddVehicleCommand(AccountId, "51A11111", "Car", "Xe 1"), TestContext.Current.CancellationToken);
        var second = await addHandler.Handle(new AddVehicleCommand(AccountId, "51A22222", "Car", "Xe 2"), TestContext.Current.CancellationToken);

        Assert.True(vehicles.Items.First(v => v.Id == first.Value!.VehicleId).IsDefault);
        Assert.False(vehicles.Items.First(v => v.Id == second.Value!.VehicleId).IsDefault);

        var deleteHandler = CreateDeleteHandler(vehicles);
        var deleteResult = await deleteHandler.Handle(new DeleteVehicleCommand(AccountId, first.Value!.VehicleId), TestContext.Current.CancellationToken);

        Assert.True(deleteResult.IsSuccess);
        Assert.Single(vehicles.Items);
        Assert.True(vehicles.Items.First().IsDefault);
    }

    private static AddVehicleCommandHandler CreateAddHandler(FakeVehicles vehicles) =>
        new(new FakeAccounts(Account.Register(AccountId, "user@example.com", "hash", null, Now)), vehicles, new FixedClock(Now), new LicensePlateNormalizer());

    private static SetDefaultVehicleCommandHandler CreateSetDefaultHandler(FakeVehicles vehicles) =>
        new(new FakeAccounts(Account.Register(AccountId, "user@example.com", "hash", null, Now)), vehicles);

    private static DeleteVehicleCommandHandler CreateDeleteHandler(FakeVehicles vehicles) =>
        new(new FakeAccounts(Account.Register(AccountId, "user@example.com", "hash", null, Now)), vehicles);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeAccounts(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(account is not null && account.Id == id ? account : null);
        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<bool> EmailExistsAsync(string e, CancellationToken ct) => Task.FromResult(false);
        public Task<Account?> FindByEmailAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) => action();
    }

    private sealed class FakeVehicles : IVehicleRepository
    {
        public List<Vehicle> Items { get; } = new();
        public SaveVehicleResult SaveResult { get; set; } = SaveVehicleResult.Success;

        public void Add(Vehicle vehicle) => Items.Add(vehicle);
        public void Remove(Vehicle vehicle) => Items.Remove(vehicle);
        public Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id));
        public Task<Vehicle?> FindByAccountAndPlateAsync(Guid accountId, string plate, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(v => v.AccountId == accountId && v.LicensePlate == plate));
        public Task<IReadOnlyList<Vehicle>> ListByAccountIdAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Vehicle>>(Items.Where(v => v.AccountId == accountId).ToList());
        public Task<bool> PlateExistsAsync(string plate, CancellationToken ct) => Task.FromResult(Items.Any(v => v.LicensePlate == plate));
        public Task<SaveVehicleResult> SaveNewVehicleAsync(CancellationToken ct)
        {
            if (SaveResult != SaveVehicleResult.Success && Items.Count > 0)
                Items.RemoveAt(Items.Count - 1);
            return Task.FromResult(SaveResult);
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
