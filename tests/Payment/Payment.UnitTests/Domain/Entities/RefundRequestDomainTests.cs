using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Domain.Entities;

public sealed class RefundRequestDomainTests
{
    private readonly Guid _walletId = Guid.NewGuid();
    private readonly Guid _lotId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_with_valid_parameters_initializes_requested_state()
    {
        // R-01: status is Requested, CreatedBy set, reviewer and ledger fields are null
        var request = RefundRequest.Create(
            walletId: _walletId,
            lotId: _lotId,
            amount: 50000m,
            reason: "Trừ tiền 2 lần tại cổng",
            createdBy: _customerId,
            referenceType: ReferenceType.ParkingSession,
            referenceId: "SES-12345",
            now: _now);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(_walletId, request.WalletId);
        Assert.Equal(_lotId, request.LotId);
        Assert.Equal(50000m, request.Amount);
        Assert.Equal("Trừ tiền 2 lần tại cổng", request.Reason);
        Assert.Equal(_customerId, request.CreatedBy);
        Assert.Equal(ReferenceType.ParkingSession, request.ReferenceType);
        Assert.Equal("SES-12345", request.ReferenceId);
        Assert.Equal(RefundStatus.Requested, request.Status);
        Assert.Null(request.OwnerConfirmedBy);
        Assert.Null(request.ReviewedBy);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.LedgerEntryId);
        Assert.Equal(_now, request.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(1000.5)]
    [InlineData(25000.99)]
    public void Create_with_non_positive_or_fractional_amount_throws(decimal invalidAmount)
    {
        // R-02: amount must be a positive whole number in VND
        Assert.Throws<ArgumentException>(() =>
            RefundRequest.Create(_walletId, _lotId, invalidAmount, "Valid reason", _customerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_or_whitespace_reason_throws(string emptyReason)
    {
        Assert.Throws<ArgumentException>(() =>
            RefundRequest.Create(_walletId, _lotId, 10000m, emptyReason, _customerId));
    }

    [Fact]
    public void Create_with_reason_exceeding_500_characters_throws()
    {
        var longReason = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            RefundRequest.Create(_walletId, _lotId, 10000m, longReason, _customerId));
    }

    [Fact]
    public void ConfirmByOwner_sets_owner_and_keeps_requested_status()
    {
        // R-05: owner confirmation sets OwnerConfirmedBy and leaves status Requested
        var request = RefundRequest.Create(_walletId, _lotId, 30000m, "Valid reason", _customerId, now: _now);

        request.ConfirmByOwner(_ownerId, _now);

        Assert.Equal(_ownerId, request.OwnerConfirmedBy);
        Assert.Equal(RefundStatus.Requested, request.Status);
    }

    [Fact]
    public void ConfirmByOwner_twice_throws()
    {
        var request = RefundRequest.Create(_walletId, _lotId, 30000m, "Valid reason", _customerId, now: _now);
        request.ConfirmByOwner(_ownerId, _now);

        Assert.Throws<InvalidOperationException>(() => request.ConfirmByOwner(Guid.NewGuid(), _now));
    }

    [Fact]
    public void Reject_before_owner_confirmation_throws_InvalidOperationException()
    {
        // R-09 / D2: Admin cannot reject before owner confirmed
        var request = RefundRequest.Create(_walletId, _lotId, 30000m, "Valid reason", _customerId, now: _now);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            request.Reject(_adminId, "Khách không có lỗi hệ thống", _now));

        Assert.Contains("Owner has not confirmed", ex.Message);
    }

    [Fact]
    public void Reject_when_creator_is_reviewer_throws_UnauthorizedAccessException()
    {
        // R-08: Separation of duties
        var request = RefundRequest.Create(_walletId, _lotId, 30000m, "Valid reason", _adminId, now: _now);
        request.ConfirmByOwner(_ownerId, _now);

        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            request.Reject(_adminId, "Từ chối", _now));

        Assert.Contains("cannot be the reviewer", ex.Message);
    }

    [Fact]
    public void Reject_after_owner_confirmation_sets_rejected_and_reviewer()
    {
        // R-07, R-11: Rejection sets status Rejected, reviewer, reviewedAt, ledgerEntryId stays null
        var request = RefundRequest.Create(_walletId, _lotId, 30000m, "Valid reason", _customerId, now: _now);
        request.ConfirmByOwner(_ownerId, _now);

        var rejectTime = _now.AddMinutes(10);
        request.Reject(_adminId, "Không đủ bằng chứng sự cố hệ thống", rejectTime);

        Assert.Equal(RefundStatus.Rejected, request.Status);
        Assert.Equal(_adminId, request.ReviewedBy);
        Assert.Equal(rejectTime, request.ReviewedAt);
        Assert.Null(request.LedgerEntryId);
    }
}
