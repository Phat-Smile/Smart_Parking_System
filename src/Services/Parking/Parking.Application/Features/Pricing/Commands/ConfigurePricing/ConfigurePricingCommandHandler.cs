using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingCommandHandler : IRequestHandler<ConfigurePricingCommand, Result<ConfigurePricingResult>>
{
    private readonly IParkingDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;

    public ConfigurePricingCommandHandler(IParkingDbContext dbContext, IAuditWriter auditWriter)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
    }

    public async Task<Result<ConfigurePricingResult>> Handle(ConfigurePricingCommand request, CancellationToken cancellationToken)
    {
        // Bước 1: Lấy bảng giá hiện tại của bãi đỗ xe và loại xe này từ cơ sở dữ liệu.
        var currentPolicy = await _dbContext.PricingPolicies
            .Where(p => p.LotId == request.LotId && p.VehicleType == request.VehicleType)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        int currentVersion = currentPolicy?.Version ?? 0;

        // Kiểm tra có người khác đang sửa giá cùng lúc với mình không
        // Nếu có người khác đã sửa và lưu trước mình, ta phải chặn lại để tránh ghi đè làm mất dữ liệu của họ
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != currentVersion)
        {
            return Result<ConfigurePricingResult>.Failure(ParkingErrors.Pricing.VersionConflict);
        }

        // Kiểm tra xem giá mới gửi lên có trùng giá cũ không
        // Nếu giống thì bỏ qua, không cần lưu thêm dòng mới dưới DB
        if (currentPolicy != null &&
            currentPolicy.BillingMode == request.BillingMode &&
            currentPolicy.BlockMinutes == request.BlockMinutes &&
            currentPolicy.Price == request.Price &&
            currentPolicy.OvernightFee == request.OvernightFee)
        {
            return Result<ConfigurePricingResult>.Success(new ConfigurePricingResult(currentVersion, true));
        }

        // Tạo dữ liệu bảng giá mới
        int newVersion = currentVersion + 1;
        var newPolicy = PricingPolicy.Create(
            request.LotId,
            request.VehicleType,
            newVersion,
            request.BillingMode,
            request.BlockMinutes,
            request.Price,
            request.OvernightFee
        );

        // Chuẩn bị lưu bảng giá mới vào hệ thống
        _dbContext.PricingPolicies.Add(newPolicy);

        // Ghi lại lịch sử hệ thống (ai, lúc nào, lý do là gì khi sửa)
        _auditWriter.Log(
            actorId: request.CallerId,
            action: "ConfigurePricing",
            targetId: request.LotId.ToString(),
            details: $"Updated {request.VehicleType} pricing from V{currentVersion} to V{newVersion}. Reason: {request.Reason}"
        );

        // Xác nhận lưu toàn bộ thay đổi xuống Database.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<ConfigurePricingResult>.Success(new ConfigurePricingResult(newVersion, false));
    }
}
