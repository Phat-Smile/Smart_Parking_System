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
    string? LicensePlate,
    string VehicleType,
    string? NickName,
    VehicleDetails? Details = null) : IRequest<Result<AddedVehicleResult>>;

public sealed record AddedVehicleResult(
    Guid VehicleId,
    Guid AccountId,
    string? LicensePlate,
    string VehicleType,
    string? NickName,
    DateTimeOffset CreatedAtUtc,
    bool IsDefault,
    VehicleDetails? Details = null);

public static class AddVehicleErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error PlateAlreadyRegistered =
        new("identity.vehicle_plate_already_registered", "Biển số xe này đã được đăng ký trong hệ thống.");

    public static readonly Error InvalidLicensePlate =
        new("identity.invalid_license_plate", "Biển số không khớp định dạng ô tô hoặc xe máy thông dụng tại Việt Nam.");

    public static readonly Error InvalidVehicleType =
        new("identity.invalid_vehicle_type", "Loại phương tiện không hợp lệ. Giá trị hợp lệ: Car, Motorbike.");
}

public sealed class AddVehicleCommandValidator : AbstractValidator<AddVehicleCommand>
{
    public AddVehicleCommandValidator()
    {
        VehicleDetailsValidation.AddRules(this, c => c.Details);
        RuleFor(c => c.LicensePlate).NotEmpty()
            .When(c => string.Equals(c.VehicleType, "Car", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.VehicleType, "Motorbike", StringComparison.OrdinalIgnoreCase));
        RuleFor(c => c.LicensePlate).MaximumLength(Vehicle.MaxLicensePlateLength)
            .When(c => c.LicensePlate is not null);

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
    IClock clock,
    ILicensePlateNormalizer plateNormalizer) : IRequestHandler<AddVehicleCommand, Result<AddedVehicleResult>>
{

    public async Task<Result<AddedVehicleResult>> Handle(AddVehicleCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.AccountNotFound);

        // Only a named value is accepted: Enum.TryParse would also accept "1", "99" or " Car".
        var typeName = new[] { nameof(VehicleDomainType.Car), nameof(VehicleDomainType.Motorbike) }
            .FirstOrDefault(name => string.Equals(name, request.VehicleType, StringComparison.OrdinalIgnoreCase));
        if (typeName is null)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidVehicleType);
        var vehicleType = Enum.Parse<VehicleDomainType>(typeName);

        // Same canonical form the plate lookup uses (no spaces, dashes or dots, upper case), so lookup and uniqueness agree.
        if (string.IsNullOrWhiteSpace(request.LicensePlate))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidLicensePlate);
        var normalizedPlate = plateNormalizer.Normalize(request.LicensePlate!);
        if (!VietnamesePlateFormat.IsValid(normalizedPlate, vehicleType))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidLicensePlate);
        if (await vehicles.PlateExistsAsync(normalizedPlate, cancellationToken))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.PlateAlreadyRegistered);

        // US-VEH-003: Chiếc xe đầu tiên của tài khoản sẽ tự động được đặt làm xe mặc định.
        var existingVehicles = await vehicles.ListByAccountIdAsync(request.AccountId, cancellationToken);
        var isFirstVehicle = existingVehicles.Count == 0;

        var vehicle = Vehicle.Add(
            Guid.NewGuid(),
            request.AccountId,
            normalizedPlate,
            vehicleType,
            clock.UtcNow,
            request.NickName,
            isDefault: isFirstVehicle);

        VehicleDetailsValidation.Apply(vehicle, request.NickName, request.Details);

        vehicles.Add(vehicle);
        if (await vehicles.SaveNewVehicleAsync(cancellationToken) == SaveVehicleResult.DuplicatePlateConflict)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.PlateAlreadyRegistered);

        return Result<AddedVehicleResult>.Success(new AddedVehicleResult(
            vehicle.Id,
            vehicle.AccountId,
            vehicle.LicensePlate,
            vehicle.VehicleType.ToString(),
            vehicle.NickName,
            vehicle.CreatedAtUtc,
            vehicle.IsDefault,
            request.Details));
    }
}
