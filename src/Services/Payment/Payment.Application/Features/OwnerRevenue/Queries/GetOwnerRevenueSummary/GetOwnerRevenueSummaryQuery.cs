using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

// Truy vấn báo cáo tổng hợp doanh thu của Chủ bãi
public sealed record GetOwnerRevenueSummaryQuery(
    Guid? ParkingLotId,
    int TimeZoneOffsetMinutes,
    Guid? CallerAccountId,
    string? CallerRole) : IRequest<Result<OwnerRevenueSummaryResponse>>;
