using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

// F3: an owner type that is not one of the enum values has an empty checklist, which let a resubmit skip every document.
public sealed class OwnerTypeInvariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void SetOwnerType_rejects_a_value_that_is_not_an_owner_type_and_changes_nothing(int value)
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        var version = account.RowVersion;

        Assert.Throws<ArgumentOutOfRangeException>(() => account.SetOwnerType((OwnerType)value));

        Assert.Equal(OwnerType.Individual, account.OwnerType);
        Assert.Equal(version, account.RowVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void ApplyForParkingLotOwner_rejects_a_value_that_is_not_an_owner_type_and_changes_nothing(int value)
    {
        var account = Account.Register(AccountId, "customer@example.com", "hash", null, Now);
        var version = account.RowVersion;

        Assert.Throws<ArgumentOutOfRangeException>(() => account.ApplyForParkingLotOwner(Now, ownerType: (OwnerType)value));

        Assert.Null(account.OwnerApplicationStatus);
        Assert.Null(account.OwnerType);
        Assert.Equal(version, account.RowVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void ResubmitOwnerApplication_rejects_a_value_that_is_not_an_owner_type_and_changes_nothing(int value)
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        account.RejectOwnerApplication(Guid.NewGuid(), "Thiếu giấy tờ.", Now);
        var version = account.RowVersion;

        Assert.Throws<ArgumentOutOfRangeException>(() => account.ResubmitOwnerApplication(Now, ownerType: (OwnerType)value));

        Assert.Equal(OwnerApplicationStatus.Rejected, account.OwnerApplicationStatus);
        Assert.Equal("Thiếu giấy tờ.", account.OwnerRejectionReason);
        Assert.Equal(OwnerType.Individual, account.OwnerType);
        Assert.Equal(version, account.RowVersion);
    }
}
