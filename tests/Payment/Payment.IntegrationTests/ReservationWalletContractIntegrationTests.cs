using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

[Collection("PaymentConcurrencyNonParallelCollection")]
public sealed class ReservationWalletContractIntegrationTests(PostgreSqlContainerFixture postgres) : IAsyncLifetime
{
    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private ServiceProvider CreateServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddPersistence(configuration);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(Options.Create(new WalletOptions
        {
            MinTopUpVnd = 10000m,
            MaxTopUpVnd = 50000000m,
            MaxBalanceVnd = 100000000m
        }));

        services.AddScoped<DebitReservationDepositCommandHandler>();
        services.AddScoped<RefundReservationDepositCommandHandler>();

        return services.BuildServiceProvider();
    }

    public async ValueTask InitializeAsync()
    {
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await using var sp = CreateServiceProvider(configuration);
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Postgres_InsufficientBalance_ReleasesLock_AndAllowsRetryWithSameKey()
    {
        // Kiểm chứng lỗi P4 trực tiếp trên Unique Index thật của PostgreSQL
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await using var sp = CreateServiceProvider(configuration);

        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var idempotencyKey = $"res-key-{Guid.NewGuid():N}";

        // 1. Tạo ví ban đầu chỉ có 10.000đ (cọc cần 20.000đ)
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = Wallet.Create(userId);
            wallet.AddEntry(LedgerEntryType.TopUp, 10_000m, "Initial 10k");
            await dbContext.Wallets.AddAsync(wallet, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Gọi trừ cọc 20.000đ -> Thất bại do thiếu tiền
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DebitReservationDepositCommandHandler>();
            var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, idempotencyKey);

            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsFailure);
            Assert.Equal(ReservationDepositErrors.InsufficientBalance.Code, result.Error?.Code);
        }

        // 3. Khách hàng nạp thêm 50.000đ vào ví
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = await dbContext.Wallets.FirstAsync(w => w.UserId == userId, cancellationToken);
            wallet.AddEntry(LedgerEntryType.TopUp, 50_000m, "Topup 50k");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 4. Gọi lại trừ cọc với CÙNG Idempotency Key -> Thành công (không bị PostgreSQL Unique Index chặn)
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DebitReservationDepositCommandHandler>();
            var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, idempotencyKey);

            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
            Assert.Equal(40_000L, result.Value!.RemainingBalance);
        }
    }

    [Fact]
    public async Task Postgres_Debit_ThenRefund_ThenDebitAgain_ReturnsAlreadyRefunded()
    {
        // Kiểm chứng thứ tự M1: Trừ -> Hoàn -> Trừ lại = AlreadyRefunded
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await using var sp = CreateServiceProvider(configuration);

        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        // 1. Tạo ví có 50.000đ
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = Wallet.Create(userId);
            wallet.AddEntry(LedgerEntryType.TopUp, 50_000m, "Initial");
            await dbContext.Wallets.AddAsync(wallet, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Trừ cọc 20.000đ
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DebitReservationDepositCommandHandler>();
            var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, $"debit-{reservationId:N}");
            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
        }

        // 3. Hoàn cọc 20.000đ
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<RefundReservationDepositCommandHandler>();
            var command = new RefundReservationDepositCommand(userId, reservationId, 20_000m, $"refund-{reservationId:N}");
            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsSuccess);
        }

        // 4. Thử trừ cọc lại cho reservation đã hoàn tiền -> Chốt chặn M1 từ chối ngay lập tức
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<DebitReservationDepositCommandHandler>();
            var command = new DebitReservationDepositCommand(userId, reservationId, 20_000m, $"debit-{reservationId:N}");
            var result = await handler.Handle(command, cancellationToken);
            Assert.True(result.IsFailure);
            Assert.Equal(ReservationDepositErrors.AlreadyRefunded.Code, result.Error?.Code);
        }
    }

    [Fact]
    public async Task Postgres_Refund_ReplaySuccess_DoesNotDoubleCredit()
    {
        // Kiểm chứng thứ tự M2: Hoàn lần 1 thành công -> Hoàn lần 2 Replay thành công, ví chỉ cộng 1 lần
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await using var sp = CreateServiceProvider(configuration);

        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var refundKey = $"refund-replay-{Guid.NewGuid():N}";

        // 1. Tạo ví và trừ cọc 30.000đ
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = Wallet.Create(userId);
            wallet.AddEntry(LedgerEntryType.TopUp, 100_000m, "Initial");
            wallet.AddEntry(LedgerEntryType.ReservationDeposit, -30_000m, "Deposit", ReferenceType.Reservation, reservationId.ToString(), createdBy: userId);
            await dbContext.Wallets.AddAsync(wallet, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Hoàn cọc lần 1
        string firstTxnId;
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<RefundReservationDepositCommandHandler>();
            var command = new RefundReservationDepositCommand(userId, reservationId, 30_000m, refundKey);
            var result1 = await handler.Handle(command, cancellationToken);
            Assert.True(result1.IsSuccess);
            firstTxnId = result1.Value!.TransactionId!;
            Assert.Equal(100_000L, result1.Value.RemainingBalance);
        }

        // 3. Hoàn cọc lần 2 (Worker retry do rớt mạng)
        using (var scope = sp.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<RefundReservationDepositCommandHandler>();
            var command = new RefundReservationDepositCommand(userId, reservationId, 30_000m, refundKey);
            var result2 = await handler.Handle(command, cancellationToken);

            Assert.True(result2.IsSuccess);
            Assert.Equal(firstTxnId, result2.Value!.TransactionId);
            Assert.Equal(100_000L, result2.Value.RemainingBalance);
        }

        // 4. Kiểm tra số dư ví cuối cùng trong Database: Chỉ được cộng đúng 1 lần (100.000đ)
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var wallet = await dbContext.Wallets.FirstAsync(w => w.UserId == userId, cancellationToken);
            Assert.Equal(100_000m, wallet.Balance);
        }
    }
}
