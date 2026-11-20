using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.CreateVnPayParkingFeePayment;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public class CreateVnPayParkingFeePaymentCommandHandlerTests
{
    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class FakeParkingSessionGateway : IParkingSessionGateway
    {
        public Result<ParkingSessionFeeContext>? ResultToReturn { get; set; }

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
            Guid sessionId, Guid? callerAccountId, string callerRole, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultToReturn ?? Result<ParkingSessionFeeContext>.Failure(PayParkingFeeErrors.SessionNotFound));
    }

    private sealed class FakePrepaymentReader : IPrepaymentReader
    {
        public Task<long> GetPrepaidAmountAsync(Guid accountId, Guid bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class FakeVnPayGateway : IVnpayPaymentGateway
    {
        public string BuildPaymentUrl(
            string txnRef, long amountVnd, string orderInfo, DateTimeOffset createdAtUtc,
            DateTimeOffset expiresAtUtc, string clientIp, string? returnUrl = null)
            => $"https://sandbox.vnpayment.vn/pay/{txnRef}";

        public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters) => true;
    }

    private sealed class DbUpdateException(string message) : Exception(message) { }

    private sealed class FakePaymentRepository : IParkingFeePaymentRepository
    {
        public readonly List<ParkingFeePayment> Payments = [];
        public Action<ParkingFeePayment>? OnCreateOrReplaceConflict { get; set; }

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(Payments.Any(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded));

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
            => HasSucceededPaymentAsync(sessionId, ct);

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(Payments.LastOrDefault(p => p.SessionId == sessionId && p.Status == PaymentStatus.Pending));

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct)
        {
            if (OnCreateOrReplaceConflict != null)
            {
                OnCreateOrReplaceConflict(newPayment);
                throw new DbUpdateException("23505 ux_fee_payments_session_pending");
            }

            foreach (var p in Payments.Where(p => p.SessionId == newPayment.SessionId && p.Status == PaymentStatus.Pending))
            {
                p.MarkAsSuperseded();
            }

            Payments.Add(newPayment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
            => Task.FromResult(Payments.FirstOrDefault(p => p.TxnRef == txnRef));

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
            => Task.FromResult(Payments
                .Where(p => (p.Id == sessionOrPaymentId || p.SessionId == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.PaymentSequence)
                .ThenByDescending(p => p.PaymentTimeUtc)
                .FirstOrDefault());
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, PaymentIdempotencyRecord> _records = [];
        public bool ReleaseCalled { get; private set; }

        public Task<PaymentIdempotencyRecord?> FindAsync(Guid accountId, string operation, string key, CancellationToken ct)
            => Task.FromResult(_records.GetValueOrDefault($"{accountId}:{operation}:{key}"));

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
            => Task.FromResult(_records.TryAdd($"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}", record));

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            _records[$"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}"] = record;
            return Task.CompletedTask;
        }

        public Task ReleasePendingAsync(Guid id, CancellationToken ct)
        {
            ReleaseCalled = true;
            var entry = _records.FirstOrDefault(r => r.Value.Id == id);
            if (entry.Key != null)
            {
                _records.Remove(entry.Key);
            }
            return Task.CompletedTask;
        }
    }

    private readonly FakeParkingSessionGateway _parkingGateway = new();
    private readonly FakePaymentRepository _paymentRepo = new();
    private readonly FakeIdempotencyStore _idempotencyStore = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));

    private CreateVnPayParkingFeePaymentCommandHandler CreateHandler() => new(
        _parkingGateway,
        new FakePrepaymentReader(),
        _paymentRepo,
        new FakeVnPayGateway(),
        _idempotencyStore,
        Options.Create(new VnpayOptions { PaymentExpiryMinutes = 15, ReturnUrl = "https://example.com/return" }),
        _clock);

    [Fact]
    public async Task Handle_WhenInitialSession_ReusesActivePendingPaymentOfSameSequence()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var existingPending = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51A-12345", "Car", 20000, 0, 20000,
            _clock.UtcNow.AddMinutes(-2), _clock.UtcNow.AddMinutes(13), "OLD_PENDING", "https://sandbox.vnpayment.vn/pay/OLD");
        _paymentRepo.Payments.Add(existingPending);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-1),
            accountId, null, new PricingSnapshotDto("P1", "PerTurn", 60, 20000)));

        var result = await CreateHandler().Handle(
            new CreateVnPayParkingFeePaymentCommand(sessionId, 20000, Guid.NewGuid().ToString(), accountId, "Customer"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsReused);
        Assert.Equal(existingPending.Id, result.Value.TransactionId);
    }

    [Fact]
    public async Task Handle_WhenOverstaySession_CreatesSequenceTwo_AndDoesNotReuseSequenceOnePendingPayment()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        // Lần 1 đã trả xong bằng Ví, đồng thời còn sót 1 mã QR Pending của lần 1 với cùng số tiền 10,000đ
        var seq1Succeeded = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, Guid.NewGuid(), accountId, "51A-12345", "Car",
            20000, 0, 20000, _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30));
        var staleSeq1Pending = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51A-12345", "Car",
            10000, 0, 10000, _clock.UtcNow.AddMinutes(-2), _clock.UtcNow.AddMinutes(13),
            "STALE_SEQ1", "https://sandbox.vnpayment.vn/pay/STALE_SEQ1", paymentSequence: 1);

        _paymentRepo.Payments.Add(seq1Succeeded);
        _paymentRepo.Payments.Add(staleSeq1Pending);

        // Quá giờ 30 phút sau hạn 15p -> 2 blocks x 5000 = 10,000đ
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-2),
            accountId, null, new PricingSnapshotDto("P1", "PerBlock", 15, 5000),
            LastPaidAtUtc: _clock.UtcNow.AddMinutes(-45),
            ArrivedAtExitUtc: _clock.UtcNow,
            GracePeriodMinutes: 15));

        var result = await CreateHandler().Handle(
            new CreateVnPayParkingFeePaymentCommand(sessionId, 10000, Guid.NewGuid().ToString(), accountId, "Customer"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsReused);
        Assert.NotEqual(staleSeq1Pending.Id, result.Value.TransactionId);
        Assert.Equal(PaymentStatus.Superseded, staleSeq1Pending.Status);

        var created = _paymentRepo.Payments.Single(p => p.Id == result.Value.TransactionId);
        Assert.Equal(2, created.PaymentSequence);
        Assert.Equal(PaymentStatus.Pending, created.Status);
    }

    [Fact]
    public async Task Handle_WhenOverstayLagDoubleChargeAttempted_ReturnsSessionAlreadyPaid()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var seq1 = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, Guid.NewGuid(), accountId, "51A-12345", "Car",
            20000, 0, 20000, _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30), paymentSequence: 1);
        var seq2 = ParkingFeePayment.CreateCashPayment(
            sessionId, lotId, accountId, "51A-12345", "Car",
            10000, 0, 10000, _clock.UtcNow.AddMinutes(-5), _clock.UtcNow.AddMinutes(10), paymentSequence: 2);
        _paymentRepo.Payments.Add(seq1);
        _paymentRepo.Payments.Add(seq2);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-2),
            accountId, null, new PricingSnapshotDto("P1", "PerBlock", 15, 5000),
            LastPaidAtUtc: _clock.UtcNow.AddMinutes(-45),
            ArrivedAtExitUtc: _clock.UtcNow,
            GracePeriodMinutes: 15));

        var result = await CreateHandler().Handle(
            new CreateVnPayParkingFeePaymentCommand(sessionId, 10000, Guid.NewGuid().ToString(), accountId, "Customer"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenCreateConflictAndSessionAlreadySucceeded_ReturnsSessionAlreadyPaid_AndWhenAmountDiffers_ReturnsFeeChanged()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-1),
            accountId, null, new PricingSnapshotDto("P1", "PerTurn", 60, 20000)));

        // Kịch bản 1: xung đột Pending vì một kênh khác vừa kịp thanh toán Succeeded
        _paymentRepo.OnCreateOrReplaceConflict = _ =>
        {
            var paid = ParkingFeePayment.CreateCashPayment(
                sessionId, lotId, accountId, "51A-12345", "Car", 20000, 0, 20000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15));
            _paymentRepo.Payments.Add(paid);
        };

        var alreadyPaidResult = await CreateHandler().Handle(
            new CreateVnPayParkingFeePaymentCommand(sessionId, 20000, Guid.NewGuid().ToString(), accountId, "Customer"),
            CancellationToken.None);

        Assert.True(alreadyPaidResult.IsFailure);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, alreadyPaidResult.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);

        // Kịch bản 2: xung đột Pending nhưng mã vừa tạo có số tiền khác (nhảy block) -> FeeChanged
        var session2 = Guid.NewGuid();
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            session2, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-1),
            accountId, null, new PricingSnapshotDto("P1", "PerTurn", 60, 20000)));

        _paymentRepo.OnCreateOrReplaceConflict = _ =>
        {
            var newerPending = ParkingFeePayment.CreateVnPayPayment(
                session2, lotId, accountId, "51A-12345", "Car", 30000, 0, 30000,
                _clock.UtcNow, _clock.UtcNow.AddMinutes(15), "NEW_30K", "https://sandbox.vnpayment.vn/new");
            _paymentRepo.Payments.Add(newerPending);
        };

        var feeChangedResult = await CreateHandler().Handle(
            new CreateVnPayParkingFeePaymentCommand(session2, 20000, Guid.NewGuid().ToString(), accountId, "Customer"),
            CancellationToken.None);

        Assert.True(feeChangedResult.IsFailure);
        Assert.Equal(PayParkingFeeErrors.FeeChanged.Code, feeChangedResult.Error!.Code);
    }
}
