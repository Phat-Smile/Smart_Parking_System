using ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Kết quả tính toán doanh thu từ cơ sở dữ liệu
public sealed record OwnerRevenueMetricsResult(
    RevenuePeriodMetrics Today,
    RevenuePeriodMetrics ThisWeek,
    RevenuePeriodMetrics ThisMonth,
    RevenuePeriodMetrics AllTime);
