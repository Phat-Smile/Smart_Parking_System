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
        public bool ClearChangeTrackerCalled { get; private set; }
        public Exception? ExceptionToThrowOnSaveChanges { get; set; }
        public Func<Task>? OnSaveChangesAsync { get; set; }
        public Action<ParkingFeePayment>? OnReload { get; set; }

        public void ClearChangeTracker()
        {
            ClearChangeTrackerCalled = true;
        }

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

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            if (OnSaveChangesAsync != null)
            {
                await OnSaveChangesAsync();
            }
            if (ExceptionToThrowOnSaveChanges != null)
            {
                throw ExceptionToThrowOnSaveChanges;
            }
            SaveChangesCalled = true;
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
            var payment = _paymentsByTxnRef.Values
                .Where(p => (p.Id == sessionOrPaymentId || p.SessionId == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.PaymentSequence)
                .ThenByDescending(p => p.PaymentTimeUtc)
                .FirstOrDefault();
            if (payment == null && HasSucceededPayment)
            {
                var now = DateTimeOffset.UtcNow;
                var fallback = CreateSamplePayment(amountPaid: 35000, txnRef: "OLD_TXN");
                fallback.MarkAsSucceeded("OLD_NO", now, now.AddMinutes(15));
                return Task.FromResult<ParkingFeePayment?>(fallback);
            }
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

    private class DbUpdateException : Exception
    {
        public DbUpdateException(string message) : base(message) { }
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

        Assert.Equal(2, _outboxWriter.Enqueued.Count);
        var (ev, eventType, version, correlationId) = _outboxWriter.Enqueued[0];
        Assert.Equal("payment.session-paid.v1", eventType);
        Assert.Equal(1, version);
        Assert.Equal(payment.Id, correlationId);

        var paidEvent = Assert.IsType<ParkingSessionPaidIntegrationEvent>(ev);
        Assert.Equal(payment.SessionId, paidEvent.SessionId);
        Assert.Equal(payment.AmountPaid, paidEvent.AmountPaid);
        Assert.Equal(_clock.UtcNow, paidEvent.PaidAtUtc);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), paidEvent.ExitWindowExpiresUtc);

        var (invEvt, invType, invVersion, invCorrelationId) = _outboxWriter.Enqueued[1];
        Assert.Equal("payment.electronic-invoice-issued.v1", invType);
        Assert.Equal(1, invVersion);
        Assert.Equal(payment.Id, invCorrelationId);

        var invoiceEvent = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(invEvt);
        Assert.Equal(payment.SessionId, invoiceEvent.SessionId);
        Assert.Equal(payment.Id, invoiceEvent.PaymentId);
        Assert.Equal(payment.AmountPaid, invoiceEvent.PaidAmount);
        Assert.Equal(payment.TotalFee, invoiceEvent.TotalFee);
        Assert.Equal(0, invoiceEvent.AmountDue);
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
        _paymentRepository.OnReload = p =>
        {
            if (p.Status != PaymentStatus.Succeeded)
            {
                p.MarkAsSucceeded("14123456", _clock.UtcNow, _clock.UtcNow.AddMinutes(15));
            }
        };

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

    // Kiểm tra khi thanh toán phụ phí quá hạn (Overstay) qua VNPay: chốt hóa đơn mới thành Succeeded và giữ nguyên hóa đơn cũ
    [Fact]
    public async Task HandleAsync_WhenOverstayPaymentViaVnpay_MarksNewPaymentSucceeded_PreservesOriginal_AndEnqueuesOutbox()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // Hóa đơn cũ đã hết hạn cách đây 15 phút (quá hạn lái xe ra)
        var oldPayment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: sessionId,
            parkingLotId: Guid.NewGuid(),
            accountId: accountId,
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 35000,
            prepaidAmount: 0,
            amountPaid: 35000,
            feeCalculatedAtUtc: _clock.UtcNow.AddMinutes(-45),
            expiresAtUtc: _clock.UtcNow.AddMinutes(-30),
            txnRef: "OLD_PAID_TXN",
            paymentUrl: "https://sandbox.vnpayment.vn",
            paymentSequence: 1);
        oldPayment.MarkAsSucceeded("14111111", _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30));
        _paymentRepository.SeedPayment(oldPayment);

        // Hóa đơn overstay mới quét tại cổng (đợt 2)
        var overstayPayment = ParkingFeePayment.CreateVnPayPayment(
            sessionId: sessionId,
            parkingLotId: oldPayment.ParkingLotId,
            accountId: accountId,
            plateNumber: "51F-12345",
            vehicleType: "Car",
            totalFee: 15000,
            prepaidAmount: 0,
            amountPaid: 15000,
            feeCalculatedAtUtc: _clock.UtcNow,
            expiresAtUtc: _clock.UtcNow.AddMinutes(15),
            txnRef: "OVERSTAY_TXN_002",
            paymentUrl: "https://sandbox.vnpayment.vn",
            paymentSequence: 2);
        _paymentRepository.SeedPayment(overstayPayment);

        var query = CreateValidQuery(overstayPayment.TxnRef!, overstayPayment.AmountPaid);

        var result = await _handler.HandleAsync(query, overstayPayment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Succeeded, overstayPayment.Status);
        Assert.Equal("14123456", overstayPayment.GatewayTransactionNo);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), overstayPayment.ExitWindowExpiresUtc);

        // Chứng từ gốc giữ nguyên bất biến
        Assert.Equal(35000, oldPayment.TotalFee);
        Assert.Equal(35000, oldPayment.AmountPaid);
        Assert.True(_paymentRepository.SaveChangesCalled);

        // Đảm bảo phát sự kiện mở cổng barie theo ID hóa đơn mới
        Assert.Equal(2, _outboxWriter.Enqueued.Count);
        var (ev, eventType, version, correlationId) = _outboxWriter.Enqueued[0];
        Assert.Equal("payment.session-paid.v1", eventType);
        Assert.Equal(1, version);
        Assert.Equal(overstayPayment.Id, correlationId);

        var paidEvent = Assert.IsType<ParkingSessionPaidIntegrationEvent>(ev);
        Assert.Equal(sessionId, paidEvent.SessionId);
        Assert.Equal(overstayPayment.Id, paidEvent.PaymentId);
        Assert.Equal(15000, paidEvent.AmountPaid);
        Assert.Equal(_clock.UtcNow, paidEvent.PaidAtUtc);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), paidEvent.ExitWindowExpiresUtc);

        var (invEv, invEventType, invVer, invCorrId) = _outboxWriter.Enqueued[1];
        Assert.Equal("payment.electronic-invoice-issued.v1", invEventType);
        Assert.Equal(1, invVer);
        Assert.Equal(overstayPayment.Id, invCorrId);

        var invoiceEvent = Assert.IsType<ElectronicInvoiceIssuedIntegrationEvent>(invEv);
        Assert.Equal(sessionId, invoiceEvent.SessionId);
        Assert.Equal(overstayPayment.Id, invoiceEvent.PaymentId);
        Assert.Equal(15000, invoiceEvent.PaidAmount);
        Assert.Equal(15000, invoiceEvent.TotalFee);
        Assert.Equal(0, invoiceEvent.AmountDue);
    }

    // Chặn lỗ hổng VNPay gửi lặp IPN overstay trong 15 phút để hoàn tiền khống vào ví
    [Fact]
    public async Task HandleAsync_WhenOverstayVnpayIpnReplayedWithinExitWindow_ReturnsRspCode02_WithoutDoubleRefundOrDuplicateOutbox()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var wallet = Wallet.Create(accountId);
        _walletRepository.SeedWallet(wallet);

        var oldPayment = ParkingFeePayment.CreateVnPayPayment(
            sessionId, Guid.NewGuid(), accountId, "51F-12345", "Car",
            35000, 0, 35000, _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30),
            "OLD_PAID_TXN", "https://sandbox.vnpayment.vn", paymentSequence: 1);
        oldPayment.MarkAsSucceeded("14111111", _clock.UtcNow.AddMinutes(-45), _clock.UtcNow.AddMinutes(-30));
        _paymentRepository.SeedPayment(oldPayment);

        var overstayPayment = ParkingFeePayment.CreateVnPayPayment(
            sessionId, oldPayment.ParkingLotId, accountId, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15),
            "OVERSTAY_TXN_REPLAY", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        _paymentRepository.SeedPayment(overstayPayment);

        var query = CreateValidQuery(overstayPayment.TxnRef!, overstayPayment.AmountPaid);

        // IPN lần 1 thành công
        var firstResult = await _handler.HandleAsync(query, overstayPayment.TxnRef!, CancellationToken.None);
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, firstResult.RspCode);

        // IPN lần 2 đến sau 10 giây (vẫn trong cửa sổ 15 phút) phải bị chặn hoàn toàn
        _clock.UtcNow = _clock.UtcNow.AddSeconds(10);
        var replayResult = await _handler.HandleAsync(query, overstayPayment.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.AlreadyConfirmed, replayResult.RspCode);
        Assert.Equal(PaymentStatus.Succeeded, overstayPayment.Status);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(wallet.LedgerEntries);
        Assert.Equal(2, _outboxWriter.Enqueued.Count);
    }

    // Đợt overstay đã được trả trước (VD: tiền mặt) -> IPN VNPay trễ của cùng đợt bị tính là thu thừa
    [Fact]
    public async Task HandleAsync_WhenOverstayCycleAlreadyPaid_LateVnpayIpn_MarksAsSurplus_AndCreditsRegisteredWallet()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var wallet = Wallet.Create(accountId);
        _walletRepository.SeedWallet(wallet);

        var paidSeq2 = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(14),
            "PAID_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        paidSeq2.MarkAsSucceeded("CASH_OR_WALLET", _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(14));
        _paymentRepository.SeedPayment(paidSeq2);

        var lateVnpaySeq2 = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow.AddMinutes(-2), _clock.UtcNow.AddMinutes(13),
            "LATE_VNPAY_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        _paymentRepository.SeedPayment(lateVnpaySeq2);

        var query = CreateValidQuery(lateVnpaySeq2.TxnRef!, lateVnpaySeq2.AmountPaid);
        var result = await _handler.HandleAsync(query, lateVnpaySeq2.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Surplus, lateVnpaySeq2.Status);
        Assert.Equal("RefundedToWallet", lateVnpaySeq2.SurplusRefundStatus);
        Assert.Equal(15000m, wallet.Balance);
        Assert.Empty(_outboxWriter.Enqueued);
    }

    [Fact]
    public async Task HandleAsync_WhenOverstayCycleAlreadyPaid_LateVnpayIpn_WithoutAccount_MarksNeedsManualRefund()
    {
        var sessionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var paidSeq2 = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, null, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(14),
            "GUEST_PAID_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        paidSeq2.MarkAsSucceeded("CASH_REF", _clock.UtcNow.AddMinutes(-1), _clock.UtcNow.AddMinutes(14));
        _paymentRepository.SeedPayment(paidSeq2);

        var lateGuestSeq2 = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, null, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow.AddMinutes(-2), _clock.UtcNow.AddMinutes(13),
            "GUEST_LATE_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        _paymentRepository.SeedPayment(lateGuestSeq2);

        var query = CreateValidQuery(lateGuestSeq2.TxnRef!, lateGuestSeq2.AmountPaid);
        var result = await _handler.HandleAsync(query, lateGuestSeq2.TxnRef!, CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(PaymentStatus.Surplus, lateGuestSeq2.Status);
        Assert.Equal("NeedsManualRefund", lateGuestSeq2.SurplusRefundStatus);
        Assert.Empty(_outboxWriter.Enqueued);
    }

    // Đua ghi khác dòng (23505): Cash/Wallet vừa thắng Seq=2 ngay khi IPN gọi SaveChangesAsync -> xóa tạm và hoàn tiền Surplus
    [Fact]
    public async Task HandleAsync_WhenCrossRow23505RaceOnSameCycle_ClearsChangeTracker_AndSettlesAsSurplus()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var wallet = Wallet.Create(accountId);
        _walletRepository.SeedWallet(wallet);

        var racingVnpay = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15),
            "RACING_VNPAY_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);
        _paymentRepository.SeedPayment(racingVnpay);

        var saveAttempts = 0;
        _paymentRepository.OnSaveChangesAsync = () =>
        {
            saveAttempts++;
            if (saveAttempts == 1)
            {
                // Mô phỏng một kênh khác vừa chốt Seq=2 thành công trước khi IPN kịp lưu DB
                var cashWinner = ParkingFeePayment.CreateCashPayment(
                    sessionId, lotId, accountId, "51F-12345", "Car",
                    15000, 0, 15000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15), paymentSequence: 2);
                _paymentRepository.SeedPayment(cashWinner);
                // Dùng reflection gán TxnRef giả để Fake dictionary lưu cashWinner (trùng key theo TxnRef)
                typeof(ParkingFeePayment).GetProperty(nameof(ParkingFeePayment.TxnRef))!.SetValue(cashWinner, "CASH_WINNER_SEQ2");
                _paymentRepository.SeedPayment(cashWinner);

                throw new DbUpdateException("23505 ux_fee_payments_session_sequence_succeeded");
            }

            return Task.CompletedTask;
        };

        // Mô phỏng EF Core ReloadAsync nạp lại Status = Pending từ DB sau rollback, kèm instance sạch khi ClearChangeTracker
        var reloadedFreshCopy = ParkingFeePayment.CreateVnPayPayment(
            sessionId, lotId, accountId, "51F-12345", "Car",
            15000, 0, 15000, _clock.UtcNow, _clock.UtcNow.AddMinutes(15),
            "RACING_VNPAY_SEQ2", "https://sandbox.vnpayment.vn", paymentSequence: 2);

        _paymentRepository.OnReload = p =>
        {
            typeof(ParkingFeePayment).GetProperty(nameof(ParkingFeePayment.Status))!.SetValue(p, PaymentStatus.Pending);
            _paymentRepository.SeedPayment(reloadedFreshCopy);
        };

        var query = CreateValidQuery("RACING_VNPAY_SEQ2", racingVnpay.AmountPaid);
        var result = await _handler.HandleAsync(query, "RACING_VNPAY_SEQ2", CancellationToken.None);

        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.True(_paymentRepository.ClearChangeTrackerCalled);
        Assert.Equal(PaymentStatus.Surplus, reloadedFreshCopy.Status);
        Assert.Equal("RefundedToWallet", reloadedFreshCopy.SurplusRefundStatus);
        Assert.Equal(15000m, wallet.Balance);
        Assert.Equal(2, saveAttempts);
    }
}
