using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public class GetParkingFeePaymentStatusQueryHandlerTests
{
    private class FakeParkingFeePaymentRepository : IParkingFeePaymentRepository
    {
        public readonly Dictionary<Guid, ParkingFeePayment> Payments = new();

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult<ParkingFeePayment?>(null);

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct) =>
            Task.FromResult<ParkingFeePayment?>(null);

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct) =>
            Task.CompletedTask;

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            Payments[payment.Id] = payment;
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            Payments.TryGetValue(id, out var payment);
            return Task.FromResult(payment);
        }

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
        {
            var payment = Payments.Values.FirstOrDefault(p => p.Id == sessionOrPaymentId || p.SessionId == sessionOrPaymentId);
            return Task.FromResult(payment);
        }
    }

    private sealed class DenyAllMembershipGateway : IParkingLotMembershipGateway
    {
        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId, Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(false, string.Empty)));
    }

    private sealed class LotMembershipGateway(Guid lotId, Guid accountId, string role) : IParkingLotMembershipGateway
    {
        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid queriedLotId, Guid queriedAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ParkingLotMembershipInfo>.Success(
                queriedLotId == lotId && queriedAccountId == accountId
                    ? new ParkingLotMembershipInfo(true, role)
                    : new ParkingLotMembershipInfo(false, string.Empty)));
    }

    private sealed class UnavailableMembershipGateway : IParkingLotMembershipGateway
    {
        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId, Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ParkingLotMembershipInfo>.Failure(PayParkingFeeErrors.ParkingServiceUnavailable));
    }

    private readonly FakeParkingFeePaymentRepository _repository = new();
    private readonly GetParkingFeePaymentStatusQueryHandler _handler;

    public GetParkingFeePaymentStatusQueryHandlerTests()
    {
        _handler = new GetParkingFeePaymentStatusQueryHandler(_repository, new DenyAllMembershipGateway());
    }

    private static readonly Guid LotA = Guid.Parse("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid CustomerA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid CustomerB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid StaffOfLotA = Guid.Parse("5a000000-0000-0000-0000-00000000005a");

    private async Task<ParkingFeePayment> SeedVnPayAsync(Guid? accountId)
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: LotA,
            accountId: accountId,
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 20000,
            prepaidAmount: 0,
            amountPaid: 20000,
            feeCalculatedAtUtc: now,
            expiresAtUtc: now.AddMinutes(15),
            txnRef: "PARK_" + Guid.NewGuid().ToString("N")[..8],
            paymentUrl: "https://sandbox.vnpayment.vn/test-url");
        await _repository.AddAsync(payment, CancellationToken.None);
        return payment;
    }

    private Task<Result<GetParkingFeePaymentStatusResponse>> ReadAsync(
        IParkingLotMembershipGateway membership, Guid paymentId, Guid? caller, string? role)
        => new GetParkingFeePaymentStatusQueryHandler(_repository, membership)
            .Handle(new GetParkingFeePaymentStatusQuery(paymentId, caller, role), CancellationToken.None);

    [Fact]
    public async Task Customer_cannot_read_another_customers_payment()
    {
        var payment = await SeedVnPayAsync(CustomerA);

        var result = await ReadAsync(new DenyAllMembershipGateway(), payment.Id, CustomerB, "Customer");

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Customer_cannot_read_a_guest_payment_without_an_account()
    {
        var payment = await SeedVnPayAsync(accountId: null);

        var result = await ReadAsync(new DenyAllMembershipGateway(), payment.Id, CustomerB, "Customer");

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Caller_without_an_account_id_is_denied()
    {
        var payment = await SeedVnPayAsync(CustomerA);

        var result = await ReadAsync(new DenyAllMembershipGateway(), payment.Id, null, "Customer");

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Staff_of_the_payments_lot_can_read_a_guest_payment()
    {
        var payment = await SeedVnPayAsync(accountId: null);

        var result = await ReadAsync(new LotMembershipGateway(LotA, StaffOfLotA, "Staff"), payment.Id, StaffOfLotA, "Staff");

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.TransactionId);
    }

    [Fact]
    public async Task Staff_of_another_lot_cannot_read_the_payment()
    {
        var payment = await SeedVnPayAsync(CustomerA);

        var result = await ReadAsync(new LotMembershipGateway(Guid.NewGuid(), StaffOfLotA, "Staff"), payment.Id, StaffOfLotA, "Staff");

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Owner_role_needs_owner_membership_of_the_lot()
    {
        var payment = await SeedVnPayAsync(CustomerA);

        var asStaffMember = await ReadAsync(new LotMembershipGateway(LotA, StaffOfLotA, "Staff"), payment.Id, StaffOfLotA, "ParkingLotOwner");
        var asOwnerMember = await ReadAsync(new LotMembershipGateway(LotA, StaffOfLotA, "Owner"), payment.Id, StaffOfLotA, "ParkingLotOwner");

        Assert.False(asStaffMember.IsSuccess);
        Assert.True(asOwnerMember.IsSuccess);
    }

    [Fact]
    public async Task Staff_is_denied_when_parking_cannot_confirm_membership()
    {
        var payment = await SeedVnPayAsync(CustomerA);

        var result = await ReadAsync(new UnavailableMembershipGateway(), payment.Id, StaffOfLotA, "Staff");

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task System_admin_can_read_any_payment()
    {
        var payment = await SeedVnPayAsync(accountId: null);

        var result = await ReadAsync(new DenyAllMembershipGateway(), payment.Id, Guid.NewGuid(), "SystemAdmin");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_EmptyTransactionId_ReturnsFailure_TransactionIdInvalid()
    {
        var query = new GetParkingFeePaymentStatusQuery(Guid.Empty, null, null);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingFeePaymentErrors.TransactionIdInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ReturnsFailure_TransactionNotFound()
    {
        var query = new GetParkingFeePaymentStatusQuery(Guid.NewGuid(), null, null);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingFeePaymentErrors.TransactionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_PendingPayment_Unexpired_ReturnsPaymentUrl()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 20000,
            prepaidAmount: 0,
            amountPaid: 20000,
            feeCalculatedAtUtc: now,
            expiresAtUtc: now.AddMinutes(15),
            txnRef: "PARK_123456",
            paymentUrl: "https://sandbox.vnpayment.vn/test-url");

        await _repository.AddAsync(payment, CancellationToken.None);

        var query = new GetParkingFeePaymentStatusQuery(payment.Id, payment.AccountId, "Customer");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.TransactionId);
        Assert.Equal(payment.SessionId, result.Value.SessionId);
        Assert.Equal(PaymentStatus.Pending, result.Value.Status);
        Assert.Equal(PaymentMethod.VnPay, result.Value.Method);
        Assert.Equal(20000, result.Value.Amount);
        Assert.Equal("https://sandbox.vnpayment.vn/test-url", result.Value.PaymentUrl);
        Assert.Null(result.Value.PaymentTimeUtc);
        Assert.Null(result.Value.ExitWindowExpiresUtc);
    }

    [Fact]
    public async Task Handle_PendingPayment_Expired_ReturnsNullPaymentUrl()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 20000,
            prepaidAmount: 0,
            amountPaid: 20000,
            feeCalculatedAtUtc: now.AddMinutes(-30),
            expiresAtUtc: now.AddMinutes(-15),
            txnRef: "PARK_123456",
            paymentUrl: "https://sandbox.vnpayment.vn/test-url");

        await _repository.AddAsync(payment, CancellationToken.None);

        var query = new GetParkingFeePaymentStatusQuery(payment.Id, payment.AccountId, "Customer");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PaymentUrl);
    }

    [Fact]
    public async Task Handle_SucceededPayment_ReturnsPaymentTimeAndExitWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 20000,
            prepaidAmount: 0,
            amountPaid: 20000,
            feeCalculatedAtUtc: now,
            expiresAtUtc: now.AddMinutes(15),
            txnRef: "PARK_123456",
            paymentUrl: "https://sandbox.vnpayment.vn/test-url");

        payment.MarkAsSucceeded("VNPay12345", now.AddMinutes(2), now.AddMinutes(17));
        await _repository.AddAsync(payment, CancellationToken.None);

        var query = new GetParkingFeePaymentStatusQuery(payment.Id, payment.AccountId, "Customer");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, result.Value!.Status);
        Assert.Null(result.Value.PaymentUrl);
        Assert.Equal(now, result.Value.PaymentTimeUtc);
        Assert.Equal(now.AddMinutes(17), result.Value.ExitWindowExpiresUtc);
        Assert.Equal(now.AddMinutes(2), result.Value.ConfirmedAtUtc);
    }

    [Fact]
    public async Task Handle_FailedPayment_ReturnsFailureCode()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 20000,
            prepaidAmount: 0,
            amountPaid: 20000,
            feeCalculatedAtUtc: now,
            expiresAtUtc: now.AddMinutes(15),
            txnRef: "PARK_123456",
            paymentUrl: "https://sandbox.vnpayment.vn/test-url");

        payment.MarkAsFailed("24", now.AddMinutes(1));
        await _repository.AddAsync(payment, CancellationToken.None);

        var query = new GetParkingFeePaymentStatusQuery(payment.Id, payment.AccountId, "Customer");
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, result.Value!.Status);
        Assert.Equal("24", result.Value.FailureCode);
        Assert.Null(result.Value.PaymentUrl);
    }
}
