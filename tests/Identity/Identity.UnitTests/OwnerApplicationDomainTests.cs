using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class OwnerApplicationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Default_registration_has_null_owner_application_status()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);

        Assert.Equal(AccountRole.Customer, account.Role);
        Assert.Null(account.OwnerApplicationStatus);
        Assert.Null(account.OwnerRejectionReason);
        Assert.Null(account.OwnerReviewedAtUtc);
        Assert.Null(account.OwnerReviewedBy);
    }

    [Fact]
    public void Registering_as_owner_starts_in_Pending_status_with_Customer_role()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        // Security invariant: even when registering as an owner, the role remains Customer until approved by Admin
        Assert.Equal(AccountRole.Customer, account.Role);
        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Null(account.OwnerRejectionReason);
        Assert.Null(account.OwnerReviewedAtUtc);
        Assert.Null(account.OwnerReviewedBy);
    }

    [Fact]
    public void Approve_transitions_from_Pending_to_Approved_and_grants_ParkingLotOwner_role()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var reviewedAt = Now.AddHours(1);

        account.ApproveOwnerApplication(AdminId, reviewedAt);

        Assert.Equal(AccountRole.ParkingLotOwner, account.Role);
        Assert.Equal(OwnerApplicationStatus.Approved, account.OwnerApplicationStatus);
        Assert.Equal(AdminId, account.OwnerReviewedBy);
        Assert.Equal(reviewedAt, account.OwnerReviewedAtUtc);
        Assert.Null(account.OwnerRejectionReason);
    }

    [Fact]
    public void Approve_non_pending_application_throws_InvalidOperationException()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);

        Assert.Throws<InvalidOperationException>(() => account.ApproveOwnerApplication(AdminId, Now));
    }

    [Fact]
    public void Admin_cannot_approve_their_own_owner_application()
    {
        var account = Account.Register(AccountId, "admin@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        Assert.Throws<InvalidOperationException>(() => account.ApproveOwnerApplication(AccountId, Now));
    }

    [Fact]
    public void Reject_transitions_from_Pending_to_Rejected_and_records_reason()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var reviewedAt = Now.AddHours(2);
        var reason = "Hồ sơ chưa có giấy phép kinh doanh bãi đỗ xe hợp lệ.";

        account.RejectOwnerApplication(AdminId, reason, reviewedAt);

        Assert.Equal(AccountRole.Customer, account.Role);
        Assert.Equal(OwnerApplicationStatus.Rejected, account.OwnerApplicationStatus);
        Assert.Equal(AdminId, account.OwnerReviewedBy);
        Assert.Equal(reviewedAt, account.OwnerReviewedAtUtc);
        Assert.Equal(reason, account.OwnerRejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_with_empty_or_whitespace_reason_throws_ArgumentException(string reason)
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        Assert.Throws<ArgumentException>(() => account.RejectOwnerApplication(AdminId, reason, Now));
    }

    [Fact]
    public void Reject_with_reason_longer_than_500_characters_throws_ArgumentException()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var longReason = new string('A', 501);

        Assert.Throws<ArgumentException>(() => account.RejectOwnerApplication(AdminId, longReason, Now));
    }

    [Fact]
    public void Reject_non_pending_application_throws_InvalidOperationException()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);

        Assert.Throws<InvalidOperationException>(() => account.RejectOwnerApplication(AdminId, "Reason", Now));
    }

    [Fact]
    public void Resubmit_transitions_from_Rejected_to_Pending_and_clears_rejection_reason()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.RejectOwnerApplication(AdminId, "Thiếu tài liệu.", Now);

        account.ResubmitOwnerApplication(Now.AddDays(1));

        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Null(account.OwnerRejectionReason);
        Assert.Null(account.OwnerReviewedBy);
        Assert.Null(account.OwnerReviewedAtUtc);
    }

    [Fact]
    public void Resubmit_non_rejected_application_throws_InvalidOperationException()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        // Account is still in Pending status, cannot resubmit
        Assert.Throws<InvalidOperationException>(() => account.ResubmitOwnerApplication(Now));
    }

    [Fact]
    public void AssignRole_to_ParkingLotOwner_synchronizes_status_to_Approved()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);

        account.AssignRole(AccountRole.ParkingLotOwner);

        Assert.Equal(AccountRole.ParkingLotOwner, account.Role);
        Assert.Equal(OwnerApplicationStatus.Approved, account.OwnerApplicationStatus);
    }

    [Fact]
    public void AssignRole_from_ParkingLotOwner_to_Customer_clears_owner_application_status()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);
        account.AssignRole(AccountRole.ParkingLotOwner);

        account.AssignRole(AccountRole.Customer);

        Assert.Equal(AccountRole.Customer, account.Role);
        Assert.Null(account.OwnerApplicationStatus);
    }

    [Fact]
    public void ApplyForParkingLotOwner_transitions_from_null_to_Pending_and_stores_tax_code_and_business_name()
    {
        var account = Account.Register(AccountId, "customer@example.com", "hash", null, Now);
        var taxCode = "0312345678";
        var businessName = "Bãi Xe Thông Minh 247";

        account.ApplyForParkingLotOwner(Now, taxCode, businessName);

        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Equal(taxCode, account.OwnerTaxCode);
        Assert.Equal(businessName, account.OwnerBusinessName);
        Assert.Null(account.OwnerRejectionReason);
        Assert.Null(account.OwnerReviewedBy);
        Assert.Null(account.OwnerReviewedAtUtc);
    }

    [Fact]
    public void ApplyForParkingLotOwner_when_already_owner_throws_InvalidOperationException()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now);
        account.AssignRole(AccountRole.ParkingLotOwner);

        Assert.Throws<InvalidOperationException>(() => account.ApplyForParkingLotOwner(Now, "0312345678", "Biz"));
    }

    [Fact]
    public void ApplyForParkingLotOwner_when_already_pending_throws_InvalidOperationException()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        Assert.Throws<InvalidOperationException>(() => account.ApplyForParkingLotOwner(Now, "0312345678", "Biz"));
    }

    [Fact]
    public void Resubmit_allows_updating_tax_code_and_business_name()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true, taxCode: "1111111111", businessName: "Old Biz");
        account.RejectOwnerApplication(AdminId, "Sai MST", Now);

        var newTaxCode = "0399999999";
        var newBizName = "New Biz Co";
        account.ResubmitOwnerApplication(Now.AddDays(1), newTaxCode, newBizName);

        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Equal(newTaxCode, account.OwnerTaxCode);
        Assert.Equal(newBizName, account.OwnerBusinessName);
        Assert.Null(account.OwnerRejectionReason);
    }

    [Fact]
    public void UpdatePhoneNumber_normalizes_and_updates_number()
    {
        var account = Account.Register(AccountId, "user@example.com", "hash", null, Now);

        account.UpdatePhoneNumber("0912345678");

        Assert.Equal("+84912345678", account.PhoneNumber);
    }
}
