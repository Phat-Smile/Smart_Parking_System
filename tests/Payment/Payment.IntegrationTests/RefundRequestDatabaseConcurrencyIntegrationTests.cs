using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

[Collection("PaymentConcurrencyNonParallelCollection")]
public sealed class RefundRequestDatabaseConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres)
{
    private sealed class FakeMembershipGateway : IParkingLotMembershipGateway
    {
        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId,
            Guid accountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, "Owner")));
    }

    private sealed class TestCurrentActor : ICurrentActor
    {
        public Guid? ActorId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "admin@smartparking.vn";
        public string Role { get; set; } = "SystemAdmin";
        public string? IpAddress { get; set; } = "127.0.0.1";
    }

    private ServiceProvider CreateServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddPersistence(configuration);
        services.AddSingleton<IParkingLotMembershipGateway, FakeMembershipGateway>();
        services.AddSingleton<ICurrentActor, TestCurrentActor>();
        services.AddScoped<CreateRefundRequestCommandHandler>();
        services.AddScoped<ConfirmRefundRequestByOwnerCommandHandler>();
        services.AddScoped<RejectRefundRequestCommandHandler>();

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
    public async Task R14_Rejection_does_not_modify_wallet_balance_or_add_ledger_entries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await InitializeDatabaseAsync(cancellationToken);

        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        Guid walletId;
        Guid requestId;

        // 1. Seed Customer Wallet with 200,000 VND balance and 1 initial ledger entry
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var wallet = Wallet.Create(customerId);
            wallet.AddEntry(LedgerEntryType.TopUp, 200_000m, "Initial topup", ReferenceType.VnpayTransaction, "TXN-INIT");
            await db.Wallets.AddAsync(wallet, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            walletId = wallet.Id;

            // Seed RefundRequest
            var refundRequest = RefundRequest.Create(
                walletId, lotId, 50_000m, "Lỗi trừ tiền đôi", customerId,
                ReferenceType.ParkingSession, "SES-100", DateTimeOffset.UtcNow);
            refundRequest.ConfirmByOwner(ownerId, DateTimeOffset.UtcNow);
            await db.RefundRequests.AddAsync(refundRequest, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            requestId = refundRequest.Id;
        }

        // Snapshot before reject
        decimal balanceBefore;
        int ledgerCountBefore;
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var wallet = await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId, cancellationToken);
            balanceBefore = wallet.Balance;
            ledgerCountBefore = await db.WalletLedgerEntries.CountAsync(e => e.WalletId == walletId, cancellationToken);
        }

        Assert.Equal(200_000m, balanceBefore);
        Assert.Equal(1, ledgerCountBefore);

        // 2. Admin rejects the refund request
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RejectRefundRequestCommandHandler>();

            var result = await handler.Handle(
                new RejectRefundRequestCommand(requestId, adminId, "SystemAdmin", "Không phải lỗi hệ thống"),
                cancellationToken);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal("Rejected", result.Value!.Status);
        }

        // 3. Snapshot after reject: verify wallet balance, ledger count, and audit row
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var wallet = await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId, cancellationToken);
            var ledgerCountAfter = await db.WalletLedgerEntries.CountAsync(e => e.WalletId == walletId, cancellationToken);
            var updatedRequest = await db.RefundRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, cancellationToken);

            // R-14 assertion: wallet balance and ledger entries count MUST NOT change!
            Assert.Equal(balanceBefore, wallet.Balance);
            Assert.Equal(ledgerCountBefore, ledgerCountAfter);

            // Request state assertion
            Assert.Equal(RefundStatus.Rejected, updatedRequest.Status);
            Assert.Equal(adminId, updatedRequest.ReviewedBy);
            Assert.NotNull(updatedRequest.ReviewedAt);
            Assert.Null(updatedRequest.LedgerEntryId);

            // Audit log check: audit row created with reason
            var auditLog = await db.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TargetId == requestId.ToString() && a.ActionType == AuditActionCatalog.RefundRequestRejected, cancellationToken);

            Assert.NotNull(auditLog);
            Assert.Equal("Không phải lỗi hệ thống", auditLog.Reason);
            Assert.Equal(lotId, auditLog.ParkingLotId);
        }
    }

    [Fact]
    public async Task R15_Concurrent_rejections_on_same_request_only_one_succeeds_other_gets_concurrency_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = PostgreSqlTestConfiguration.Create(postgres.ConnectionString);
        await InitializeDatabaseAsync(cancellationToken);

        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        Guid requestId;

        // Seed confirmed refund request
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var wallet = Wallet.Create(customerId);
            await db.Wallets.AddAsync(wallet, cancellationToken);

            var refundRequest = RefundRequest.Create(
                wallet.Id, lotId, 30_000m, "Sự cố", customerId,
                ReferenceType.Reservation, "RES-111", DateTimeOffset.UtcNow);
            refundRequest.ConfirmByOwner(ownerId, DateTimeOffset.UtcNow);

            await db.RefundRequests.AddAsync(refundRequest, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            requestId = refundRequest.Id;
        }

        // Fire 2 concurrent rejections by two different admins (or same admin)
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();

        var task1 = Task.Run(async () =>
        {
            await using var sp = CreateServiceProvider(configuration);
            using var scope = sp.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RejectRefundRequestCommandHandler>();
            return await handler.Handle(
                new RejectRefundRequestCommand(requestId, admin1, "SystemAdmin", "Admin 1 reject"),
                cancellationToken);
        });

        var task2 = Task.Run(async () =>
        {
            await using var sp = CreateServiceProvider(configuration);
            using var scope = sp.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RejectRefundRequestCommandHandler>();
            return await handler.Handle(
                new RejectRefundRequestCommand(requestId, admin2, "SystemAdmin", "Admin 2 reject"),
                cancellationToken);
        });

        var results = await Task.WhenAll(task1, task2);

        // Exactly one should succeed with 200/Success, and the loser should receive ConcurrencyConflict (409) or AlreadyReviewed
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failedResult = results.Single(r => !r.IsSuccess);
        Assert.Contains(failedResult.Error!.Code, new[]
        {
            RefundRequestErrors.ConcurrencyConflict.Code,
            RefundRequestErrors.AlreadyReviewed.Code,
            RefundRequestErrors.InvalidStatus.Code
        });

        // DB state check: exactly one rejection recorded
        await using (var sp = CreateServiceProvider(configuration))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var req = await db.RefundRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, cancellationToken);
            Assert.Equal(RefundStatus.Rejected, req.Status);
            Assert.True(req.ReviewedBy == admin1 || req.ReviewedBy == admin2);
        }
    }
}
