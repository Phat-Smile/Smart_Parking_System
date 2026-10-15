using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles.AddVehicle;

public sealed record AddVehicleCommand(
    Guid AccountId,
    string LicensePlate,
    string VehicleType,
    string? NickName) : IRequest<Result<AddedVehicleResult>>;

public sealed record AddedVehicleResult(
    Guid VehicleId,
    Guid AccountId,
    string LicensePlate,
    string VehicleType,
    string? NickName,
    DateTimeOffset CreatedAtUtc);

public static class AddVehicleErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error PlateAlreadyRegistered =
        new("identity.vehicle_plate_already_registered", "Biển số xe này đã được đăng ký trong hệ thống.");

    public static readonly Error InvalidVehicleType =
        new("identity.invalid_vehicle_type", "Loại phương tiện không hợp lệ. Giá trị hợp lệ: Car, Motorbike.");
}

public sealed class AddVehicleCommandValidator : AbstractValidator<AddVehicleCommand>
{
    public AddVehicleCommandValidator()
    {
        RuleFor(c => c.LicensePlate)
            .NotEmpty().WithMessage("Biển số xe không được để trống.")
            .MaximumLength(Vehicle.MaxLicensePlateLength).WithMessage($"Biển số xe không được vượt quá {Vehicle.MaxLicensePlateLength} ký tự.");

        RuleFor(c => c.VehicleType)
            .NotEmpty().WithMessage("Loại phương tiện không được để trống.");

        RuleFor(c => c.NickName)
            .MaximumLength(Vehicle.MaxNickNameLength).When(c => c.NickName is not null)
            .WithMessage($"Tên gợi nhớ không được vượt quá {Vehicle.MaxNickNameLength} ký tự.");
    }
}

public sealed class AddVehicleCommandHandler(
    IAccountRepository accounts,
    IVehicleRepository vehicles,
    IClock clock) : IRequestHandler<AddVehicleCommand, Result<AddedVehicleResult>>
{
    public async Task<Result<AddedVehicleResult>> Handle(AddVehicleCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.AccountNotFound);

        if (!Enum.TryParse<VehicleDomainType>(request.VehicleType, ignoreCase: true, out var vehicleType))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidVehicleType);

        var normalizedPlate = request.LicensePlate.Trim().ToUpperInvariant();

        var plateExists = await vehicles.PlateExistsAsync(normalizedPlate, cancellationToken);
        if (plateExists)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.PlateAlreadyRegistered);

        var vehicle = Vehicle.Add(
            Guid.NewGuid(),
            request.AccountId,
            normalizedPlate,
            vehicleType,
            clock.UtcNow,
            request.NickName);

        vehicles.Add(vehicle);
        await vehicles.SaveChangesAsync(cancellationToken);

        return Result<AddedVehicleResult>.Success(new AddedVehicleResult(
            vehicle.Id,
            vehicle.AccountId,
            vehicle.LicensePlate,
            vehicle.VehicleType.ToString(),
            vehicle.NickName,
            vehicle.CreatedAtUtc));
    }
}
