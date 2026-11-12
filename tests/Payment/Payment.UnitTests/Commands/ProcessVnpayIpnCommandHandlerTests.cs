using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Commands;

public class ProcessVnpayIpnCommandHandlerTests
{
    private class FakePaymentGateway : IPaymentGateway
    {
        public bool IsSignatureValid { get; set; } = true;

        public string CreatePaymentUrl(string txnRef, decimal amount, DateTimeOffset expiresAtUtc, string clientIp)
        {
            return "https://sandbox.vnpayment.vn";
        }

        public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters)
        {
            return IsSignatureValid;
        }
    }

    private class FakeVnpayTransactionRepository : IVnpayTransactionRepository
    {
        private readonly Dictionary<string, VnpayTransaction> _txns = new();
        public bool SaveChangesCalled { get; private set; }

        public void SeedTransaction(VnpayTransaction tx)
        {
            _txns[tx.TxnRef] = tx;
        }

        public Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken)
        {
            _txns.TryGetValue(txnRef, out var tx);
            return Task.FromResult(tx);
        }

        public Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(Guid walletId, string clientRequestKey, CancellationToken cancellationToken)
        {
            return Task.FromResult<VnpayTransaction?>(null);
        }

        public Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
        {
            _txns[transaction.TxnRef] = transaction;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }

        public Task<VnpayTransaction?> GetTopupByTxnRefAndUserIdAsync(string txnRef, Guid userId, CancellationToken cancellationToken)
        {
            _txns.TryGetValue(txnRef, out var tx);
            return Task.FromResult(tx);
        }

        public Task<IReadOnlyList<VnpayTransaction>> GetRecentPendingByWalletIdAsync(Guid walletId, int limit, CancellationToken cancellationToken)
        {
            var items = _txns.Values
                .Where(t => t.WalletId == walletId && t.Status == VnpayTransactionStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<VnpayTransaction>>(items);
        }

        public Task<IReadOnlyList<VnpayTransaction>> GetRecentExpiredTopupsByWalletIdAsync(Guid walletId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VnpayTransaction>>(Array.Empty<VnpayTransaction>());
    }

    private class FakeWalletRepository : IWalletRepository
    {
        private readonly Dictionary<Guid, Wallet> _wallets = new();

        public void SeedWallet(Wallet wallet)
        {
            _wallets[wallet.Id] = wallet;
        }

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            _wallets.TryGetValue(id, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Wallet?>(null);
        }

        public Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Wallet.Create(userId));
        }

        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
        {
            _wallets[wallet.Id] = wallet;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class FakeOutboxWriter : IOutboxWriter
    {
        public List<(object Event, string EventType, int EventVersion)> Enqueued { get; } = new();

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null) where TEvent : class
        {
            Enqueued.Add((integrationEvent, eventType, eventVersion));
        }
    }

    private class FakeParkingFeeIpnHandler : IParkingFeeIpnHandler
    {
        public bool HandleCalled { get; private set; }
        public VnpayIpnResponseDto ResponseToReturn { get; set; } = VnpayIpnResponseDto.Success();

        public Task<VnpayIpnResponseDto> HandleAsync(
            IReadOnlyDictionary<string, string> query,
            string txnRef,
            CancellationToken cancellationToken)
        {
            HandleCalled = true;
            return Task.FromResult(ResponseToReturn);
        }
    }

    private readonly FakePaymentGateway _paymentGateway = new();
    private readonly FakeVnpayTransactionRepository _txnRepository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeOutboxWriter _outboxWriter = new();
    private readonly FakeParkingFeeIpnHandler _parkingFeeHandler = new();
    private readonly ProcessVnpayIpnCommandHandler _handler;

    public ProcessVnpayIpnCommandHandlerTests()
    {
        var topupHandler = new WalletTopUpResultHandler(
            _walletRepository,
            _txnRepository,
            _outboxWriter,
            NullLogger<WalletTopUpResultHandler>.Instance);

        var directPaymentHandler = new DirectPaymentResultHandler(
            _txnRepository,
            _outboxWriter,
            NullLogger<DirectPaymentResultHandler>.Instance);

        _handler = new ProcessVnpayIpnCommandHandler(
            _paymentGateway,
            _txnRepository,
            new IVnpayResultHandler[] { topupHandler, directPaymentHandler },
            NullLogger<ProcessVnpayIpnCommandHandler>.Instance,
            _parkingFeeHandler);
    }

    [Fact]
    public async Task TC_IPN_01_ValidSuccessIpn_CreditsWalletAndReturns00()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        _walletRepository.SeedWallet(wallet);

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            "01234567890123456789012345678901",
            wallet.Id,
            50_000m,
            "req_key_123456",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "5000000", // 50,000 * 100
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TransactionNo"] = "1456789",
            ["vnp_BankCode"] = "NCB"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(VnpayIpnResponseDto.Messages.Success, result.Message);
        Assert.Equal(VnpayTransactionStatus.Success, txn.Status);
        Assert.Equal(50_000m, wallet.Balance);
        Assert.Single(wallet.LedgerEntries);
        Assert.True(_txnRepository.SaveChangesCalled);
    }

    [Fact]
    public async Task TC_IPN_02_DuplicateIpn_Returns02AndDoesNotCreditAgain()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        _walletRepository.SeedWallet(wallet);

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            "01234567890123456789012345678901",
            wallet.Id,
            50_000m,
            "req_key_123456",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        txn.MarkAsSuccess("1456789", "NCB", "00", DateTimeOffset.UtcNow);
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.AlreadyConfirmed, result.RspCode);
        Assert.Equal(VnpayIpnResponseDto.Messages.AlreadyConfirmed, result.Message);
        Assert.Equal(0m, wallet.Balance); // Số dư không bị cộng lần 2
    }

    [Fact]
    public async Task TC_IPN_03_InvalidSignature_Returns97()
    {
        // Arrange
        _paymentGateway.IsSignatureValid = false;
        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "ref123"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.InvalidSignature, result.RspCode);
    }

    [Fact]
    public async Task TC_IPN_04_AmountMismatch_Returns04()
    {
        // Arrange
        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            "01234567890123456789012345678901",
            Guid.NewGuid(),
            50_000m,
            "req_key_123456",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "10000000" // 100,000 VND (lệch với 50,000 VND)
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.InvalidAmount, result.RspCode);
        Assert.Equal(VnpayTransactionStatus.Pending, txn.Status);
    }

    [Fact]
    public async Task TC_IPN_05_UnknownTransaction_Returns01()
    {
        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "non_existent_ref"
        };

        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);
        Assert.Equal(VnpayIpnResponseDto.Codes.OrderNotFound, result.RspCode);
    }

    [Fact]
    public async Task TC_IPN_06_PaymentFailedAtGateway_MarksFailedAndDoesNotCredit()
    {
        // Arrange
        var wallet = Wallet.Create(Guid.NewGuid());
        _walletRepository.SeedWallet(wallet);

        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            "01234567890123456789012345678901",
            wallet.Id,
            50_000m,
            "req_key_123456",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "24", // Khách hủy giao dịch
            ["vnp_TransactionStatus"] = "02"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(VnpayTransactionStatus.Failed, txn.Status);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(wallet.LedgerEntries);
    }

    [Fact]
    public async Task TC_IPN_07_LateIpnAfterExpiry_StillCreditsWalletNormally()
    {
        // Arrange (BR-12.d: IPN báo thành công sau khi hết hạn vẫn được cộng tiền)
        var wallet = Wallet.Create(Guid.NewGuid());
        _walletRepository.SeedWallet(wallet);

        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        var txn = VnpayTransaction.CreateTopup(
            Guid.NewGuid(),
            "01234567890123456789012345678901",
            wallet.Id,
            20_000m,
            "req_key_123456",
            expiredTime.AddMinutes(-15),
            expiredTime);
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "2000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(VnpayTransactionStatus.Success, txn.Status);
        Assert.Equal(20_000m, wallet.Balance);
    }

    [Fact]
    public async Task Handle_WhenTxnRefStartsWithPark_DelegatesToParkingFeeHandler()
    {
        // Arrange
        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "PARK_session_123",
            ["vnp_SecureHash"] = "valid_hash"
        };
        _paymentGateway.IsSignatureValid = true;

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal("00", result.RspCode);
        Assert.True(_parkingFeeHandler.HandleCalled);
    }

    [Fact]
    public async Task TC_IPN_08_DirectPayment_Success_EnqueuesOutboxEventAndMarksSuccess()
    {
        // Arrange (S3-L5: Direct Payment at exit barrier via VNPay)
        var sessionId = Guid.NewGuid();
        var txn = VnpayTransaction.CreateDirectPayment(
            Guid.NewGuid(),
            "01234567890123456789012345678902",
            sessionId,
            35_000m,
            "req_key_exit_pay",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "3500000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TransactionNo"] = "987654321",
            ["vnp_BankCode"] = "NCB"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(VnpayTransactionStatus.Success, txn.Status);
        Assert.True(_txnRepository.SaveChangesCalled);

        // Outbox event must be enqueued with event type payment.direct-payment-completed.v1
        Assert.Single(_outboxWriter.Enqueued);
        var enqueued = _outboxWriter.Enqueued[0];
        Assert.Equal("payment.direct-payment-completed.v1", enqueued.EventType);
        Assert.Equal(1, enqueued.EventVersion);

        var directEvent = Assert.IsType<ParkingSystem.Contracts.Events.Payment.DirectPaymentCompletedIntegrationEvent>(enqueued.Event);
        Assert.Equal(txn.Id, directEvent.TransactionId);
        Assert.Equal(txn.TxnRef, directEvent.TxnRef);
        Assert.Equal(sessionId, directEvent.WalletId);
        Assert.Equal(35_000m, directEvent.Amount);
        Assert.Equal("987654321", directEvent.VnpayTxnNo);
    }

    [Fact]
    public async Task TC_IPN_09_DirectPayment_Failure_MarksFailedAndDoesNotEnqueueEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var txn = VnpayTransaction.CreateDirectPayment(
            Guid.NewGuid(),
            "01234567890123456789012345678903",
            sessionId,
            40_000m,
            "req_key_exit_fail",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "4000000",
            ["vnp_ResponseCode"] = "24", // Canceled by customer
            ["vnp_TransactionStatus"] = "02"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.Success, result.RspCode);
        Assert.Equal(VnpayTransactionStatus.Failed, txn.Status);
        Assert.Empty(_outboxWriter.Enqueued);
        Assert.True(_txnRepository.SaveChangesCalled);
    }

    [Fact]
    public async Task TC_IPN_10_DirectPayment_DuplicateIpn_Returns02AndDoesNotEnqueueDuplicate()
    {
        // Arrange (Idempotency check: Already confirmed)
        var sessionId = Guid.NewGuid();
        var txn = VnpayTransaction.CreateDirectPayment(
            Guid.NewGuid(),
            "01234567890123456789012345678904",
            sessionId,
            30_000m,
            "req_key_exit_dup",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15));
        txn.MarkAsSuccess("987654322", "NCB", "00", DateTimeOffset.UtcNow);
        _txnRepository.SeedTransaction(txn);

        var query = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txn.TxnRef,
            ["vnp_Amount"] = "3000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        };

        // Act
        var result = await _handler.Handle(new ProcessVnpayIpnCommand(query), CancellationToken.None);

        // Assert
        Assert.Equal(VnpayIpnResponseDto.Codes.AlreadyConfirmed, result.RspCode);
        Assert.Equal(VnpayIpnResponseDto.Messages.AlreadyConfirmed, result.Message);
        Assert.Empty(_outboxWriter.Enqueued);
    }
}
