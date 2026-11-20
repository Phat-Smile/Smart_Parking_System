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

    private class FakeParkingLotMembershipGateway : IParkingLotMembershipGateway
    {
        public Result<ParkingLotMembershipInfo>? ResultToReturn { get; set; }
        public Guid? LastQueriedLotId { get; private set; }
        public Guid? LastQueriedAccountId { get; private set; }

        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            LastQueriedLotId = lotId;
            LastQueriedAccountId = accountId;
            return Task.FromResult(ResultToReturn ?? Result<ParkingLotMembershipInfo>.Success(
                new ParkingLotMembershipInfo(true, "Staff")));
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

    private class DbUpdateException(string message) : Exception(message) { }
    private class DbUpdateConcurrencyException(string message) : DbUpdateException(message) { }

    private class FakeParkingFeePaymentRepository : IParkingFeePaymentRepository
    {
        public bool HasSucceeded { get; set; }
        public ParkingFeePayment? PendingPayment { get; set; }
        public readonly List<ParkingFeePayment> AddedPayments = [];
        private readonly List<ParkingFeePayment> _uncommittedAdds = [];
        public Func<Task>? OnSaveChangesAsync { get; set; }

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
            _uncommittedAdds.Add(payment);
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

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            if (OnSaveChangesAsync != null)
            {
                await OnSaveChangesAsync();
            }

            _uncommittedAdds.Clear();
        }

        public void ClearChangeTracker()
        {
            foreach (var pendingAdd in _uncommittedAdds)
            {
                AddedPayments.Remove(pendingAdd);
            }

            _uncommittedAdds.Clear();
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            return Task.FromResult(AddedPayments.LastOrDefault(p => p.Id == id));
        }

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
        {
            return Task.FromResult(AddedPayments
                .Where(p => (p.Id == sessionOrPaymentId || p.SessionId == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.PaymentSequence)
                .ThenByDescending(p => p.PaymentTimeUtc)
                .FirstOrDefault());
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
    private readonly FakeParkingLotMembershipGateway _membershipGateway = new();
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
            _membershipGateway,
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
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.RoleNotAllowedCash, result.Error);
    }

    [Fact]
    public async Task Handle_WhenStaffDoesNotBelongToLot_ShouldReturnLotAccessDenied()
    {
        // Nhân viên không thuộc bãi của phiên xe
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var sessionLotId = Guid.NewGuid();
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
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(
            new ParkingLotMembershipInfo(false, string.Empty));

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.LotAccessDenied, result.Error);
        Assert.Equal(sessionLotId, _membershipGateway.LastQueriedLotId);
        Assert.Equal(staffAccountId, _membershipGateway.LastQueriedAccountId);
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
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.PaymentAlreadyInProgress, result.Error);
    }

    [Fact]
    public async Task Handle_WhenAmountDueIsZero_ShouldSucceedAndPublishEventWithZeroAmount()
    {
        // Xe đã cọc đủ tiền không còn nợ: vẫn thanh toán thành công 0đ và phát sự kiện
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
            Guid.NewGuid().ToString(),
            0);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.AmountPaid);
        Assert.Equal("Cash", result.Value.PaymentMethod);
        Assert.Single(_outboxWriter.EnqueuedEvents);
        var paidEvent = _outboxWriter.EnqueuedEvents[0].Event as ParkingSessionPaidIntegrationEvent;
        Assert.NotNull(paidEvent);
        Assert.Equal(0, paidEvent.AmountPaid);
        Assert.Equal(sessionId, paidEvent.SessionId);
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

    [Fact]
    public async Task Handle_WhenOwnerBelongsToLot_ShouldSucceed()
    {
        var sessionId = Guid.NewGuid();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-1);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(
            new ParkingLotMembershipInfo(true, "Owner"));

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            ownerAccountId,
            "ParkingLotOwner",
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ownerAccountId, result.Value.ConfirmedByStaffId);
        Assert.Equal(lotId, result.Value.ParkingLotId);
        Assert.Single(_paymentRepository.AddedPayments);
    }

    [Fact]
    public async Task Handle_WhenOwnerOfOtherLotTriesToConfirm_ShouldReturnLotAccessDenied()
    {
        // Chủ bãi A cố gắng xác nhận thu tiền tại bãi B -> bị từ chối
        var sessionId = Guid.NewGuid();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-1);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(
            new ParkingLotMembershipInfo(false, string.Empty));

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            ownerAccountId,
            "ParkingLotOwner",
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.LotAccessDenied, result.Error);
        Assert.Empty(_paymentRepository.AddedPayments);
    }

    [Fact]
    public async Task Handle_WhenMembershipGatewayFails_ShouldReturnParkingServiceUnavailable_FailClosed()
    {
        // gRPC tra cứu quyền bãi bị lỗi (timeout, network, unavailable) -> fail-closed 503
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-1);
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            null,
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Failure(
            PayParkingFeeErrors.ParkingServiceUnavailable);

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.ParkingServiceUnavailable, result.Error);
        Assert.Empty(_paymentRepository.AddedPayments);
    }

    [Fact]
    public async Task Handle_WhenOverstaySession_ShouldInsertImmutableCashPayment_WithoutMutatingOriginal()
    {
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var entryTime = _fixedNow.AddHours(-3);

        // Lần gốc trả qua Ví 20,000 VND (hỗn hợp phương thức)
        var previousPayment = ParkingFeePayment.CreateWalletPayment(
            sessionId,
            lotId,
            Guid.NewGuid(),
            customerId,
            "51A-12345",
            "Car",
            20000,
            0,
            20000,
            _fixedNow.AddMinutes(-45),
            _fixedNow.AddMinutes(-30));
        _paymentRepository.AddedPayments.Add(previousPayment);
        _paymentRepository.HasSucceeded = true;

        // Phiên bị reopen sang Active sau khi quá hạn 15p (LastPaidAt = 45p trước)
        // Block 15p = 5000đ. Quá 45 - 15 = 30p -> 2 blocks = 10000đ
        var ctx = new ParkingSessionFeeContext(
            sessionId,
            lotId,
            "51A-12345",
            "Car",
            "Active",
            entryTime,
            customerId,
            null,
            new PricingSnapshotDto("P1", "Block", 15, 5000),
            LastPaidAtUtc: _fixedNow.AddMinutes(-45),
            ArrivedAtExitUtc: _fixedNow,
            GracePeriodMinutes: 15);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(
            new ParkingLotMembershipInfo(true, "Staff"));

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            Guid.NewGuid().ToString(),
            10000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10000, result.Value!.AmountPaid);
        Assert.Equal(2, _paymentRepository.AddedPayments.Count);

        // Chứng từ Ví cũ giữ nguyên 20,000; chứng từ Tiền mặt mới tạo riêng với PaymentSequence = 2
        Assert.Equal(20000, previousPayment.TotalFee);
        Assert.Equal(20000, previousPayment.AmountPaid);
        Assert.Equal(PaymentMethod.Wallet, previousPayment.PaymentMethod);

        var overstayCash = _paymentRepository.AddedPayments[1];
        Assert.NotEqual(previousPayment.Id, overstayCash.Id);
        Assert.Equal(2, overstayCash.PaymentSequence);
        Assert.Equal(PaymentMethod.Cash, overstayCash.PaymentMethod);
        Assert.Equal(10000, overstayCash.AmountPaid);
        Assert.Equal(overstayCash.Id, result.Value.TransactionId);

        Assert.Single(_auditWriter.Entries);
        Assert.Equal(overstayCash.Id, _auditWriter.Entries[0].Details!["paymentId"]);
        Assert.Equal(2, _auditWriter.Entries[0].Details!["paymentSequence"]);

        Assert.Single(_outboxWriter.EnqueuedEvents);
        Assert.Equal(overstayCash.Id, _outboxWriter.EnqueuedEvents[0].CorrelationId);
    }

    [Fact]
    public async Task Handle_WhenConcurrentUniqueConstraintViolation_ReleasesPendingKey_AndReturnsSessionAlreadyPaid()
    {
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
            Guid.NewGuid(),
            null,
            new PricingSnapshotDto("P1", "PerTurn", 60, 20000));

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(ctx);
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(
            new ParkingLotMembershipInfo(true, "Staff"));

        // Giả lập kênh khác vừa chốt thành công đợt 1 trước khi Cash lưu DB
        _paymentRepository.OnSaveChangesAsync = () =>
        {
            var winningPayment = ParkingFeePayment.CreateWalletPayment(
                sessionId, lotId, Guid.NewGuid(), ctx.AccountId!.Value, "51A-12345", "Car",
                20000, 0, 20000, _fixedNow, _fixedNow.AddMinutes(15));
            _paymentRepository.AddedPayments.Add(winningPayment);
            throw new DbUpdateException("23505 duplicate key");
        };

        var command = new ConfirmCashParkingFeePaymentCommand(
            sessionId,
            staffAccountId,
            "Staff",
            Guid.NewGuid().ToString(),
            20000);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenOverstayLagDoubleChargeAttempted_ReturnsSessionAlreadyPaid()
    {
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        // Đợt 2 đã trả bằng Ví lúc -5p nhưng Parking chưa cập nhật LastPaidAtUtc (-45p)
        var seq1 = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, Guid.NewGuid(), Guid.NewGuid(), "51A-12345", "Car",
            20000, 0, 20000, _fixedNow.AddMinutes(-45), _fixedNow.AddMinutes(-30), paymentSequence: 1);
        var seq2 = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, Guid.NewGuid(), seq1.AccountId!.Value, "51A-12345", "Car",
            10000, 0, 10000, _fixedNow.AddMinutes(-5), _fixedNow.AddMinutes(10), paymentSequence: 2);
        _paymentRepository.AddedPayments.Add(seq1);
        _paymentRepository.AddedPayments.Add(seq2);
        _paymentRepository.HasSucceeded = true;

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _fixedNow.AddHours(-2),
            seq1.AccountId, null, new PricingSnapshotDto("P1", "Block", 15, 5000),
            LastPaidAtUtc: _fixedNow.AddMinutes(-45),
            ArrivedAtExitUtc: _fixedNow,
            GracePeriodMinutes: 15));

        var result = await CreateHandler().Handle(
            new ConfirmCashParkingFeePaymentCommand(sessionId, staffAccountId, "Staff", Guid.NewGuid().ToString(), 10000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.Equal(2, _paymentRepository.AddedPayments.Count);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenStalePreviousSequenceOrExpiredPendingQrExists_SupersedesQrAndConfirmsCash()
    {
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var seq1 = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, Guid.NewGuid(), Guid.NewGuid(), "51A-12345", "Car",
            20000, 0, 20000, _fixedNow.AddMinutes(-45), _fixedNow.AddMinutes(-30), paymentSequence: 1);
        var staleSeq1Qr = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, seq1.AccountId, "51A-12345", "Car",
            20000, 0, 20000, _fixedNow.AddMinutes(-46), _fixedNow.AddMinutes(5), "OLD_SEQ1_QR", "https://sandbox.vnpayment.vn", paymentSequence: 1);

        _paymentRepository.AddedPayments.Add(seq1);
        _paymentRepository.HasSucceeded = true;
        _paymentRepository.PendingPayment = staleSeq1Qr;

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _fixedNow.AddHours(-2),
            seq1.AccountId, null, new PricingSnapshotDto("P1", "Block", 15, 5000),
            LastPaidAtUtc: _fixedNow.AddMinutes(-45),
            ArrivedAtExitUtc: _fixedNow,
            GracePeriodMinutes: 15));

        var result = await CreateHandler().Handle(
            new ConfirmCashParkingFeePaymentCommand(sessionId, staffAccountId, "Staff", Guid.NewGuid().ToString(), 10000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Superseded, staleSeq1Qr.Status);
        Assert.Equal(2, _paymentRepository.AddedPayments[1].PaymentSequence);

        // Trường hợp 2: mã QR cùng đợt 2 nhưng đã quá hạn quét cũng phải được hủy thay vì chặn tiền mặt
        var session2 = Guid.NewGuid();
        var expiredSeq2Qr = ParkingFeePayment.CreateVnPayPayment(
            session2, lotId, null, "51A-99999", "Car",
            10000, 0, 10000, _fixedNow.AddMinutes(-20), _fixedNow.AddMinutes(-5), "EXPIRED_QR", "https://sandbox.vnpayment.vn", paymentSequence: 1);
        _paymentRepository.HasSucceeded = false;
        _paymentRepository.PendingPayment = expiredSeq2Qr;
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            session2, lotId, "51A-99999", "Car", "Active", _fixedNow.AddHours(-1),
            null, null, new PricingSnapshotDto("P1", "PerTurn", 60, 10000)));

        var result2 = await CreateHandler().Handle(
            new ConfirmCashParkingFeePaymentCommand(session2, staffAccountId, "Staff", Guid.NewGuid().ToString(), 10000),
            CancellationToken.None);

        Assert.True(result2.IsSuccess);
        Assert.Equal(PaymentStatus.Superseded, expiredSeq2Qr.Status);
    }

    [Fact]
    public async Task Handle_WhenConcurrentSupersedeHitsConcurrencyException_AndCycleAlreadyPaid_ReturnsSessionAlreadyPaid()
    {
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var expiredQr = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, null, "51A-12345", "Car",
            20000, 0, 20000, _fixedNow.AddMinutes(-20), _fixedNow.AddMinutes(-5), "EXPIRED_QR", "https://sandbox.vnpayment.vn", paymentSequence: 1);

        _paymentRepository.PendingPayment = expiredQr;
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _fixedNow.AddHours(-1),
            null, null, new PricingSnapshotDto("P1", "PerTurn", 60, 20000)));
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Staff"));

        // Giả lập VNPay IPN vừa chốt xong cùng đợt làm thay đổi phiên bản dòng QR cũ
        _paymentRepository.OnSaveChangesAsync = () =>
        {
            var winningPayment = ParkingFeePayment.CreateWalletPayment(
                sessionId, lotId, Guid.NewGuid(), Guid.NewGuid(), "51A-12345", "Car",
                20000, 0, 20000, _fixedNow, _fixedNow.AddMinutes(15), paymentSequence: 1);
            _paymentRepository.AddedPayments.Add(winningPayment);
            throw new DbUpdateConcurrencyException("xmin mismatch");
        };

        var result = await CreateHandler().Handle(
            new ConfirmCashParkingFeePaymentCommand(sessionId, staffAccountId, "Staff", Guid.NewGuid().ToString(), 20000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenConcurrentSupersedeHitsConcurrencyException_AndNewActiveQrCreated_ReturnsPaymentAlreadyInProgress()
    {
        var sessionId = Guid.NewGuid();
        var staffAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var expiredQr = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, null, "51A-12345", "Car",
            20000, 0, 20000, _fixedNow.AddMinutes(-20), _fixedNow.AddMinutes(-5), "EXPIRED_QR", "https://sandbox.vnpayment.vn", paymentSequence: 1);

        _paymentRepository.PendingPayment = expiredQr;
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _fixedNow.AddHours(-1),
            null, null, new PricingSnapshotDto("P1", "PerTurn", 60, 20000)));
        _membershipGateway.ResultToReturn = Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Staff"));

        // Giả lập luồng tạo QR mới vừa thay thế dòng QR hết hạn trước khi tiền mặt lưu DB
        _paymentRepository.OnSaveChangesAsync = () =>
        {
            _paymentRepository.PendingPayment = ParkingFeePayment.CreateVnPayPayment(
                sessionId, lotId, null, "51A-12345", "Car",
                20000, 0, 20000, _fixedNow, _fixedNow.AddMinutes(10), "NEW_QR", "https://sandbox.vnpayment.vn", paymentSequence: 1);
            throw new DbUpdateConcurrencyException("xmin mismatch");
        };

        var result = await CreateHandler().Handle(
            new ConfirmCashParkingFeePaymentCommand(sessionId, staffAccountId, "Staff", Guid.NewGuid().ToString(), 20000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PayParkingFeeErrors.PaymentAlreadyInProgress.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }
}
