using System.Text.Json;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public class PayParkingFeeByWalletCommandHandlerTests
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
                PayParkingFeeErrors.ParkingServiceUnavailable));
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

    private class DbUpdateConcurrencyException(string message) : Exception(message) { }
    private class DbUpdateException(string message) : Exception(message) { }

    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();
        public bool ThrowConcurrencyOnSave { get; set; }
        public Exception? ExceptionToThrowOnSave { get; set; }
        public Action? OnBeforeSave { get; set; }

        public void SeedWallet(Wallet wallet)
        {
            _wallets[wallet.UserId] = wallet;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var wallet = _wallets.Values.FirstOrDefault(w => w.Id == id);
            return Task.FromResult(wallet);
        }

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            _wallets.TryGetValue(userId, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (_wallets.TryGetValue(userId, out var wallet))
            {
                return Task.FromResult(wallet);
            }

            var newWallet = Wallet.Create(userId);
            _wallets[userId] = newWallet;
            return Task.FromResult(newWallet);
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
        {
            _wallets[wallet.UserId] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            OnBeforeSave?.Invoke();

            if (ThrowConcurrencyOnSave)
            {
                // Giả lập ngoại lệ xung đột số dư đồng thời
                throw new DbUpdateConcurrencyException("Concurrency conflict");
            }

            if (ExceptionToThrowOnSave != null)
            {
                throw ExceptionToThrowOnSave;
            }

            return Task.CompletedTask;
        }
    }

    private class FakeParkingFeePaymentRepository : IParkingFeePaymentRepository
    {
        public bool SessionExists { get; set; }
        public readonly List<ParkingFeePayment> AddedPayments = [];

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(SessionExists);
        }

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(SessionExists);
        }

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
        {
            return Task.FromResult(AddedPayments.LastOrDefault(p => p.SessionId == sessionId && p.Status == "Pending"));
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
        public readonly Dictionary<string, PaymentIdempotencyRecord> Records = new();
        public bool ReleaseCalled { get; private set; }

        public Task<PaymentIdempotencyRecord?> FindAsync(
            Guid accountId, string operation, string key, CancellationToken ct)
        {
            Records.TryGetValue($"{accountId}:{operation}:{key}", out var record);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var key = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            if (Records.ContainsKey(key))
            {
                return Task.FromResult(false);
            }

            Records[key] = record;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
        {
            var key = $"{record.AccountId}:{record.Operation}:{record.IdempotencyKey}";
            Records[key] = record;
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

    private class FakeOutboxWriter : IOutboxWriter
    {
        public readonly List<object> EnqueuedEvents = [];

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null) where TEvent : class
        {
            EnqueuedEvents.Add(integrationEvent);
        }
    }

    private readonly FakeParkingSessionGateway _parkingGateway = new();
    private readonly FakePrepaymentReader _prepaymentReader = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeParkingFeePaymentRepository _paymentRepository = new();
    private readonly FakeIdempotencyStore _idempotencyStore = new();
    private readonly FakeOutboxWriter _outboxWriter = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

    private PayParkingFeeByWalletCommandHandler CreateHandler()
    {
        return new PayParkingFeeByWalletCommandHandler(
            _parkingGateway,
            _prepaymentReader,
            _walletRepository,
            _paymentRepository,
            _idempotencyStore,
            _outboxWriter,
            _clock);
    }

    [Fact]
    public async Task Handle_WhenCallerNotCustomer_ReturnsRoleNotAllowed()
    {
        var handler = CreateHandler();
        var command = new PayParkingFeeByWalletCommand(
            SessionId: Guid.NewGuid(),
            ExpectedAmountDue: 20000,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: Guid.NewGuid(),
            CallerRole: "Staff");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.RoleNotAllowed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenIdempotencyKeyInvalid_ReturnsIdempotencyKeyInvalid()
    {
        var handler = CreateHandler();
        var command = new PayParkingFeeByWalletCommand(
            SessionId: Guid.NewGuid(),
            ExpectedAmountDue: 20000,
            IdempotencyKey: "not-a-guid",
            CallerAccountId: Guid.NewGuid(),
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.IdempotencyKeyInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenExpectedAmountInvalid_ReturnsExpectedAmountInvalid()
    {
        var handler = CreateHandler();
        var command = new PayParkingFeeByWalletCommand(
            SessionId: Guid.NewGuid(),
            ExpectedAmountDue: -1,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: Guid.NewGuid(),
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.ExpectedAmountInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenSessionAccessDenied_ReturnsSessionAccessDenied()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var differentAccountId = Guid.NewGuid();

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: differentAccountId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.SessionAccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenFeeChanged_ReturnsFeeChanged()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 30000))); // Phí thực tế là 30000

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000, // Khách kỳ vọng trả 20000
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.FeeChanged.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenInsufficientBalance_ReturnsWalletInsufficientBalance()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        _walletRepository.SeedWallet(wallet); // Số dư ví = 0

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.WalletInsufficientBalance.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_DeductsWalletBalanceAndEnqueuesOutboxEvent()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20000, result.Value!.AmountPaid);
        Assert.Equal(80000, result.Value!.WalletBalanceRemaining);
        Assert.Equal(80000, wallet.Balance);
        Assert.Single(_paymentRepository.AddedPayments);
        Assert.Equal(2, _outboxWriter.EnqueuedEvents.Count);
        Assert.IsType<ParkingSessionPaidIntegrationEvent>(_outboxWriter.EnqueuedEvents[0]);
        var invoice = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(_outboxWriter.EnqueuedEvents[1]);
        Assert.Equal(20000, invoice.PaidAmount);
        Assert.Equal(20000, invoice.TotalFee);
        Assert.Equal(0, invoice.AmountDue);
        Assert.Equal("51A-***.45", invoice.MaskedPlateNumber);
    }

    [Fact]
    public async Task Handle_WhenAmountDueIsZero_SucceedsWithoutDebitingWallet()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 50000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        _prepaymentReader.PrepaidAmount = 20000;
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: bookingId,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 0,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.AmountPaid);
        Assert.Equal(50000, result.Value!.WalletBalanceRemaining);
        Assert.Equal(50000, wallet.Balance);
        Assert.Single(_paymentRepository.AddedPayments);
        Assert.Equal(0, _paymentRepository.AddedPayments[0].AmountPaid);
        Assert.Equal(2, _outboxWriter.EnqueuedEvents.Count);
        var paidEvent = Assert.IsType<ParkingSessionPaidIntegrationEvent>(_outboxWriter.EnqueuedEvents[0]);
        Assert.Equal(0, paidEvent.AmountPaid);
        Assert.Equal(sessionId, paidEvent.SessionId);
        var invoice = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(_outboxWriter.EnqueuedEvents[1]);
        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal(20000, invoice.TotalFee);
        Assert.Equal(0, invoice.AmountDue);
    }

    [Fact]
    public async Task Handle_WhenConcurrencyOccurs_ReleasesPendingAndReturnsWalletBusy()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);
        _walletRepository.ThrowConcurrencyOnSave = true; // Kích hoạt ngoại lệ xung đột đồng thời

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.WalletBusy.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenPrepaidExceedsTotalFee_RefundsSurplusToWallet_UnderBR10b()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 50000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        _prepaymentReader.PrepaidAmount = 20000; // Khách đã cọc 20.000đ

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: bookingId,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 15000))); // Phí thực tế là 15.000đ -> Dư 5.000đ

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 0,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.AmountPaid);
        Assert.Equal(55000, result.Value!.WalletBalanceRemaining);
        Assert.Equal(55000, wallet.Balance); // Ví ban đầu 50.000 + 5.000 dư = 55.000đ
        Assert.Single(_paymentRepository.AddedPayments);
        Assert.Equal(2, _outboxWriter.EnqueuedEvents.Count);
        Assert.IsType<ParkingSessionPaidIntegrationEvent>(_outboxWriter.EnqueuedEvents[0]);
        var invoice = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(_outboxWriter.EnqueuedEvents[1]);
        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal(15000, invoice.TotalFee);
        Assert.Equal(0, invoice.AmountDue);
    }

    [Fact]
    public async Task Handle_WhenInsufficientBalance_ReleasesPending_AllowingRetryAfterTopup()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var sharedKey = Guid.NewGuid().ToString();

        var wallet = Wallet.Create(callerId);
        _walletRepository.SeedWallet(wallet); // Ví rỗng 0đ

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: Guid.NewGuid(),
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: sharedKey,
            CallerAccountId: callerId,
            CallerRole: "Customer");

        // Lần 1: Thiếu tiền ví -> Thất bại nhưng phải giải phóng khóa tạm
        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.IsFailure);
        Assert.Equal(PayParkingFeeErrors.WalletInsufficientBalance.Code, result1.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);

        // Khách nạp tiền vào ví
        wallet.AddEntry(LedgerEntryType.TopUp, 50000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);

        // Lần 2: Thử lại với cùng idempotency key -> Phải thành công!
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal(20000, result2.Value!.AmountPaid);
        Assert.Equal(30000, result2.Value!.WalletBalanceRemaining);
    }

    [Fact]
    public async Task Handle_WhenSessionReopenedDueToOverstay_AllowsOverstayFeePayment_AndExtendsExitWindow()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var sharedKey = Guid.NewGuid().ToString();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        // Đã có 1 hóa đơn thanh toán thành công trước đó
        var previousPayment = ParkingFeePayment.CreateWalletPayment(
            sessionId,
            lotId,
            wallet.Id,
            callerId,
            "51A-12345",
            "Car",
            20000,
            0,
            20000,
            _clock.UtcNow.AddMinutes(-45),
            _clock.UtcNow.AddMinutes(-30));
        _paymentRepository.AddedPayments.Add(previousPayment);
        _paymentRepository.SessionExists = true;

        // Phiên bị reopen sang Active sau khi quá hạn 15p ra cổng (LastPaidAtUtc = 45 phút trước)
        // Block 15p = 5000đ. Đã quá 45 - 15 = 30 phút -> 2 blocks = 10000đ
        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: lotId,
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-2),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "Block", 15, 5000),
            LastPaidAtUtc: _clock.UtcNow.AddMinutes(-45),
            ArrivedAtExitUtc: _clock.UtcNow,
            GracePeriodMinutes: 15));

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 10000,
            IdempotencyKey: sharedKey,
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10000, result.Value!.AmountPaid);
        Assert.Equal(90000, result.Value!.WalletBalanceRemaining);
        Assert.Equal(2, _paymentRepository.AddedPayments.Count);

        // Hóa đơn cũ giữ nguyên, hóa đơn quá giờ tạo dòng mới với PaymentSequence = 2
        Assert.Equal(20000, previousPayment.TotalFee);
        Assert.Equal(20000, previousPayment.AmountPaid);
        var overstayPayment = _paymentRepository.AddedPayments[1];
        Assert.NotEqual(previousPayment.Id, overstayPayment.Id);
        Assert.Equal(2, overstayPayment.PaymentSequence);
        Assert.Equal(10000, overstayPayment.TotalFee);
        Assert.Equal(10000, overstayPayment.AmountPaid);
        Assert.Equal(PaymentMethod.Wallet, overstayPayment.PaymentMethod);
        Assert.Equal(overstayPayment.Id, result.Value.TransactionId);

        Assert.Equal(2, _outboxWriter.EnqueuedEvents.Count);
        var paidEvent = Assert.IsType<ParkingSessionPaidIntegrationEvent>(_outboxWriter.EnqueuedEvents[0]);
        Assert.Equal(overstayPayment.Id, paidEvent.PaymentId);
        var invoice = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(_outboxWriter.EnqueuedEvents[1]);
        Assert.Equal(overstayPayment.Id, invoice.PaymentId);
        Assert.Equal(10000, invoice.PaidAmount);
        Assert.Equal(10000, invoice.TotalFee);
        Assert.Equal(0, invoice.AmountDue);
    }

    [Fact]
    public async Task Handle_WhenConcurrentUniqueConstraintViolation_ReleasesPendingKey_AndReturnsSessionAlreadyPaid()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var sharedKey = Guid.NewGuid().ToString();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup balance", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            SessionId: sessionId,
            ParkingLotId: lotId,
            PlateNumber: "51A-12345",
            VehicleType: "Car",
            Status: "Active",
            EntryTimeUtc: _clock.UtcNow.AddHours(-1),
            AccountId: callerId,
            BookingId: null,
            Pricing: new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        // Giả lập request khác vừa commit xong đợt 1 ngay trước khi request này lưu DB
        _walletRepository.OnBeforeSave = () =>
        {
            var winningPayment = ParkingFeePayment.CreateCashPayment(
                sessionId, lotId, callerId, "51A-12345", "Car", 20000, 0, 20000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15));
            _paymentRepository.AddedPayments.Add(winningPayment);
        };
        _walletRepository.ExceptionToThrowOnSave = new DbUpdateException("23505 duplicate key");

        var command = new PayParkingFeeByWalletCommand(
            SessionId: sessionId,
            ExpectedAmountDue: 20000,
            IdempotencyKey: sharedKey,
            CallerAccountId: callerId,
            CallerRole: "Customer");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenOverstayLagDoubleChargeAttempted_ReturnsSessionAlreadyPaid_WithoutDebitingWallet()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        // Đợt 2 đã thanh toán lúc -10p nhưng Parking chưa kịp nhận tin mở cổng nên LastPaidAtUtc vẫn là -45p
        var seq1 = ParkingFeePayment.CreateWalletPayment(
            sessionId, lotId, wallet.Id, callerId, "51A-12345", "Car",
            20000, 0, 20000, _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30), paymentSequence: 1);
        var seq2 = ParkingFeePayment.CreateCashPayment(
            sessionId, lotId, callerId, "51A-12345", "Car",
            10000, 0, 10000, _clock.UtcNow.AddMinutes(-10), _clock.UtcNow.AddMinutes(5), paymentSequence: 2);
        _paymentRepository.AddedPayments.Add(seq1);
        _paymentRepository.AddedPayments.Add(seq2);
        _paymentRepository.SessionExists = true;

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-2),
            callerId, null, new PricingSnapshotDto("P1", "Block", 15, 5000),
            LastPaidAtUtc: _clock.UtcNow.AddMinutes(-45),
            ArrivedAtExitUtc: _clock.UtcNow,
            GracePeriodMinutes: 15));

        var result = await handler.Handle(
            new PayParkingFeeByWalletCommand(sessionId, 10000, Guid.NewGuid().ToString(), callerId, "Customer"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayParkingFeeErrors.SessionAlreadyPaid.Code, result.Error!.Code);
        Assert.Equal(100000m, wallet.Balance);
        Assert.Equal(2, _paymentRepository.AddedPayments.Count);
        Assert.True(_idempotencyStore.ReleaseCalled);
    }

    [Fact]
    public async Task Handle_WhenPendingQrExists_SupersedesPendingQrOnWalletPayment()
    {
        var handler = CreateHandler();
        var sessionId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var wallet = Wallet.Create(callerId);
        wallet.AddEntry(LedgerEntryType.TopUp, 100000, "Topup", ReferenceType.VnpayTransaction, Guid.NewGuid().ToString(), null, null, callerId);
        _walletRepository.SeedWallet(wallet);

        var staleQr = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, callerId, "51A-12345", "Car",
            20000, 0, 20000, _clock.UtcNow.AddMinutes(-5), _clock.UtcNow.AddMinutes(10), "QR_ABANDONED", "https://sandbox.vnpayment.vn");
        _paymentRepository.AddedPayments.Add(staleQr);

        _parkingGateway.ResultToReturn = Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            sessionId, lotId, "51A-12345", "Car", "Active", _clock.UtcNow.AddHours(-1),
            callerId, null, new PricingSnapshotDto("P1", "PerTurn", 0, 20000)));

        var result = await handler.Handle(
            new PayParkingFeeByWalletCommand(sessionId, 20000, Guid.NewGuid().ToString(), callerId, "Customer"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Superseded, staleQr.Status);
    }
}
