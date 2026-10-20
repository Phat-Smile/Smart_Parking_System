using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class RegisterParkingLotTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Handler_creates_pending_lot_with_authenticated_owner()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository);

        var result = await handler.Handle(
            new RegisterParkingLotCommand(
                OwnerId,
                "  Riverside Lot  ",
                "  12 Demo Street ",
                10.776530m,
                106.700981m,
                "outdoor"),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(OwnerId, result.OwnerUserId);
        Assert.Equal("Riverside Lot", result.Name);
        Assert.Equal("12 Demo Street", result.Address);
        Assert.Equal("Outdoor", result.Type);
        Assert.Equal("Pending", result.ApprovalStatus);
        Assert.NotNull(repository.Saved);
        Assert.Equal(result.Id, repository.Saved.Id);
        Assert.Equal(ParkingLotApprovalStatus.Pending, repository.Saved.ApprovalStatus);
    }

    [Theory]
    [InlineData("", "Address", "Outdoor")]
    [InlineData("   ", "Address", "Outdoor")]
    [InlineData("Lot", "", "Outdoor")]
    [InlineData("Lot", "Address", "123")]
    [InlineData("Lot", "Address", "Unknown")]
    public async Task Validator_rejects_invalid_text(string name, string address, string type)
    {
        var validator = new RegisterParkingLotCommandValidator();
        var result = await validator.ValidateAsync(
            new RegisterParkingLotCommand(OwnerId, name, address, 10.5m, 106.5m, type),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_rejects_missing_owner_and_coordinates_out_of_range()
    {
        var validator = new RegisterParkingLotCommandValidator();
        var result = await validator.ValidateAsync(
            new RegisterParkingLotCommand(Guid.Empty, "Lot", "Address", 91m, -181m, "Outdoor"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterParkingLotCommand.OwnerUserId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterParkingLotCommand.Latitude));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterParkingLotCommand.Longitude));
    }

    [Fact]
    public void Domain_rejects_invalid_owner_without_creating_a_lot()
    {
        Assert.Throws<ArgumentException>(() => ParkingLot.Register(
            Guid.NewGuid(),
            Guid.Empty,
            "Lot",
            "Address",
            10.5m,
            106.5m,
            ParkingLotType.Outdoor));
    }

    [Fact]
    public async Task Validator_rejects_oversized_names_and_addresses()
    {
        var validator = new RegisterParkingLotCommandValidator();
        var result = await validator.ValidateAsync(
            new RegisterParkingLotCommand(
                OwnerId,
                new string('N', ParkingLot.NameMaxLength + 1),
                new string('A', ParkingLot.AddressMaxLength + 1),
                10m,
                106m,
                "Outdoor"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_requires_coordinates_even_when_the_json_omits_them()
    {
        var validator = new RegisterParkingLotCommandValidator();
        var result = await validator.ValidateAsync(
            new RegisterParkingLotCommand(OwnerId, "Lot", "Address", null, null, "Outdoor"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterParkingLotCommand.Latitude));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterParkingLotCommand.Longitude));
    }

    [Fact]
    public async Task Handler_rejects_a_second_registration_with_the_same_owner_and_name()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository);
        var command = new RegisterParkingLotCommand(
            OwnerId, "  Duplicate Lot  ", "15 Demo Street", 10m, 106m, "Outdoor");

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DuplicateParkingLotException>(() => handler.Handle(
            command with { Name = "Duplicate Lot" }, TestContext.Current.CancellationToken));

        Assert.Equal(first.Id, repository.Saved!.Id);
        Assert.Single(repository.Lots);
    }

    [Fact]
    public async Task Handler_allows_same_name_for_different_owners()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository);
        var command = new RegisterParkingLotCommand(
            OwnerId, "Shared Name", "15 Demo Street", 10m, 106m, "Outdoor");

        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command with { OwnerUserId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        Assert.Equal(2, repository.Lots.Count);
    }

    private sealed class FakeRepository : IRegisterParkingLotRepository
    {
        public List<ParkingLot> Lots { get; } = [];
        public ParkingLot? Saved { get; private set; }

        public Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken)
            => Task.FromResult(Lots.Any(lot => lot.OwnerUserId == ownerUserId && lot.Name == name));

        public Task AddAsync(ParkingLot parkingLot, CancellationToken cancellationToken)
        {
            Saved = parkingLot;
            Lots.Add(parkingLot);
            return Task.CompletedTask;
        }
    }
}
