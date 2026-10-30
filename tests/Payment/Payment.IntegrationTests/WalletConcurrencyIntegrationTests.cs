using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

[CollectionDefinition("PaymentConcurrencyNonParallelCollection", DisableParallelization = true)]
public class PaymentConcurrencyNonParallelCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
}

[Collection("PaymentConcurrencyNonParallelCollection")]
public sealed class WalletConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres)
{
    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public string CreatePaymentUrl(string txnRef, decimal amount, DateTimeOffset expiresAt, string? clientIp)
            => $"https://sandbox.vnpayment.vn/payment-url/{txnRef}";

        public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters)
            => true;
    }

    private ServiceProvider CreateServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddPersistence(configuration);
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton(Options.Create(new WalletOptions
        {
            MinTopUpVnd = 10000m,
            MaxTopUpVnd = 50000000m,
            MaxBalanceVnd = 100000000m
        }));
        services.AddSingleton(Options.Create(new VnpayOptions
        {
            TmnCode = "TESTTMN",
            HashSecret = "TESTSECRET",
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "http://localhost:3000/payment/vnpay-return",
            Mode = "Sandbox",
            PaymentExpiryMinutes = 15
        }));

        services.AddScoped<IVnpayResultHandler, WalletTopUpResultHandler>();
        services.AddScoped<CreateWalletTopupCommandHandler>();
        services.AddScoped<ProcessVnpayIpnCommandHandler>();

        return services.BuildServiceProvider();
    }

    private async Task<PaymentDbContext> InitializeDatabaseAsync(CancellationToken cancellationToken)
    {
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await using var sp = CreateServiceProvider(configuration);
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        return dbContext;
    }

    [Fact]
    public async Task Concurrent_CreateWalletTopup_WithSameIdempotencyKey_RecoversGracefullyAndCreatesOnlyOneTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await InitializeDatabaseAsync(cancellationToken);

        var userId = Guid.NewGuid();
        var idempotencyKey = $"topup-key-{Guid.NewGuid():N}";
        const decimal amount = 100_000m;
        const int concurrentCount = 10;

        // Bắn đồng thời concurrentCount requests nạp ví với cùng IdempotencyKey
        var tasks = Enumerable.Range(0, concurrentCount).Select(async _ =>
        {
            await using var sp = CreateServiceProvider(configuration);
            using var scope = sp.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateWalletTopupCommandHandler>();

            return await handler.Handle(
                new CreateWalletTopupCommand(userId, amount, idempotencyKey, "127.0.0.1"),
                cancellationToken);
        });

        var results = await Task.WhenAll(tasks);

        // Tất cả request đều thành công (không có unhandled exception 500)
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));

        // Tất cả đều có cùng TopupId (mã TxnRef)
        var firstTopupId = results[0].Value!.TopupId;
        Assert.NotNull(firstTopupId);
        Assert.All(results, result => Assert.Equal(firstTopupId, result.Value!.TopupId));

        // Đúng 1 request là tạo mới, các request còn lại là Replay
        var replayCount = results.Count(r => r.Value!.IsReplay);
        var initialCount = results.Count(r => !r.Value!.IsReplay);
        Assert.Equal(1, initialCount);
        Assert.Equal(concurrentCount - 1, replayCount);

        // Kiểm tra Database thực tế: chỉ tồn tại đúng 1 bản ghi giao dịch duy nhất
        await using var verifySp = CreateServiceProvider(configuration);
        using var verifyScope = verifySp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var txnCount = await verifyDb.VnpayTransactions
            .CountAsync(t => t.ClientRequestKey == idempotencyKey, cancellationToken);
        Assert.Equal(1, txnCount);
    }

    [Fact]
    public async Task Concurrent_VnpayIpn_ForSameTopup_CreditsWalletOnlyOnce_AndAvoidsDoubleSpending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await InitializeDatabaseAsync(cancellationToken);

        var userId = Guid.NewGuid();
        var idempotencyKey = $"ipn-race-{Guid.NewGuid():N}";
        const decimal amount = 200_000m;

        // 1. Tạo 1 giao dịch nạp Pending trước
        string txnRef;
        Guid walletId;
        {
            await using var sp = CreateServiceProvider(configuration);
            using var scope = sp.CreateScope();
            var topupHandler = scope.ServiceProvider.GetRequiredService<CreateWalletTopupCommandHandler>();
            var createResult = await topupHandler.Handle(
                new CreateWalletTopupCommand(userId, amount, idempotencyKey, "127.0.0.1"),
                cancellationToken);
            Assert.True(createResult.IsSuccess);
            txnRef = createResult.Value!.TopupId;

            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = await db.Wallets.SingleAsync(w => w.UserId == userId, cancellationToken);
            walletId = wallet.Id;
            Assert.Equal(0m, wallet.Balance);
        }

        // 2. Giả lập VNPay gửi 10 Webhook IPN thông báo nạp thành công đồng thời
        const int concurrentIpnCount = 10;
        var rawAmount = ((long)amount * 100).ToString();
        var ipnParams = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = txnRef,
            ["vnp_Amount"] = rawAmount,
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TransactionNo"] = "14567890",
            ["vnp_BankCode"] = "NCB",
            ["vnp_SecureHash"] = "VALID_HASH"
        };

        var ipnTasks = Enumerable.Range(0, concurrentIpnCount).Select(async _ =>
        {
            await using var sp = CreateServiceProvider(configuration);
            using var scope = sp.CreateScope();
            var ipnHandler = scope.ServiceProvider.GetRequiredService<ProcessVnpayIpnCommandHandler>();

            return await ipnHandler.Handle(
                new ProcessVnpayIpnCommand(ipnParams),
                cancellationToken);
        });

        var ipnResults = await Task.WhenAll(ipnTasks);

        // Đúng 1 webhook trả về RspCode 00 (Success), các webhook còn lại trả về RspCode 02 (AlreadyConfirmed)
        var successCount = ipnResults.Count(r => r.RspCode == "00");
        var alreadyConfirmedCount = ipnResults.Count(r => r.RspCode == "02");

        Assert.Equal(1, successCount);
        Assert.Equal(concurrentIpnCount - 1, alreadyConfirmedCount);

        // 3. Kiểm chứng toàn vẹn Database: số dư ví CHỈ được cộng đúng 1 lần (200,000 VND)
        await using var verifySp = CreateServiceProvider(configuration);
        using var verifyScope = verifySp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var creditedWallet = await verifyDb.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId, cancellationToken);
        Assert.Equal(amount, creditedWallet.Balance);

        // Sổ cái (Ledger Entries) chỉ sinh đúng 1 dòng
        var ledgerEntries = await verifyDb.WalletLedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == walletId)
            .ToListAsync(cancellationToken);
        Assert.Single(ledgerEntries);
        Assert.Equal(amount, ledgerEntries[0].Amount);

        // Outbox Messages chỉ sinh đúng 1 sự kiện nạp ví thành công cho giao dịch này
        var outboxEvents = await verifyDb.OutboxMessages.AsNoTracking()
            .Where(o => o.EventType == "payment.wallet-topup-completed.v1")
            .ToListAsync(cancellationToken);
        var matchingEvents = outboxEvents.Where(o => o.Payload.Contains(txnRef)).ToList();
        Assert.Single(matchingEvents);
    }
}
