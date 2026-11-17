using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Application.Profiles.UpsertProfile;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Application.Vehicles.AddVehicle;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Application.Vehicles.GetVehicle;
using ParkingSystem.Identity.Application.Vehicles.UpdateVehicle;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Domain.Vehicles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Profiles;

// US-PROFILE-001/002 and US-VEH-001. Business rules: full name is the only required profile field; phone, CCCD and
// driving licence are optional and never block anything; a plate belongs to exactly one account.
public sealed class ProfileValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    private static UpsertProfileCommandValidator Validator() => new(new FixedClock(Now));

    private static UpsertProfileCommand Command(
        string fullName = "Nguyen Van A",
        DateOnly? dob = null,
        string? cccd = null,
        string? license = null,
        string? avatar = null) => new(Guid.NewGuid(), fullName, dob, cccd, license, avatar);

    [Fact]
    public void Only_the_full_name_is_required()
    {
        Assert.True(Validator().Validate(Command()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_full_name_is_rejected(string name) =>
        Assert.False(Validator().Validate(Command(fullName: name)).IsValid);

    [Fact]
    public void Full_name_accepts_100_characters_and_rejects_101()
    {
        Assert.True(Validator().Validate(Command(fullName: new string('a', 100))).IsValid);
        Assert.False(Validator().Validate(Command(fullName: new string('a', 101))).IsValid);
    }

    [Fact]
    public void Birth_date_in_the_future_is_rejected()
    {
        Assert.False(Validator().Validate(Command(dob: new DateOnly(2026, 10, 20))).IsValid);
        Assert.True(Validator().Validate(Command(dob: new DateOnly(2026, 10, 19))).IsValid);
    }

    [Fact]
    public void Birth_date_more_than_120_years_ago_is_rejected()
    {
        Assert.False(Validator().Validate(Command(dob: new DateOnly(1900, 1, 1))).IsValid);
        Assert.True(Validator().Validate(Command(dob: new DateOnly(1950, 1, 1))).IsValid);
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.png", true)]
    [InlineData("http://localhost:3000/a.png", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=", false)]
    [InlineData("ftp://example.com/a.png", false)]
    [InlineData("/relative/a.png", false)]
    [InlineData("not a url", false)]
    public void Avatar_must_be_an_absolute_http_or_https_url(string avatar, bool valid) =>
        Assert.Equal(valid, Validator().Validate(Command(avatar: avatar)).IsValid);

    [Fact]
    public void Optional_identity_numbers_have_a_length_limit_but_are_never_required()
    {
        Assert.True(Validator().Validate(Command(cccd: null, license: null)).IsValid);
        Assert.False(Validator().Validate(Command(cccd: new string('1', 21))).IsValid);
        Assert.False(Validator().Validate(Command(license: new string('1', 21))).IsValid);
    }

    [Fact]
    public void Phone_and_address_are_validated()
    {
        Assert.True(Validator().Validate(new UpsertProfileCommand(Guid.NewGuid(), "User", null, null, null, null, "+84901234567", "Hanoi")).IsValid);
        Assert.False(Validator().Validate(new UpsertProfileCommand(Guid.NewGuid(), "User", null, null, null, null, "abc", null)).IsValid);
        Assert.False(Validator().Validate(new UpsertProfileCommand(Guid.NewGuid(), "User", null, null, null, null, null, new string('x', 501))).IsValid);
    }
}

public sealed class ProfileDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Blank_optional_fields_are_stored_as_null_so_they_read_as_removed()
    {
        var profile = Profile.Create(Guid.NewGuid(), "  Nguyen Van A  ", Now, null, "   ", "", "  ");

        Assert.Equal("Nguyen Van A", profile.FullName);
        Assert.Null(profile.CitizenIdNumber);
        Assert.Null(profile.DriverLicenseNumber);
        Assert.Null(profile.AvatarUrl);
    }

    [Fact]
    public void Update_replaces_every_field_and_blank_clears_an_optional_one()
    {
        var profile = Profile.Create(Guid.NewGuid(), "Old", Now, new DateOnly(1990, 1, 1), "012345678901", "B123456", "https://a.example/x.png");

        profile.Update("New Name", Now.AddDays(1), null, "  ", null, null);

        Assert.Equal("New Name", profile.FullName);
        Assert.Null(profile.DateOfBirth);
        Assert.Null(profile.CitizenIdNumber);
        Assert.Null(profile.DriverLicenseNumber);
        Assert.Equal(Now.AddDays(1), profile.UpdatedAtUtc);
    }

    [Fact]
    public void ClearIdentity_removes_both_cccd_and_driver_license_and_updates_timestamp()
    {
        var profile = Profile.Create(Guid.NewGuid(), "User", Now, new DateOnly(1990, 1, 1), "012345678901", "B123456", "https://a.example/x.png");

        profile.ClearIdentity(Now.AddHours(2));

        Assert.Equal("User", profile.FullName);
        Assert.Equal(new DateOnly(1990, 1, 1), profile.DateOfBirth);
        Assert.Null(profile.CitizenIdNumber);
        Assert.Null(profile.DriverLicenseNumber);
        Assert.Equal("https://a.example/x.png", profile.AvatarUrl);
        Assert.Equal(Now.AddHours(2), profile.UpdatedAtUtc);
    }

    [Fact]
    public void Address_is_trimmed_and_can_be_cleared()
    {
        var profile = Profile.Create(Guid.NewGuid(), "User", Now, address: "  Hanoi  ");
        Assert.Equal("Hanoi", profile.Address);
        profile.Update("User", Now.AddDays(1), address: " ");
        Assert.Null(profile.Address);
    }
}

public sealed class AddVehicleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Theory]
    [InlineData("51a-123.45", "51A12345")]
    [InlineData("  51A 123 45 ", "51A12345")]
    [InlineData("30f.999.99", "30F99999")]
    public async Task Plate_is_stored_in_the_same_canonical_form_the_lookup_uses(string raw, string expected)
    {
        var vehicles = new FakeVehicles();
        var result = await Handler(vehicles).Handle(new AddVehicleCommand(OwnerId, raw, "Car", null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value!.LicensePlate);
        Assert.Equal(expected, Assert.Single(vehicles.Items).LicensePlate);
    }

    [Fact]
    public async Task A_plate_written_differently_is_still_a_duplicate()
    {
        var vehicles = new FakeVehicles();
        vehicles.Items.Add(Vehicle.Add(Guid.NewGuid(), Guid.NewGuid(), "51A12345", VehicleDomainType.Car, Now));

        var result = await Handler(vehicles).Handle(new AddVehicleCommand(OwnerId, "51a-123.45", "Car", null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AddVehicleErrors.PlateAlreadyRegistered.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Losing_the_race_for_a_plate_is_reported_as_a_conflict_not_thrown()
    {
        var vehicles = new FakeVehicles { SaveResult = SaveVehicleResult.DuplicatePlateConflict };

        var result = await Handler(vehicles).Handle(new AddVehicleCommand(OwnerId, "51A12345", "Car", null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AddVehicleErrors.PlateAlreadyRegistered.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("!!!@@@")]
    [InlineData("A1")]
    [InlineData("51A 123 45 /*")]
    [InlineData("51A1234567890123")]
    public async Task Plates_that_cannot_be_a_plate_are_rejected(string raw)
    {
        var vehicles = new FakeVehicles();

        var result = await Handler(vehicles).Handle(new AddVehicleCommand(OwnerId, raw, "Car", null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AddVehicleErrors.InvalidLicensePlate.Code, result.Error!.Code);
        Assert.Empty(vehicles.Items);
    }

    [Theory]
    [InlineData("Car", true)]
    [InlineData("car", true)]
    [InlineData("MOTORBIKE", true)]
    [InlineData("Bicycle", false)]
    [InlineData("1", false)]
    [InlineData("0", false)]
    [InlineData("99", false)]
    [InlineData(" Car", false)]
    [InlineData("Car,Motorbike", false)]
    [InlineData("Truck", false)]
    public async Task Vehicle_type_must_be_a_named_value_not_a_number(string type, bool accepted)
    {
        var result = await Handler(new FakeVehicles()).Handle(new AddVehicleCommand(OwnerId, "51A12345", type, null), TestContext.Current.CancellationToken);

        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted)
            Assert.Equal(AddVehicleErrors.InvalidVehicleType.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Unknown_account_is_reported()
    {
        var handler = new AddVehicleCommandHandler(new FakeAccounts(null), new FakeVehicles(), new FixedClock(Now), new LicensePlateNormalizer());

        var result = await handler.Handle(new AddVehicleCommand(Guid.NewGuid(), "51A12345", "Car", null), TestContext.Current.CancellationToken);

        Assert.Equal(AddVehicleErrors.AccountNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Bicycle_registration_is_rejected()
    {
        var vehicles = new FakeVehicles();
        var result = await Handler(vehicles).Handle(
            new AddVehicleCommand(OwnerId, null, "Bicycle", null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AddVehicleErrors.InvalidVehicleType.Code, result.Error!.Code);
        Assert.Empty(vehicles.Items);
        Assert.Throws<ArgumentException>(() =>
            Vehicle.Add(Guid.NewGuid(), OwnerId, null, VehicleDomainType.Bicycle, Now));
    }

    private static AddVehicleCommandHandler Handler(FakeVehicles vehicles) =>
        new(new FakeAccounts(Account.Register(OwnerId, "owner@example.com", "hash", null, Now)), vehicles, new FixedClock(Now), new LicensePlateNormalizer());
}

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
}

internal sealed class FakeAccounts(Account? account) : IAccountRepository
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

internal sealed class FakeVehicles : IVehicleRepository
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

public sealed class CustomerVehicleDetailsTests
{
    [Fact]
    public void Electric_car_can_use_the_system_illustration_without_an_uploaded_photo()
    {
        var details = new VehicleDetails(null, null, null, null, "electric", null, null, "standard",
            "/images/parkwise/vehicle-electric-car-cutout.png");

        Assert.True(VehicleDetailsValidation.IsSystemVehicleImageUrl(details.PhotoUrl));
        Assert.True(new AddVehicleCommandValidator().Validate(
            new AddVehicleCommand(Guid.NewGuid(), "55A44555", "Car", null, details)).IsValid);
    }

    [Fact]
    public async Task Owner_can_update_and_read_vehicle_details()
    {
        var ownerId = Guid.NewGuid();
        var vehicle = Vehicle.Add(Guid.NewGuid(), ownerId, "51A12345", VehicleDomainType.Car, DateTimeOffset.UtcNow);
        var repo = new FakeVehicles();
        repo.Add(vehicle);
        var details = new VehicleDetails("Toyota", "Vios", null, 5, "gasoline", "white", 2020, "standard", null);

        var updated = await new UpdateVehicleCommandHandler(repo).Handle(
            new UpdateVehicleCommand(ownerId, vehicle.Id, "Family car", details), TestContext.Current.CancellationToken);
        var read = await new GetVehicleQueryHandler(repo).Handle(
            new GetVehicleQuery(ownerId, vehicle.Id), TestContext.Current.CancellationToken);

        Assert.True(updated.IsSuccess);
        Assert.True(read.IsSuccess);
        Assert.Equal("Toyota", read.Value!.Details!.Brand);
        Assert.Equal("Family car", vehicle.NickName);
        Assert.Equal("51A12345", vehicle.LicensePlate);
    }

    [Fact]
    public async Task Another_account_cannot_read_or_update_vehicle()
    {
        var vehicle = Vehicle.Add(Guid.NewGuid(), Guid.NewGuid(), "51A12345", VehicleDomainType.Car, DateTimeOffset.UtcNow);
        var repo = new FakeVehicles();
        repo.Add(vehicle);
        var strangerId = Guid.NewGuid();
        var details = new VehicleDetails("Stolen", null, null, null, null, null, null, null, null);

        var updated = await new UpdateVehicleCommandHandler(repo).Handle(
            new UpdateVehicleCommand(strangerId, vehicle.Id, null, details), TestContext.Current.CancellationToken);
        var read = await new GetVehicleQueryHandler(repo).Handle(
            new GetVehicleQuery(strangerId, vehicle.Id), TestContext.Current.CancellationToken);

        Assert.False(updated.IsSuccess);
        Assert.False(read.IsSuccess);
        Assert.Null(vehicle.Brand);
    }
}
