using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingCommandHandler : IRequestHandler<ConfigurePricingCommand, Result<ConfigurePricingResult>>
{
    private readonly IPricingPolicyRepository _pricingRepository;
    private readonly IAuditWriter _auditWriter;
    private readonly ILotOwnershipService _lotOwnershipService;

    public ConfigurePricingCommandHandler(
        IPricingPolicyRepository pricingRepository,
        IAuditWriter auditWriter,
        ILotOwnershipService lotOwnershipService)
    {
        _pricingRepository = pricingRepository;
        _auditWriter = auditWriter;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<ConfigurePricingResult>> Handle(ConfigurePricingCommand request, CancellationToken cancellationToken)
    {
        // Chuan hoa loai xe ve chu in hoa
        string normalizedVehicleType = request.VehicleType.Trim().ToUpperInvariant();

        // Kiem tra bai xe co ton tai khong
        bool lotExists = await _lotOwnershipService.LotExistsAsync(request.LotId);
        if (!lotExists)
        {
            return Result<ConfigurePricingResult>.Failure(ParkingErrors.Pricing.LotNotFound);
        }

        // Kiem tra quyen chu so huu bai xe
        bool isOwner = await _lotOwnershipService.IsLotOwnerAsync(request.LotId, request.CallerId);
        if (!isOwner)
        {
            return Result<ConfigurePricingResult>.Failure(ParkingErrors.Pricing.NotLotOwner);
        }

        // Lay phien ban bang gia moi nhat hien tai
        var currentPolicy = await _pricingRepository.GetLatestAsync(request.LotId, normalizedVehicleType, cancellationToken);

        int currentVersion = currentPolicy?.Version ?? 0;
        int expectedVersion = request.ExpectedVersion ?? 0;

        // Kiem tra xung dot phien ban
        if (expectedVersion != currentVersion)
        {
            return Result<ConfigurePricingResult>.Failure(ParkingErrors.Pricing.VersionConflict);
        }

        // Neu gia tri khong doi thi giu nguyen phien ban
        if (currentPolicy != null && currentPolicy.HasSameValues(
            request.BillingMode,
            request.BlockMinutes,
            request.Price,
            request.OvernightFee))
        {
            return Result<ConfigurePricingResult>.Success(new ConfigurePricingResult(currentVersion, false));
        }

        // Tao phien ban bang gia moi tang 1
        int newVersion = currentVersion + 1;
        var newPolicy = PricingPolicy.Create(
            request.LotId,
            normalizedVehicleType,
            newVersion,
            request.BillingMode,
            request.BlockMinutes,
            request.Price,
            request.OvernightFee);

        await _pricingRepository.AddAsync(newPolicy, cancellationToken);

        // Ghi nhat ky kiem toan he thong
        _auditWriter.Log(
            actorId: request.CallerId,
            action: "ConfigurePricing",
            targetId: request.LotId.ToString(),
            details: $"Updated {normalizedVehicleType} pricing from V{currentVersion} to V{newVersion}. Reason: {request.Reason}");

        // Luu du lieu xuong database va bat xung dot
        bool saveSuccess = await _pricingRepository.SaveChangesAsync(cancellationToken);
        if (!saveSuccess)
        {
            return Result<ConfigurePricingResult>.Failure(ParkingErrors.Pricing.VersionConflict);
        }

        return Result<ConfigurePricingResult>.Success(new ConfigurePricingResult(newVersion, true));
    }
}
