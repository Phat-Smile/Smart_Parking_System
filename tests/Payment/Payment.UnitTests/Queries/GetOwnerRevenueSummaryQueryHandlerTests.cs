using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using Xunit;

namespace ParkingSystem.Payment.UnitTests.Queries;

public class GetOwnerRevenueSummaryQueryHandlerTests
{
    private sealed class FakePaymentRepository : IParkingFeePaymentRepository
    {
        private readonly List<ParkingFeePayment> _payments = [];

        public void Seed(ParkingFeePayment payment) => _payments.Add(payment);

        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(_payments.Any(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded));

        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
            => HasSucceededPaymentAsync(sessionId, ct);

        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.SessionId == sessionId && p.Status == PaymentStatus.Pending));

        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct)
        {
            _payments.Add(newPayment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.TxnRef == txnRef));

        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct)
            => Task.CompletedTask;

        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
        {
            _payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_payments.FirstOrDefault(p => p.Id == id));

        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
            => Task.FromResult(_payments
                .Where(p => (p.SessionId == sessionOrPaymentId || p.Id == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
                .OrderByDescending(p => p.PaymentTimeUtc)
                .FirstOrDefault());

        public Task<OwnerRevenueMetricsResult> GetRevenueMetricsAsync(
            IReadOnlyList<Guid> lotIds,
            DateTimeOffset todayStartUtc,
            DateTimeOffset weekStartUtc,
            DateTimeOffset monthStartUtc,
            CancellationToken ct)
        {
            var query = _payments.Where(p => p.Status == PaymentStatus.Succeeded);
            if (lotIds.Count == 1)
                query = query.Where(p => p.ParkingLotId == lotIds[0]);
            else if (lotIds.Count > 1)
                query = query.Where(p => lotIds.Contains(p.ParkingLotId));

            var list = query.ToList();

            RevenuePeriodMetrics Calc(Func<ParkingFeePayment, bool> predicate)
            {
                var subset = list.Where(predicate).ToList();
                var cash = subset.Where(p => p.PaymentMethod == PaymentMethod.Cash).Sum(p => p.AmountPaid);
                var vnpay = subset.Where(p => p.PaymentMethod == PaymentMethod.VnPay).Sum(p => p.AmountPaid);
                var wallet = subset.Where(p => p.PaymentMethod == PaymentMethod.Wallet).Sum(p => p.AmountPaid);
                var online = vnpay + wallet;
                var total = cash + online;

                var cashCount = subset.Count(p => p.PaymentMethod == PaymentMethod.Cash);
                var vnpayCount = subset.Count(p => p.PaymentMethod == PaymentMethod.VnPay);
                var walletCount = subset.Count(p => p.PaymentMethod == PaymentMethod.Wallet);
                var onlineCount = vnpayCount + walletCount;
                var totalCount = cashCount + onlineCount;

                return new RevenuePeriodMetrics(
                    total, cash, online, vnpay, wallet, 0L, totalCount, cashCount, onlineCount);
            }

            return Task.FromResult(new OwnerRevenueMetricsResult(
                Calc(p => p.PaymentTimeUtc >= todayStartUtc),
                Calc(p => p.PaymentTimeUtc >= weekStartUtc),
                Calc(p => p.PaymentTimeUtc >= monthStartUtc),
                Calc(_ => true)));
        }
    }

    private sealed class FakeMembershipGateway : IParkingLotMembershipGateway
    {
        public bool FailService { get; set; }
        public Dictionary<(Guid LotId, Guid AccountId), string> Memberships { get; } = [];

        public Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
            Guid lotId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            if (FailService)
                return Task.FromResult(Result<ParkingLotMembershipInfo>.Failure(ParkingFeeErrors.ParkingServiceUnavailable));

            if (Memberships.TryGetValue((lotId, accountId), out var role))
                return Task.FromResult(Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(true, role)));

            return Task.FromResult(Result<ParkingLotMembershipInfo>.Success(new ParkingLotMembershipInfo(false, string.Empty)));
        }
    }

    private static ParkingFeePayment CreatePayment(
        Guid lotId,
        string method,
        long amount,
        DateTimeOffset paymentTimeUtc,
        string status = PaymentStatus.Succeeded)
    {
        var sessionId = Guid.NewGuid();
        var payment = method switch
        {
            PaymentMethod.Cash => ParkingFeePayment.CreateCashPayment(
                sessionId, lotId, Guid.NewGuid(), "51A-12345", "Car4To7", amount, 0, amount, paymentTimeUtc, paymentTimeUtc.AddMinutes(15)),
            PaymentMethod.VnPay => ParkingFeePayment.CreateVnPayPayment(
                sessionId, lotId, Guid.NewGuid(), "51A-12345", "Car4To7", amount, 0, amount, paymentTimeUtc, paymentTimeUtc.AddMinutes(15), "TXN-" + Guid.NewGuid(), "https://vnpay.test"),
            _ => ParkingFeePayment.CreateWalletPayment(
                sessionId, lotId, Guid.NewGuid(), Guid.NewGuid(), "51A-12345", "Car4To7", amount, 0, amount, paymentTimeUtc, paymentTimeUtc.AddMinutes(15))
        };

        if (status == PaymentStatus.Succeeded && method == PaymentMethod.VnPay)
        {
            payment.MarkAsSucceeded("GW-" + Guid.NewGuid(), paymentTimeUtc, paymentTimeUtc.AddMinutes(15));
        }
        else if (status == PaymentStatus.Superseded)
        {
            payment.MarkAsSuperseded();
        }

        return payment;
    }

    [Fact]
    public async Task Handle_WhenUnauthenticated_ReturnsForbiddenRole()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var query = new GetOwnerRevenueSummaryQuery(Guid.NewGuid(), 420, null, null);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.ForbiddenRole.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Staff")]
    [InlineData("Guest")]
    public async Task Handle_WhenCallerHasUnauthorizedRole_ReturnsForbiddenRole(string unauthorizedRole)
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var query = new GetOwnerRevenueSummaryQuery(Guid.NewGuid(), 420, Guid.NewGuid(), unauthorizedRole);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.ForbiddenRole.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData(-721)]
    [InlineData(841)]
    [InlineData(999999)]
    public async Task Handle_WhenTimezoneOffsetOutOfRange_ReturnsInvalidTimeZoneOffset(int invalidOffset)
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var query = new GetOwnerRevenueSummaryQuery(Guid.NewGuid(), invalidOffset, Guid.NewGuid(), "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.InvalidTimeZoneOffset.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenOwnerOmitsLotId_ReturnsParkingLotIdRequired()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var query = new GetOwnerRevenueSummaryQuery(null, 420, Guid.NewGuid(), "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.ParkingLotIdRequired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenOwnerDoesNotOwnLot_ReturnsForbiddenLotAccess_AntiIdor()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var ownerAccountId = Guid.NewGuid();
        var victimLotId = Guid.NewGuid();

        // Caller is NOT owner of victimLotId
        var query = new GetOwnerRevenueSummaryQuery(victimLotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.ForbiddenLotAccess.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenParkingServiceUnavailable_ReturnsParkingServiceUnavailable()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway { FailService = true };
        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);

        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var query = new GetOwnerRevenueSummaryQuery(lotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerRevenueErrors.ParkingServiceUnavailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenLotHasZeroPaidSessions_ReturnsAllZeroMetrics()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        gateway.Memberships[(lotId, ownerAccountId)] = "Owner";

        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);
        var query = new GetOwnerRevenueSummaryQuery(lotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var res = result.Value!;
        Assert.Equal(lotId, res.ParkingLotId);
        Assert.Equal("VND", res.Currency);
        Assert.Equal(0, res.Today.TotalRevenue);
        Assert.Equal(0, res.Today.TransactionCount);
        Assert.Equal(0, res.ThisWeek.TotalRevenue);
        Assert.Equal(0, res.ThisMonth.TotalRevenue);
        Assert.Equal(0, res.AllTime.TotalRevenue);
    }

    [Fact]
    public async Task Handle_WhenValidOwner_AggregatesCashAndOnlineCorrectly()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        gateway.Memberships[(lotId, ownerAccountId)] = "Owner";

        var now = DateTimeOffset.UtcNow;
        // Cash payment: 30,000 VND today
        repo.Seed(CreatePayment(lotId, PaymentMethod.Cash, 30_000, now));
        // VNPay payment: 50,000 VND today
        repo.Seed(CreatePayment(lotId, PaymentMethod.VnPay, 50_000, now));
        // Wallet payment: 20,000 VND today
        repo.Seed(CreatePayment(lotId, PaymentMethod.Wallet, 20_000, now));

        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);
        var query = new GetOwnerRevenueSummaryQuery(lotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var res = result.Value!;
        Assert.Equal(100_000, res.Today.TotalRevenue);
        Assert.Equal(30_000, res.Today.CashRevenue);
        Assert.Equal(70_000, res.Today.OnlineRevenue);
        Assert.Equal(50_000, res.Today.VnpayRevenue);
        Assert.Equal(20_000, res.Today.WalletRevenue);
        Assert.Equal(3, res.Today.TransactionCount);
        Assert.Equal(1, res.Today.CashTransactionCount);
        Assert.Equal(2, res.Today.OnlineTransactionCount);
    }

    [Fact]
    public async Task Handle_WhenOverstayPaymentExists_IgnoresSupersededRecords_PreventsDoubleCounting()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        gateway.Memberships[(lotId, ownerAccountId)] = "Owner";

        var now = DateTimeOffset.UtcNow;
        // Succeeded base payment (which recorded overstay update: 40,000 total)
        repo.Seed(CreatePayment(lotId, PaymentMethod.VnPay, 40_000, now, PaymentStatus.Succeeded));
        // Superseded overstay pending payment (15,000) that must NOT be counted again!
        repo.Seed(CreatePayment(lotId, PaymentMethod.VnPay, 15_000, now, PaymentStatus.Superseded));

        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);
        var query = new GetOwnerRevenueSummaryQuery(lotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(40_000, result.Value!.Today.TotalRevenue);
        Assert.Equal(1, result.Value.Today.TransactionCount);
    }

    [Fact]
    public async Task Handle_WhenAdminQueriesWithoutLotId_AggregatesAcrossAllLots()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var adminAccountId = Guid.NewGuid();

        var lot1 = Guid.NewGuid();
        var lot2 = Guid.NewGuid();

        var now = DateTimeOffset.UtcNow;
        repo.Seed(CreatePayment(lot1, PaymentMethod.Cash, 50_000, now));
        repo.Seed(CreatePayment(lot2, PaymentMethod.VnPay, 70_000, now));

        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);
        // Admin queries with null lotId -> All lots
        var query = new GetOwnerRevenueSummaryQuery(null, 420, adminAccountId, "SystemAdmin");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ParkingLotId);
        Assert.Equal(120_000, result.Value.Today.TotalRevenue);
        Assert.Equal(50_000, result.Value.Today.CashRevenue);
        Assert.Equal(70_000, result.Value.Today.OnlineRevenue);
        Assert.Equal(2, result.Value.Today.TransactionCount);
    }

    [Fact]
    public async Task Handle_TimezoneBoundary_CorrectlyBucketsMidnightTransactionsAtUtcPlus7()
    {
        var repo = new FakePaymentRepository();
        var gateway = new FakeMembershipGateway();
        var ownerAccountId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        gateway.Memberships[(lotId, ownerAccountId)] = "Owner";

        // Current UTC time
        var nowUtc = DateTimeOffset.UtcNow;
        var offset = TimeSpan.FromMinutes(420); // UTC+7 Vietnam
        var localNow = nowUtc + offset;
        var localTodayStart = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, offset);

        // Transaction A: 01:00 AM local today (which is todayStartUtc + 1h)
        var paymentA = localTodayStart.AddHours(1).ToUniversalTime();
        repo.Seed(CreatePayment(lotId, PaymentMethod.Cash, 25_000, paymentA));

        // Transaction B: 23:30 PM local YESTERDAY (30 mins before todayStartLocal)
        var paymentB = localTodayStart.AddMinutes(-30).ToUniversalTime();
        repo.Seed(CreatePayment(lotId, PaymentMethod.Cash, 50_000, paymentB));

        var handler = new GetOwnerRevenueSummaryQueryHandler(repo, gateway);
        var query = new GetOwnerRevenueSummaryQuery(lotId, 420, ownerAccountId, "ParkingLotOwner");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Today must contain ONLY transaction A (25,000), NOT transaction B (which belongs to yesterday)
        Assert.Equal(25_000, result.Value!.Today.TotalRevenue);
        Assert.Equal(1, result.Value.Today.TransactionCount);

        // AllTime must contain BOTH (75,000)
        Assert.Equal(75_000, result.Value.AllTime.TotalRevenue);
        Assert.Equal(2, result.Value.AllTime.TransactionCount);
    }
}
