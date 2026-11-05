using System.Text.Json;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Application.Features.ParkingSessions.Commands;

// Kiểm thử ca xác nhận thu tiền mặt đỗ xe
public class ConfirmCashParkingFeePaymentCommandHandlerTests
{
    private class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private class FakeParkingSessionGateway : IParkingSessionGateway
    {
        public Result<ParkingSessionFeeContext>? ResultToReturn { get; set; }

        public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
            Guid sessionId,
            Guid? callerAccountId,
            string callerRole,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn ?? Result<ParkingSessionFeeContext>.Failure(
                PayParkingFeeErrors.SessionNotFound));
        }
    }

    private class FakePrepaymentReader : IPrepaymentReader
    {
        public long PrepaidAmount { get; set; }

        public Task<long> GetPrepaidAmountAsync(
            Guid accountId,
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PrepaidAmount);
        }
    }

    private class FakeParkingFeePaymentRepository : IParkingFeePaymentRepository
    {
        public bool HasSucceeded { get; set; }
        public ParkingFeePayment? PendingPayment { get; set; }
        public readonly List<ParkingFeePayment> AddedPayments = [];

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(HasSucceeded);
        }

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(HasSucceeded);
        }

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(PendingPayment);
        }

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct)
        {
            AddedPayments.Add(newPayment);
            return Task.CompletedTask;
        }

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            AddedPayments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
        {
            return Task.FromResult(AddedPayments.LastOrDefault(p => p.TxnRef == txnRef));
        }

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(AddedPayments.LastOrDefault(p => p.Id == id));
        }
    }

    private class FakeIdempotencyStore : IIdempotencyStore
    {
        public readonly Dictionary<string, PaymentIdempotencyRecord> Records = [];
        public bool ReleaseCalled { get; private set; }

        public Task<PaymentIdempotencyRecord?> FindAsync(
            Guid accountId, string operation, string key, CancellationToken ct)
        {
            Records.TryGetValue($"{accountId}:{operation}:{key}", out var record);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var k = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            if (Records.ContainsKey(k))
            {
                return Task.FromResult(false);
            }

            Records[k] = record;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var k = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            Records[k] = record;
            return Task.CompletedTask;
        }

        public Task ReleasePendingAsync(Guid id, CancellationToken ct)
        {
            ReleaseCalled = true;
            var entry = Records.FirstOrDefault(r => r.Value.Id == id);
            if (entry.Key != null)
            {
                Records.Remove(entry.Key);
            }
            return Task.CompletedTask;
        }
    }

    private class FakeAuditWriter : IAuditWriter
    {
        public readonly List<AuditEntryInput> Entries = [];

        public Task WriteAsync(AuditEntryInput entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private class FakeOutboxWriter : IOutboxWriter
    {
        public readonly List<(object Event, string Type, int Version, Guid? CorrelationId)> EnqueuedEvents = [];

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null) where TEvent : class
        {
            EnqueuedEvents.Add((integrationEvent, eventType, eventVersion, correlationId));
        }
    }

    private readonly DateTimeOffset _fixedNow = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    private readonly TestClock _clock;
    private readonly FakeParkingSessionGateway _parkingGateway = new();
    private readonly FakePrepaymentReader _prepaymentReader = new();
    private readonly FakeParkingFeePaymentRepository _paymentRepository = new();
    private readonly FakeIdempotencyStore _idempotencyStore = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly FakeOutboxWriter _outboxWriter = new();

    public ConfirmCashParkingFeePaymentCommandHandlerTests()
    {
        _clock = new TestClock(_fixedNow);
    }

    private ConfirmCashParkingFeePaymentCommandHandler CreateHandler()
    {
        return new ConfirmCashParkingFeePaymentCommandHandler(
            _parkingGateway,
            _prepaymentReader,
            _paymentRepository,
            _idempotencyStore,
            _auditWriter,
            _outboxWriter,
            _clock);
    }

    [Fact]
    public async Task Handle_WhenValidStaffAndMatchingLot_ShouldConfirmCash_WriteAuditLog_AndEnqueueOutbox()
    {
        // Khởi tạo phiên xe hợp lệ
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-2);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            Guid.NewGuid(),
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000,
            "Thu tiền mặt tại cổng");

        var handler = CreateHandler();

        // Thực thi xác nhận thu tiền
        var result = await handler.Handle(command, CancellationToken.None);

        // Kiểm tra kết quả trả về
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.TransactionId);
        Assert.Equal(sessionId, result.Value.SessionId);
        Assert.Equal(20000, result.Value.AmountPaid);
        Assert.Equal("Cash", result.Value.PaymentMethod);
        Assert.Equal(staffAccountId, result.Value.ConfirmedByStaffId);
        Assert.Equal(lotId, result.Value.ParkingLotId);
        Assert.Equal(_fixedNow, result.Value.PaymentTimeUtc);
        Assert.Equal(_fixedNow.AddMinutes(15), result.Value.ExitWindowExpiresUtc);

        // Kiểm tra lưu hóa đơn
        Assert.Single(_paymentRepository.AddedPayments);
        var payment = _paymentRepository.AddedPayments[0];
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("Cash", payment.PaymentMethod);
        Assert.Equal(20000, payment.AmountPaid);

        // Kiểm tra ghi sổ kiểm toán
        Assert.Single(_auditWriter.Entries);
        var audit = _auditWriter.Entries[0];
        Assert.Equal("CashPaymentConfirmed", audit.ActionType);
        Assert.Equal("ParkingSession", audit.TargetType);
        Assert.Equal(sessionId.ToString(), audit.TargetId);
        Assert.Equal("Thu tiền mặt tại cổng", audit.Reason);
        Assert.Equal(lotId, audit.ParkingLotId);

        // Kiểm tra sự kiện Outbox
        Assert.Single(_outboxWriter.EnqueuedEvents);
        var (evt, type, version, correlationId) = _outboxWriter.EnqueuedEvents[0];
        Assert.Equal("payment.session-paid.v1", type);
        Assert.Equal(1, version);
        Assert.Equal(payment.Id, correlationId);
        var paidEvt = Assert.IsType<ParkingSessionPaidIntegrationEvent>(evt);
        Assert.Equal(sessionId, paidEvt.SessionId);
        Assert.Equal(payment.Id, paidEvt.PaymentId);
        Assert.Equal(20000, paidEvt.AmountPaid);
        Assert.Equal(_fixedNow, paidEvt.PaidAtUtc);
        Assert.Equal(_fixedNow.AddMinutes(15), paidEvt.ExitWindowExpiresUtc);
    }

    [Fact]
    public async Task Handle_WhenUnregisteredGuest_WithNullAccountId_ShouldSucceed()
    {
        // Khách vãng lai không có tài khoản
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-1);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-99999",
            "Car",
            "Active",
            entryTime,
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        // Xác nhận thành công và không có tài khoản khách
        Assert.True(result.IsSuccess);
        Assert.Null(_paymentRepository.AddedPayments[0].AccountId);
        Assert.Equal("Khách thanh toán tiền mặt tại bãi", _auditWriter.Entries[0].Reason);
    }

    [Fact]
    public async Task Handle_WhenCallerIsCustomer_ShouldReturnRoleNotAllowed()
    {
        // Khách hàng không được tự xác nhận tiền mặt
        var command = new ConfirmCashParkingFeePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Customer",
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.RoleNotAllowedCash, result.Error);
    }

    [Fact]
    public async Task Handle_WhenStaffLotIdDoesNotMatchSessionLotId_ShouldReturnLotAccessDenied()
    {
        // Nhân viên thuộc bãi khác
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var sessionLotId = Guid.NewGuid();
        var otherLotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-1);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            sessionLotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            otherLotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.LotAccessDenied, result.Error);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ShouldReturnSessionNotFound()
    {
        // Phiên xe không tồn tại
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Failure(PayParkingFeeErrors.SessionNotFound);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_WhenSessionNotActive_ShouldReturnSessionNotActive()
    {
        // Phiên xe đã kết thúc
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Completed",
            _fixedNow.AddHours(-1),
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionNotActive, result.Error);
    }

    [Fact]
    public async Task Handle_WhenSucceededPaymentExists_ShouldReturnSessionAlreadyPaid()
    {
        // Phiên xe đã thanh toán thành công trước đó
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            _fixedNow.AddHours(-1),
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _paymentRepository.HasSucceeded = true;

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid, result.Error);
    }

    [Fact]
    public async Task Handle_WhenPendingPaymentExists_ShouldReturnPaymentAlreadyInProgress()
    {
        // Đang có giao dịch khác xử lý dở
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            _fixedNow.AddHours(-1),
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _paymentRepository.PendingPayment = ParkingFeePayment.CreateVnPayPayment(
            sessionId,
            lotId,
            null,
            "51A-12345",
            "Car",
            20000,
            0,
            20000,
            _fixedNow,
            _fixedNow.AddMinutes(15),
            "TXN_123",
            "https://sandbox.vnpayment.vn");

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.PaymentAlreadyInProgress, result.Error);
    }

    [Fact]
    public async Task Handle_WhenNoAmountDue_ShouldReturnNoAmountDue()
    {
        // Xe đã cọc đủ tiền không còn nợ
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            _fixedNow.AddHours(-1),
            accountId,
            bookingId,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _prepaymentReader.PrepaidAmount = 20000;

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.NoAmountDue, result.Error);
    }

    [Fact]
    public async Task Handle_WhenFeeChanged_ShouldReturnFeeChanged()
    {
        // Phí tính toán thực tế khác số tiền mong đợi
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            _fixedNow.AddHours(-1),
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            Guid.NewGuid().ToString(),
            15000); // Mong đợi 15k nhưng thực tế 20k

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.FeeChanged, result.Error);
    }

    [Fact]
    public async Task Handle_WhenReasonExceeds500Chars_ShouldReturnReasonTooLong()
    {
        // Lý do dài quá 500 ký tự
        var longReason = new string('A', 501);
        var command = new ConfirmCashParkingFeePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff",
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            20000,
            longReason);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.ReasonTooLong, result.Error);
    }

    [Fact]
    public async Task Handle_WhenIdempotencySuccessExists_ShouldReplayOriginalResponse()
    {
        // Khóa chống trùng đã ghi nhận thành công trước đó
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var cachedResponse = new ConfirmCashParkingFeePaymentResponse(
            Guid.NewGuid(),
            sessionId,
            20000,
            "Cash",
            staffAccountId,
            lotId,
            _fixedNow,
            _fixedNow.AddMinutes(15));

        var record = PaymentIdempotencyRecord.CreatePending(
            staffAccountId,
            "confirm_cash_payment",
            key,
            $"{sessionId}:20000",
            _fixedNow);
        record.MarkSuccess(JsonSerializer.Serialize(cachedResponse), 200);

        _idempotencyStore.Records[$"{staffAccountId}:confirm_cash_payment:{key}"] = record;

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            lotId,
            key,
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(cachedResponse.TransactionId, result.Value.TransactionId);
        Assert.Equal(cachedResponse.AmountPaid, result.Value.AmountPaid);
        Assert.Empty(_paymentRepository.AddedPayments);
    }
}
