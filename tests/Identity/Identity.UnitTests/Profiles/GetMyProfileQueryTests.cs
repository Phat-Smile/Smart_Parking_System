using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Application.Profiles.GetMyProfile;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Profiles;

public sealed class GetMyProfileQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public async Task GetMyProfile_returns_profile_when_it_exists()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);
        var profile = Profile.Create(
            AccountId,
            "Nguyen Van Test",
            Now,
            new DateOnly(1995, 5, 20),
            "079201234567",
            "B2-123456",
            "https://example.com/avatar.png");

        var handler = new GetMyProfileQueryHandler(
            new FakeAccounts(account),
            new FakeProfiles(profile));

        var result = await handler.Handle(new GetMyProfileQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountId, result.Value!.AccountId);
        Assert.Equal("Nguyen Van Test", result.Value.FullName);
        Assert.Equal(new DateOnly(1995, 5, 20), result.Value.DateOfBirth);
        Assert.Equal("079201234567", result.Value.CitizenIdNumber);
        Assert.Equal("B2-123456", result.Value.DriverLicenseNumber);
        Assert.Equal("https://example.com/avatar.png", result.Value.AvatarUrl);
    }

    [Fact]
    public async Task GetMyProfile_returns_default_profile_when_profile_not_yet_created()
    {
        var account = Account.Register(AccountId, "john.doe@example.com", "hash", null, Now);

        var handler = new GetMyProfileQueryHandler(
            new FakeAccounts(account),
            new FakeProfiles(null));

        var result = await handler.Handle(new GetMyProfileQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountId, result.Value!.AccountId);
        Assert.Equal("john.doe", result.Value.FullName);
        Assert.Null(result.Value.DateOfBirth);
        Assert.Null(result.Value.CitizenIdNumber);
        Assert.Null(result.Value.DriverLicenseNumber);
        Assert.Null(result.Value.AvatarUrl);
    }

    [Fact]
    public async Task GetMyProfile_returns_error_when_account_does_not_exist()
    {
        var handler = new GetMyProfileQueryHandler(
            new FakeAccounts(null),
            new FakeProfiles(null));

        var result = await handler.Handle(new GetMyProfileQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetMyProfileErrors.AccountNotFound.Code, result.Error!.Code);
    }
}

internal sealed class FakeProfiles(Profile? profile) : IProfileRepository
{
    public void Add(Profile p) { }
    public Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult(profile is not null && profile.AccountId == accountId ? profile : null);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
