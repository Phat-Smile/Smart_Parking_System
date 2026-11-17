using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

// Xử lý truy vấn báo cáo doanh thu bãi xe cho Chủ bãi
public sealed class GetOwnerRevenueSummaryQueryHandler(
    IParkingFeePaymentRepository paymentRepository,
    IParkingLotMembershipGateway membershipGateway)
    : IRequestHandler<GetOwnerRevenueSummaryQuery, Result<OwnerRevenueSummaryResponse>>
{
    private const string AdminRole = "SystemAdmin";
    private static readonly string[] OwnerRoles = ["ParkingLotOwner", "LotOwner"];

    public async Task<Result<OwnerRevenueSummaryResponse>> Handle(
        GetOwnerRevenueSummaryQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tài khoản người gọi
        if (request.CallerAccountId is not { } accountId || accountId == Guid.Empty)
        {
            return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.ForbiddenRole);
        }

        // 2. Kiểm tra vai trò quyền hạn
        var isAdmin = string.Equals(request.CallerRole, AdminRole, StringComparison.OrdinalIgnoreCase);
        var isOwner = OwnerRoles.Any(r => string.Equals(request.CallerRole, r, StringComparison.OrdinalIgnoreCase));
        if (!isAdmin && !isOwner)
        {
            return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.ForbiddenRole);
        }

        // 3. Kiểm tra tính hợp lệ của múi giờ (-12h đến +14h)
        if (request.TimeZoneOffsetMinutes < -720 || request.TimeZoneOffsetMinutes > 840)
        {
            return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.InvalidTimeZoneOffset);
        }

        // 4. Kiểm tra chống xem trộm dữ liệu (NFR-07 Anti-IDOR)
        IReadOnlyList<Guid> targetLotIds;
        if (!isAdmin)
        {
            // Chủ bãi bắt buộc phải chọn bãi xe cụ thể
            if (!request.ParkingLotId.HasValue || request.ParkingLotId.Value == Guid.Empty)
            {
                return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.ParkingLotIdRequired);
            }

            var lotId = request.ParkingLotId.Value;
            var membership = await membershipGateway.GetMembershipAsync(lotId, accountId, cancellationToken);
            if (!membership.IsSuccess || membership.Value is null)
            {
                return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.ParkingServiceUnavailable);
            }

            if (!membership.Value.IsMember || !string.Equals(membership.Value.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return Result<OwnerRevenueSummaryResponse>.Failure(OwnerRevenueErrors.ForbiddenLotAccess);
            }

            targetLotIds = [lotId];
        }
        else
        {
            // Quản trị viên hệ thống có thể xem toàn bộ hoặc theo bãi
            targetLotIds = request.ParkingLotId.HasValue && request.ParkingLotId.Value != Guid.Empty
                ? [request.ParkingLotId.Value]
                : [];
        }

        // 5. Tính toán các mốc thời gian UTC theo múi giờ địa phương
        var offset = TimeSpan.FromMinutes(request.TimeZoneOffsetMinutes);
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = nowUtc + offset;

        var todayStartLocal = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, offset);
        var todayStartUtc = todayStartLocal.ToUniversalTime();

        var daysToSubtract = (7 + (int)nowLocal.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var weekStartLocal = todayStartLocal.AddDays(-daysToSubtract);
        var weekStartUtc = weekStartLocal.ToUniversalTime();

        var monthStartLocal = new DateTimeOffset(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, offset);
        var monthStartUtc = monthStartLocal.ToUniversalTime();

        // 6. Truy vấn tổng hợp doanh thu 1 round-trip từ cơ sở dữ liệu
        var metricsResult = await paymentRepository.GetRevenueMetricsAsync(
            targetLotIds,
            todayStartUtc,
            weekStartUtc,
            monthStartUtc,
            cancellationToken);

        // 7. Đóng gói kết quả gửi về cho client
        var response = new OwnerRevenueSummaryResponse(
            ParkingLotId: request.ParkingLotId,
            GeneratedAtUtc: nowUtc.UtcDateTime,
            Currency: "VND",
            Today: metricsResult.Today,
            ThisWeek: metricsResult.ThisWeek,
            ThisMonth: metricsResult.ThisMonth,
            AllTime: metricsResult.AllTime);

        return Result<OwnerRevenueSummaryResponse>.Success(response);
    }
}
