using ParkingSystem.Parking.Application.ParkingLots.PublicInfo;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ConfigurePublicInfoTests
{
    private static ConfigurePublicInfoCommand Command(ParkingLotPublicInfo info) =>
        new(Guid.NewGuid(), Guid.NewGuid(), info);

    private static ParkingLotPublicInfo Valid() => new(
        "[DEMO] Public parking description",
        "[DEMO] Follow signs to the entrance",
        ["[DEMO] Follow staff directions"],
        ["/images/parkwise/dashboard-parking-structure.webp"],
        true,
        ["[DEMO] Cash"],
        2);

    [Fact]
    public void Owner_profile_validator_accepts_references_to_public_assets()
    {
        var result = new ConfigurePublicInfoCommandValidator().Validate(Command(Valid()));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("https://third-party.example/vehicle-owner.jpg")]
    [InlineData("/images/parkwise/../../private-document.png")]
    [InlineData("/uploads/private-identity.jpeg")]
    public void Owner_profile_validator_rejects_unsafe_photos(string url)
    {
        var invalid = Valid() with { PhotoUrls = [url] };
        Assert.False(new ConfigurePublicInfoCommandValidator().Validate(Command(invalid)).IsValid);
    }

    [Fact]
    public void Owner_profile_validator_rejects_invalid_ev_charger_count()
    {
        var invalid = Valid() with { EvChargingPoints = -1 };
        Assert.False(new ConfigurePublicInfoCommandValidator().Validate(Command(invalid)).IsValid);
    }

    [Fact]
    public void Domain_saves_owner_profile_as_valid_json_without_approving_lot()
    {
        var lot = ParkingLot.Register(Guid.NewGuid(), Guid.NewGuid(), "QA lot", "1 Demo street",
            10.7m, 106.7m, ParkingLotType.Outdoor);
        lot.SetPublicInfo(Valid());
        Assert.Contains("photoUrls", lot.PublicInfoJson);
        Assert.Equal(ParkingLotApprovalStatus.Pending, lot.ApprovalStatus);
    }
}
