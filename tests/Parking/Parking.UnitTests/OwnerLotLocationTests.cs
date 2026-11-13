using Microsoft.AspNetCore.DataProtection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLocation;
using ParkingSystem.Parking.WebAPI.OwnerLocation;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerLotLocationTests
{
    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Resolve_trims_address_and_returns_one_approximate_candidate()
    {
        var geocoder = new FakeGeocoder(new GeocodedDestination("Hải Triều", 10.771m, 106.704m));
        var handler = new ResolveOwnerLotAddressQueryHandler(geocoder);
        var result = await handler.Handle(new ResolveOwnerLotAddressQuery(" 2 Hải Triều "), TestContext.Current.CancellationToken);

        Assert.Equal("2 Hải Triều", geocoder.LastQuery);
        Assert.Equal("2 Hải Triều", result.Address);
        Assert.True(result.ManualPinAllowed);
        Assert.Single(result.Candidates);
        Assert.Equal(10.771m, result.Candidates[0].Latitude);
    }

    [Fact]
    public async Task Resolve_not_found_allows_manual_pin_instead_of_returning_not_found()
    {
        var handler = new ResolveOwnerLotAddressQueryHandler(new FakeGeocoder(null));
        var result = await handler.Handle(
            new ResolveOwnerLotAddressQuery("Unknown street in Vietnam"), TestContext.Current.CancellationToken);
        Assert.Empty(result.Candidates);
        Assert.True(result.ManualPinAllowed);
    }

    [Fact]
    public void Resolve_rejects_empty_short_and_long_addresses()
    {
        var validator = new ResolveOwnerLotAddressQueryValidator();
        Assert.False(validator.Validate(new ResolveOwnerLotAddressQuery("  ")).IsValid);
        Assert.False(validator.Validate(new ResolveOwnerLotAddressQuery("ab")).IsValid);
        Assert.False(validator.Validate(new ResolveOwnerLotAddressQuery(new string('x', 201))).IsValid);
        Assert.True(validator.Validate(new ResolveOwnerLotAddressQuery("2 Hải Triều")).IsValid);
    }

    [Theory]
    [InlineData(null, 10.77, 106.7, true)]
    [InlineData("2 Hải Triều", 91, 106.7, true)]
    [InlineData("2 Hải Triều", 10.77, -181, true)]
    [InlineData("2 Hải Triều", 0, 0, true)]
    [InlineData("2 Hải Triều", 10.77, 106.7, false)]
    public void Confirm_rejects_invalid_address_pin_or_unconfirmed_map(
        string? address, double latitude, double longitude, bool confirmed)
    {
        var errors = OwnerLotLocationRules.ValidateConfirmation(
            address, (decimal)latitude, (decimal)longitude, confirmed);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Confirm_accepts_manual_pin_without_geocoding_a_matching_address()
    {
        var errors = OwnerLotLocationRules.ValidateConfirmation("Địa chỉ Owner nhập", 10.771m, 106.704m, true);
        Assert.Empty(errors);
    }

    [Fact]
    public void Token_cannot_be_used_by_another_owner_or_for_a_modified_address_or_pin()
    {
        var service = new OwnerLotLocationConfirmationService(new EphemeralDataProtectionProvider());
        var confirmation = service.Issue(OwnerId, " 2 Hải Triều ", 10.771m, 106.704m);
        Assert.True(confirmation.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(service.MatchesRegistration(OwnerId, confirmation.LocationConfirmationToken,
            "2 Hải Triều", 10.771m, 106.704m, requireConfirmation: true));
        Assert.False(service.MatchesRegistration(Guid.NewGuid(), confirmation.LocationConfirmationToken,
            "2 Hải Triều", 10.771m, 106.704m, requireConfirmation: true));
        Assert.False(service.MatchesRegistration(OwnerId, confirmation.LocationConfirmationToken,
            "3 Hải Triều", 10.771m, 106.704m, requireConfirmation: true));
        Assert.False(service.MatchesRegistration(OwnerId, confirmation.LocationConfirmationToken,
            "2 Hải Triều", 10.772m, 106.704m, requireConfirmation: true));
        Assert.False(service.MatchesRegistration(OwnerId, confirmation.LocationConfirmationToken + "bad",
            "2 Hải Triều", 10.771m, 106.704m, requireConfirmation: true));
    }

    [Fact]
    public void Strict_mode_requires_token_but_legacy_mode_allows_none()
    {
        var service = new OwnerLotLocationConfirmationService(new EphemeralDataProtectionProvider());
        Assert.False(service.MatchesRegistration(OwnerId, null, "Address", 10m, 106m, true));
        Assert.True(service.MatchesRegistration(OwnerId, null, "Address", 10m, 106m, false));
        Assert.False(service.MatchesRegistration(OwnerId, "forged", "Address", 10m, 106m, false));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        var service = new OwnerLotLocationConfirmationService(dataProtection);
        var expired = dataProtection
            .CreateProtector(OwnerLotLocationConfirmationService.TokenPurpose)
            .ToTimeLimitedDataProtector()
            .Protect("{}", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(service.MatchesRegistration(OwnerId, expired, "Address", 10m, 106m, true));
    }

    private sealed class FakeGeocoder(GeocodedDestination? destination) : IAddressGeocoder
    {
        public string? LastQuery { get; private set; }
        public Task<GeocodedDestination?> GeocodeAsync(string address, CancellationToken cancellationToken)
        {
            LastQuery = address;
            return Task.FromResult(destination);
        }
    }
}
