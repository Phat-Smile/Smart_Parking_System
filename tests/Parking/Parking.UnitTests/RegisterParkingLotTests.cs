using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class RegisterParkingLotTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handler_creates_pending_lot_with_authenticated_owner()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository, new FixedClock(Now));

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
        var handler = new RegisterParkingLotCommandHandler(repository, new FixedClock(Now));
        var command = new RegisterParkingLotCommand(
            OwnerId, "  Duplicate Lot  ", "15 Demo Street", 10m, 106m, "Outdoor");

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DuplicateParkingLotException>(() => handler.Handle(
            command with { Name = "Duplicate Lot" }, TestContext.Current.CancellationToken));

        Assert.Equal(first.Id, repository.Saved!.Id);
        Assert.Single(repository.Lots);
        Assert.Single(repository.OwnerAssignments);
    }

    [Fact]
    public async Task Handler_allows_same_name_for_different_owners()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository, new FixedClock(Now));
        var command = new RegisterParkingLotCommand(
            OwnerId, "Shared Name", "15 Demo Street", 10m, 106m, "Outdoor");

        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command with { OwnerUserId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        Assert.Equal(2, repository.Lots.Count);
    }

    // MOD-PK-04: the registrant is the owner of the lot, and ownership is the Owner assignment row (TECH-12 table).
    [Fact]
    public async Task Handler_gives_the_registrant_an_owner_assignment_for_the_new_lot()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository, new FixedClock(Now));

        var result = await handler.Handle(
            new RegisterParkingLotCommand(OwnerId, "Owner Row Lot", "12 Demo Street", 10.7m, 106.7m, "Outdoor"),
            TestContext.Current.CancellationToken);

        var assignment = Assert.IsType<ParkingLotAssignment>(repository.SavedOwnerAssignment);
        Assert.Equal(result.Id, assignment.LotId);
        Assert.Equal(OwnerId, assignment.AccountId);
        Assert.Equal(ParkingLotMemberRole.Owner, assignment.Role);
        Assert.Equal(OwnerId, assignment.AssignedBy);
        Assert.Equal(Now, assignment.AssignedAtUtc);
    }

    [Fact]
    public async Task Each_registration_gets_its_own_owner_assignment_for_its_own_lot()
    {
        var repository = new FakeRepository();
        var handler = new RegisterParkingLotCommandHandler(repository, new FixedClock(Now));
        var secondOwner = Guid.NewGuid();

        var first = await handler.Handle(
            new RegisterParkingLotCommand(OwnerId, "First Lot", "1 Demo Street", 10m, 106m, "Outdoor"), TestContext.Current.CancellationToken);
        var second = await handler.Handle(
            new RegisterParkingLotCommand(secondOwner, "Second Lot", "2 Demo Street", 10m, 106m, "Underground"), TestContext.Current.CancellationToken);

        Assert.Equal(2, repository.OwnerAssignments.Count);
        Assert.Contains(repository.OwnerAssignments, a => a.LotId == first.Id && a.AccountId == OwnerId);
        Assert.Contains(repository.OwnerAssignments, a => a.LotId == second.Id && a.AccountId == secondOwner);
        Assert.All(repository.OwnerAssignments, a => Assert.Equal(ParkingLotMemberRole.Owner, a.Role));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeRepository : IRegisterParkingLotRepository
    {
        public List<ParkingLot> Lots { get; } = [];
        public List<ParkingLotAssignment> OwnerAssignments { get; } = [];
        public ParkingLot? Saved { get; private set; }
        public ParkingLotAssignment? SavedOwnerAssignment { get; private set; }

        public Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken)
            => Task.FromResult(Lots.Any(lot => lot.OwnerUserId == ownerUserId && lot.Name == name));

        public Task AddAsync(ParkingLot parkingLot, ParkingLotAssignment ownerAssignment, CancellationToken cancellationToken)
        {
            Saved = parkingLot;
            SavedOwnerAssignment = ownerAssignment;
            Lots.Add(parkingLot);
            OwnerAssignments.Add(ownerAssignment);
            return Task.CompletedTask;
        }
    }
}
