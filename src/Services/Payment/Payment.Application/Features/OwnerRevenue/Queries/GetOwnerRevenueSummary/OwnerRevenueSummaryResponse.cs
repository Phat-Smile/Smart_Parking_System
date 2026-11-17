namespace ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

// Chi tiết doanh thu theo từng mốc thời gian
public sealed record RevenuePeriodMetrics(
    long TotalRevenue,
    long CashRevenue,
    long OnlineRevenue,
    long VnpayRevenue,
    long WalletRevenue,
    long ForfeitedDepositRevenue,
    int TransactionCount,
    int CashTransactionCount,
    int OnlineTransactionCount);

// Báo cáo tổng hợp doanh thu gửi về cho Chủ bãi
public sealed record OwnerRevenueSummaryResponse(
    Guid? ParkingLotId,
    DateTime GeneratedAtUtc,
    string Currency,
    RevenuePeriodMetrics Today,
    RevenuePeriodMetrics ThisWeek,
    RevenuePeriodMetrics ThisMonth,
    RevenuePeriodMetrics AllTime);
