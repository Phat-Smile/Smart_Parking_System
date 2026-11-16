using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Queries;

public class GetParkingFeeReceiptQueryHandlerTests
{
    private sealed class FakePaymentRepository : IParkingFeePaymentRepository
    {
        private readonly List<ParkingFeePayment> _payments = [];

        public void Seed(ParkingFeePayment payment) => _payments.Add(payment);

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(_payments.Any(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded));

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
            => HasSucceededPaymentAsync(sessionId, ct);

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.SessionId == sessionId && p.Status == PaymentStatus.Pending));

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct)
        {
            _payments.Add(newPayment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.TxnRef == txnRef));

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct)
            => Task.CompletedTask;

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            _payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.Id == id));

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
            => Task.FromResult(_payments
                .Where(p => (p.SessionId == sessionOrPaymentId || p.Id == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.PaymentTimeUtc)
                .FirstOrDefault());
    }

    private sealed class FakeParkingSessionGateway : IParkingSessionGateway
    {
        public Result<ParkingSessionFeeContext>? ResultToReturn { get; set; }

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
            Guid sessionId,
            Guid? callerAccountId,
            string callerRole,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn
                ?? Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound));
        }
    }

    private sealed class FakeMembershipGateway : IParkingLotMembershipGateway
    {
        public ParkingLotMembershipInfo Membership { get; set; } = new(false, string.Empty);

        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId, Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ParkingLotMembershipInfo>.Success(Membership));
    }

    private readonly FakePaymentRepository _paymentRepo = new();
    private readonly FakeParkingSessionGateway _parkingGateway = new();
    private readonly FakeMembershipGateway _membership = new();
    private readonly Guid _ownerAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _sessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _lotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly DateTimeOffset _entryTime = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _paymentTime = new(2026, 11, 10, 10, 30, 0, TimeSpan.Zero);

    private ParkingFeePayment CreateTestPayment(
        long totalFee = 30000,
        long prepaidAmount = 10000,
        long amountPaid = 20000)
    {
        return ParkingFeePayment.CreateWalletPayment(
            sessionId: _sessionId,
            parkingLotId: _lotId,
            walletId: Guid.NewGuid(),
            accountId: _ownerAccountId,
            plateNumber: "51A-999.88",
            vehicleType: "Car",
            totalFee: totalFee,
            prepaidAmount: prepaidAmount,
            amountPaid: amountPaid,
            paymentTimeUtc: _paymentTime,
            exitWindowExpiresUtc: _paymentTime.AddMinutes(15));
    }

    private ParkingFeePayment CreateWalkInPayment(
        long totalFee = 30000,
        long prepaidAmount = 0,
        long amountPaid = 30000)
    {
        return ParkingFeePayment.CreateCashPayment(
            sessionId: _sessionId,
            parkingLotId: _lotId,
            accountId: null,
            plateNumber: "51A-999.88",
            vehicleType: "Car",
            totalFee: totalFee,
            prepaidAmount: prepaidAmount,
            amountPaid: amountPaid,
            paymentTimeUtc: _paymentTime,
            exitWindowExpiresUtc: _paymentTime.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_EmptyIdentifier_ReturnsTransactionIdInvalidError()
    {
        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(Guid.Empty, _ownerAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingFeePaymentErrors.TransactionIdInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsTransactionNotFoundError()
    {
        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(Guid.NewGuid(), _ownerAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingFeePaymentErrors.TransactionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_DifferentAccountWithoutPrivilegedRole_ReturnsSessionAccessDeniedError()
    {
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _otherAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_AdminRole_AllowsAccessRegardlessOfAccountId()
    {
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _otherAccountId, "SystemAdmin");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
    }

    [Fact]
    public async Task Handle_StaffOfThePaymentsLot_CanReadReceipt()
    {
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);
        _membership.Membership = new ParkingLotMembershipInfo(true, "Staff");

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _otherAccountId, "Staff");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
    }

    [Fact]
    public async Task Handle_StaffRoleWithoutMembershipOfTheLot_IsDenied()
    {
        // Before: any Staff token read every receipt (IDOR across lots).
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _otherAccountId, "Staff");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_CallerWithoutAccountId_IsDenied()
    {
        // When payment is linked to an account, a missing caller id is denied access.
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, null, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_AnonymousWalkInPayment_AllowsAccessWithoutAccountId()
    {
        var payment = CreateWalkInPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, null, string.Empty);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
        Assert.Null(result.Value!.AccountId);
    }

    [Fact]
    public async Task Handle_AnonymousWalkInPayment_AllowsAccessWithAnyCallerAccountId()
    {
        var payment = CreateWalkInPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _otherAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsReceiptWithFullSettlementDetails()
    {
        var payment = CreateTestPayment(totalFee: 30000, prepaidAmount: 10000, amountPaid: 20000);
        _paymentRepo.Seed(payment);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: _sessionId,
            ParkingLotId: _lotId,
            PlateNumber: "51A-999.88",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _entryTime,
            AccountId: _ownerAccountId,
            BookingId: Guid.NewGuid(),
            Pricing: new PricingSnapshotDto("POLICY_01", "PerBlock", 60, 10000)));

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _ownerAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var receipt = result.Value!;
        Assert.Equal(payment.Id, receipt.PaymentId);
        Assert.Equal(_sessionId, receipt.SessionId);
        Assert.Equal(_lotId, receipt.ParkingLotId);
        Assert.Equal(_ownerAccountId, receipt.AccountId);
        Assert.Equal("51A-999.88", receipt.PlateNumber);
        Assert.Equal("Car", receipt.VehicleType);
        Assert.Equal("Wallet", receipt.PaymentMethod);
        Assert.Equal(30000, receipt.TotalFee);
        Assert.Equal(10000, receipt.PrepaidAmount);
        Assert.Equal(20000, receipt.AmountPaid);
        Assert.Equal(_paymentTime, receipt.PaymentTimeUtc);
        Assert.Equal(_entryTime, receipt.EntryTimeUtc);
        Assert.Equal(150, receipt.DurationMinutes); // 2h 30m = 150m
        Assert.Equal(PaymentStatus.Succeeded, receipt.Status);
    }

    [Fact]
    public async Task Handle_QueryByPaymentId_ReturnsReceiptSuccessfully()
    {
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(payment.Id, _ownerAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
        Assert.Equal(_sessionId, result.Value!.SessionId);
    }

    [Fact]
    public async Task Handle_ParkingGatewayFailure_GracefullyReturnsReceiptWithoutEntryTime()
    {
        var payment = CreateTestPayment();
        _paymentRepo.Seed(payment);
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);

        var handler = new GetParkingFeeReceiptQueryHandler(_paymentRepo, _parkingGateway, _membership);
        var query = new GetParkingFeeReceiptQuery(_sessionId, _ownerAccountId, "Customer");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value!.PaymentId);
        Assert.Null(result.Value!.EntryTimeUtc);
        Assert.Null(result.Value!.DurationMinutes);
    }
}
