using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Application.Profiles.DeleteProfileIdentity;
using ParkingSystem.Identity.Application.Profiles.CustomerMedia;
using ParkingSystem.Identity.Application.Profiles.UpsertProfile;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Profiles;

// US-PRIV-002: Unit tests for deleting identity data (CCCD/GPLX) from profile.
public sealed class DeleteProfileIdentityCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public async Task DeleteProfileIdentity_clears_cccd_and_driver_license_and_updates_timestamp()
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

        var repo = new TrackingFakeProfiles(profile);
        var clock = new FakeClock(Now.AddMinutes(5));
        var handler = new DeleteProfileIdentityCommandHandler(new FakeAccounts(account), repo, new FakeCustomerMedia(), clock);

        var result = await handler.Handle(
            new DeleteProfileIdentityCommand(AccountId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Thông tin định danh (CCCD/GPLX) đã được xóa thành công.", result.Value!.Message);
        Assert.Null(profile.CitizenIdNumber);
        Assert.Null(profile.DriverLicenseNumber);
        Assert.Equal("Nguyen Van Test", profile.FullName); // Non-identity fields are preserved
        Assert.Equal(Now.AddMinutes(5), profile.UpdatedAtUtc);
        Assert.True(repo.Saved);
    }

    [Fact]
    public async Task DeleteProfileIdentity_succeeds_idempotently_when_profile_does_not_exist()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);
        var repo = new TrackingFakeProfiles(null);
        var clock = new FakeClock(Now);
        var handler = new DeleteProfileIdentityCommandHandler(new FakeAccounts(account), repo, new FakeCustomerMedia(), clock);

        var result = await handler.Handle(
            new DeleteProfileIdentityCommand(AccountId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(repo.Saved); // No writes needed
    }

    [Fact]
    public async Task DeleteProfileIdentity_returns_error_when_account_does_not_exist()
    {
        var repo = new TrackingFakeProfiles(null);
        var clock = new FakeClock(Now);
        var handler = new DeleteProfileIdentityCommandHandler(new FakeAccounts(null), repo, new FakeCustomerMedia(), clock);

        var result = await handler.Handle(
            new DeleteProfileIdentityCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeleteProfileIdentityErrors.AccountNotFound.Code, result.Error!.Code);
    }
}

internal sealed class FakeCustomerMedia : ICustomerMediaStore
{
    public Task<bool> ExistsAsync(Guid accountId, Guid mediaId, string kind, CancellationToken ct) => Task.FromResult(false);
    public Task<CustomerMediaResult> SaveAsync(Guid accountId, string kind, Stream content, long sizeBytes, CancellationToken ct) =>
        throw new NotSupportedException();
    public Task<(Stream Content, string ContentType)?> OpenReadAsync(Guid accountId, Guid mediaId, CancellationToken ct) =>
        Task.FromResult<(Stream Content, string ContentType)?>(null);
    public Task<bool> DeleteAsync(Guid accountId, Guid mediaId, CancellationToken ct) => Task.FromResult(false);
    public Task DeleteIdentityMediaAsync(Guid accountId, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class TrackingFakeProfiles(Profile? initial) : IProfileRepository
{
    public Profile? Profile { get; set; } = initial;
    public bool Saved { get; private set; }

    public void Add(Profile p) => Profile = p;

    public Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult(Profile is not null && Profile.AccountId == accountId ? Profile : null);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saved = true;
        return Task.CompletedTask;
    }
}

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
}
