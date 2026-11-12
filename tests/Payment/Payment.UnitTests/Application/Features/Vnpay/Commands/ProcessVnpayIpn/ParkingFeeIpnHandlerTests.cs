using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

public class ParkingFeeIpnHandlerTests
{
    private class FakeParkingFeePaymentRepository : IParkingFeePaymentRepository
    {
        private readonly Dictionary<string, ParkingFeePayment> _paymentsByTxnRef = new();
        public bool HasSucceededPayment { get; set; }
        public bool SaveChangesCalled { get; private set; }
        public Exception? ExceptionToThrowOnSaveChanges { get; set; }
        public Action<ParkingFeePayment>? OnReload { get; set; }

        public void SeedPayment(ParkingFeePayment payment)
        {
            if (payment.TxnRef != null)
            {
                _paymentsByTxnRef[payment.TxnRef] = payment;
            }
        }

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult(HasSucceededPayment);

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult<ParkingFeePayment?>(null);

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
        {
            _paymentsByTxnRef.TryGetValue(txnRef, out var payment);
            return Task.FromResult(payment);
        }

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            OnReload?.Invoke(payment);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            if (ExceptionToThrowOnSaveChanges != null)
            {
                throw ExceptionToThrowOnSaveChanges;
            }
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            SeedPayment(payment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var payment = _paymentsByTxnRef.Values.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(payment);
        }

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
        {
            var payment = _paymentsByTxnRef.Values.FirstOrDefault(p => p.Id == sessionOrPaymentId || p.SessionId == sessionOrPaymentId);
            return Task.FromResult(payment);
        }
    }

    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _walletsByUserId = new();

        public void SeedWallet(Wallet wallet)
        {
            _walletsByUserId[wallet.UserId] = wallet;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var wallet = _walletsByUserId.Values.FirstOrDefault(w => w.Id == id);
            return Task.FromResult(wallet);
        }

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            _walletsByUserId.TryGetValue(userId, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (!_walletsByUserId.TryGetValue(userId, out var wallet))
            {
                wallet = Wallet.Create(userId);
                _walletsByUserId[userId] = wallet;
            }
            return Task.FromResult(wallet);
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
        {
            _walletsByUserId[wallet.UserId] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class FakeOutboxWriter : IOutboxWriter
    {
        public List<(object Event, string EventType, int EventVersion, Guid? CorrelationId)> Enqueued { get; } = new();

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null) where TEvent : class
        {
            Enqueued.Add((integrationEvent, eventType, eventVersion, correlationId));
        }
    }

    private class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    }

    private class DbUpdateConcurrencyException : Exception
    {
        public DbUpdateConcurrencyException(string message) : base(message) { }
    }

    private readonly FakeParkingFeePaymentRepository _paymentRepository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeOutboxWriter _outboxWriter = new();
    private readonly FakeClock _clock = new();
    private readonly ParkingFeeIpnHandler _handler;

    public ParkingFeeIpnHandlerTests()
    {
        _handler = new ParkingFeeIpnHandler(
            _paymentRepository,
            _walletRepository,
            _outboxWriter,
            _clock,
            NullLogger<ParkingFeeIpnHandler>.Instance);
    }

    private static ParkingFeePayment CreateSamplePayment(
        Guid? accountId = null,
        long amountPaid = 35000,
        string txnRef = "PAY_TXN_001")
    {
        return ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: accountId ?? Guid.NewGuid(),
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: amountPaid,
            prepaidAmount: 0,
            amountPaid: amountPaid,
            feeCalculatedAtUtc: DateTimeOffset.UtcNow,
            expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            txnRef: txnRef,
            paymentUrl: "https://sandbox.vnpayment.vn");
    }

    private static Dictionary<string, string> CreateValidQuery(
        string txnRef,
        long amountPaid,
        string responseCode = "00",
        string transactionStatus = "00")
    {
        return new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txnRef,
            ["vnp_Amount"] = (amountPaid * 100).ToString(),
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TransactionNo"] = "14123456",
            ["vnp_BankCode"] = "NCB"
        };
    }

    // Kiểm tra khi không tìm thấy hóa đơn
    [Fact]
    public async Task HandleAsync_WhenOrderNotFound_ReturnsRspCode01()
    {
        var query = CreateValidQuery("UNKNOWN_REF", 35000);

        var result = await _handler.HandleAsync(query, "UNKNOWN_REF", CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.OrderNotFound, result.RspCode);
    }

    // Kiểm tra khi đơn đã được xử lý trước đó
    [Fact]
    public async Task HandleAsync_WhenPaymentAlreadyConfirmed_ReturnsRspCode02()
    {
        var payment = CreateSamplePayment();
        payment.MarkAsSucceeded("14123456", _clock.UtcNow, _clock.UtcNow.AddMinutes(15));
        _paymentRepository.SeedPayment(payment);

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.AlreadyConfirmed, result.RspCode);
    }

    // Kiểm tra khi số tiền không khớp hóa đơn
    [Fact]
    public async Task HandleAsync_WhenAmountMismatch_ReturnsRspCode04()
    {
        var payment = CreateSamplePayment(amountPaid: 35000);
        _paymentRepository.SeedPayment(payment);

        var query = CreateValidQuery(payment.TxnRef!, 20000);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.InvalidAmount, result.RspCode);
    }

    // Kiểm tra khi số tiền không đúng định dạng số
    [Fact]
    public async Task HandleAsync_WhenAmountInvalidFormat_ReturnsRspCode04()
    {
        var payment = CreateSamplePayment();
        _paymentRepository.SeedPayment(payment);

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);
        query["vnp_Amount"] = "invalid_number";

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.InvalidAmount, result.RspCode);
    }

    // Kiểm tra khi cổng ngân hàng báo thanh toán thất bại
    [Fact]
    public async Task HandleAsync_WhenGatewayFailed_MarksAsFailedAndSaves_ReturnsRspCode00()
    {
        var payment = CreateSamplePayment();
        _paymentRepository.SeedPayment(payment);

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid, responseCode: "24", transactionStatus: "02");

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("24", payment.FailureCode);
        Assert.True(_paymentRepository.SaveChangesCalled);
    }

    // Kiểm tra khi thanh toán thành công và mở cổng xe
    [Fact]
    public async Task HandleAsync_WhenGatewaySucceeded_MarksAsSucceeded_EnqueuesOutbox_ReturnsRspCode00()
    {
        var payment = CreateSamplePayment();
        _paymentRepository.SeedPayment(payment);
        _paymentRepository.HasSucceededPayment = false;

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("14123456", payment.GatewayTransactionNo);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), payment.ExitWindowExpiresUtc);
        Assert.True(_paymentRepository.SaveChangesCalled);

        Assert.Single(_outboxWriter.Enqueued);
        var (ev, eventType, version, correlationId) = _outboxWriter.Enqueued[0];
        Assert.Equal("payment.session-paid.v1", eventType);
        Assert.Equal(1, version);
        Assert.Equal(payment.Id, correlationId);

        var paidEvent = Assert.IsType<ParkingSessionPaidIntegrationEvent>(ev);
        Assert.Equal(payment.SessionId, paidEvent.SessionId);
        Assert.Equal(payment.AmountPaid, paidEvent.AmountPaid);
        Assert.Equal(_clock.UtcNow, paidEvent.PaidAtUtc);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), paidEvent.ExitWindowExpiresUtc);
    }

    // Kiểm tra khi thu tiền thừa hoàn vào ví thành viên
    [Fact]
    public async Task HandleAsync_WhenGatewaySucceeded_AndAlreadyPaid_WithAccount_MarksAsSurplus_CreditsWallet_ReturnsRspCode00()
    {
        var accountId = Guid.NewGuid();
        var payment = CreateSamplePayment(accountId: accountId, amountPaid: 40000);
        _paymentRepository.SeedPayment(payment);
        _paymentRepository.HasSucceededPayment = true;

        var wallet = Wallet.Create(accountId);
        _walletRepository.SeedWallet(wallet);

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Surplus, payment.Status);
        Assert.Equal("14123456", payment.GatewayTransactionNo);
        Assert.Equal("RefundedToWallet", payment.SurplusRefundStatus);
        Assert.True(_paymentRepository.SaveChangesCalled);

        // Kiểm tra tiền đã được hoàn vào ví
        Assert.Equal(40000m, wallet.Balance);
        Assert.Single(wallet.LedgerEntries);
        var entry = wallet.LedgerEntries.First();
        Assert.Equal(LedgerEntryType.Refund, entry.EntryType);
        Assert.Equal(40000m, entry.Amount);
        Assert.Equal($"refund:vnpay_surplus:{payment.Id}", entry.IdempotencyKey);

        // Không gửi tin nhắn mở cổng vì phiên đã thanh toán trước
        Assert.Empty(_outboxWriter.Enqueued);
    }

    // Kiểm tra khi thu tiền thừa của khách chưa đăng ký
    [Fact]
    public async Task HandleAsync_WhenGatewaySucceeded_AndAlreadyPaid_WithoutAccount_MarksAsSurplus_NeedsManualRefund_ReturnsRspCode00()
    {
        var payment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: Guid.NewGuid(),
            parkingLotId: Guid.NewGuid(),
            accountId: null,
            plateNumber: "51F-99999",
            vehicleType: "Motorbike",
            totalFee: 10000,
            prepaidAmount: 0,
            amountPaid: 10000,
            feeCalculatedAtUtc: DateTimeOffset.UtcNow,
            expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            txnRef: "TXN_GUEST_001",
            paymentUrl: "https://sandbox.vnpayment.vn");

        _paymentRepository.SeedPayment(payment);
        _paymentRepository.HasSucceededPayment = true;

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Surplus, payment.Status);
        Assert.Equal("NeedsManualRefund", payment.SurplusRefundStatus);
        Assert.True(_paymentRepository.SaveChangesCalled);
        Assert.Empty(_outboxWriter.Enqueued);
    }

    // Kiểm tra xung đột dữ liệu nhưng đơn đã thành công
    [Fact]
    public async Task HandleAsync_WhenConcurrencyExceptionAndReloadedSucceeded_ReturnsRspCode02()
    {
        var payment = CreateSamplePayment();
        _paymentRepository.SeedPayment(payment);
        _paymentRepository.ExceptionToThrowOnSaveChanges = new DbUpdateConcurrencyException("Concurrency conflict");
        _paymentRepository.OnReload = p => p.MarkAsSucceeded("14123456", _clock.UtcNow, _clock.UtcNow.AddMinutes(15));

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.AlreadyConfirmed, result.RspCode);
    }

    // Kiểm tra xung đột dữ liệu nhưng đơn chưa thành công
    [Fact]
    public async Task HandleAsync_WhenConcurrencyExceptionAndReloadedNotSucceeded_ReturnsRspCode99()
    {
        var payment = CreateSamplePayment();
        _paymentRepository.SeedPayment(payment);
        _paymentRepository.ExceptionToThrowOnSaveChanges = new DbUpdateConcurrencyException("Concurrency conflict");
        _paymentRepository.OnReload = p => p.MarkAsFailed("99", _clock.UtcNow);

        var query = CreateValidQuery(payment.TxnRef!, payment.AmountPaid);

        var result = await _handler.HandleAsync(query, payment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.UnknownError, result.RspCode);
    }
}
