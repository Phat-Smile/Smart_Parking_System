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

    private readonly FakeParkingFeePaymentRepository _repository = new();
    private readonly GetParkingFeePaymentStatusQueryHandler _handler;

    public GetParkingFeePaymentStatusQueryHandlerTests()
    {
        _handler = new GetParkingFeePaymentStatusQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_EmptyTransactionId_ReturnsFailure_TransactionIdInvalid()
    {
        var query = new GetParkingFeePaymentStatusQuery(Guid.Empty);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingFeePaymentErrors.TransactionIdInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ReturnsFailure_TransactionNotFound()
    {
        var query = new GetParkingFeePaymentStatusQuery(Guid.NewGuid());
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

        var query = new GetParkingFeePaymentStatusQuery(payment.Id);
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

        var query = new GetParkingFeePaymentStatusQuery(payment.Id);
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

        var query = new GetParkingFeePaymentStatusQuery(payment.Id);
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

        var query = new GetParkingFeePaymentStatusQuery(payment.Id);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, result.Value!.Status);
        Assert.Equal("24", result.Value.FailureCode);
        Assert.Null(result.Value.PaymentUrl);
    }
}
