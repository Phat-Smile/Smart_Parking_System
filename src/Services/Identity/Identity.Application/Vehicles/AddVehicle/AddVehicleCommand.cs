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
    public static readonly Error BicyclePhotoRequired =
        new("identity.bicycle_photo_required", "Bicycle photo is required.");
    public static readonly Error BicyclePlateNotAllowed =
        new("identity.bicycle_plate_not_allowed", "A bicycle cannot have a license plate.");
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error PlateAlreadyRegistered =
        new("identity.vehicle_plate_already_registered", "Biển số xe này đã được đăng ký trong hệ thống.");

    public static readonly Error InvalidLicensePlate =
        new("identity.invalid_license_plate", "Biển số xe không hợp lệ. Chỉ gồm chữ cái và chữ số (có thể có dấu - . hoặc khoảng trắng), dài 5 đến 12 ký tự.");

    public static readonly Error InvalidVehicleType =
        new("identity.invalid_vehicle_type", "Loại phương tiện không hợp lệ. Giá trị hợp lệ: Car, Motorbike, Bicycle.");
}

public sealed class AddVehicleCommandValidator : AbstractValidator<AddVehicleCommand>
{
    public AddVehicleCommandValidator()
    {
        VehicleDetailsValidation.AddRules(this, c => c.Details);
        RuleFor(c => c.LicensePlate).NotEmpty()
            .When(c => !string.Equals(c.VehicleType, "Bicycle", StringComparison.OrdinalIgnoreCase));
        RuleFor(c => c.LicensePlate).MaximumLength(Vehicle.MaxLicensePlateLength)
            .When(c => c.LicensePlate is not null);
        RuleFor(c => c.LicensePlate).Empty().When(c => string.Equals(c.VehicleType, "Bicycle", StringComparison.OrdinalIgnoreCase));
        RuleFor(c => c.Details).Must(d => !string.IsNullOrWhiteSpace(d?.PhotoUrl))
            .When(c => string.Equals(c.VehicleType, "Bicycle", StringComparison.OrdinalIgnoreCase))
            .WithMessage("A bicycle requires a photo URL.");

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
    private const int MinPlateLength = 5;
    private const int MaxPlateLength = 12;

    public async Task<Result<AddedVehicleResult>> Handle(AddVehicleCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.AccountNotFound);

        // Only a named value is accepted: Enum.TryParse would also accept "1", "99" or " Car".
        var typeName = Enum.GetNames<VehicleDomainType>()
            .FirstOrDefault(name => string.Equals(name, request.VehicleType, StringComparison.OrdinalIgnoreCase));
        if (typeName is null)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidVehicleType);
        var vehicleType = Enum.Parse<VehicleDomainType>(typeName);
        if (vehicleType == VehicleDomainType.Bicycle && !string.IsNullOrWhiteSpace(request.LicensePlate))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.BicyclePlateNotAllowed);
        if (vehicleType == VehicleDomainType.Bicycle && !VehicleDetailsValidation.IsCustomerMediaUrl(request.Details?.PhotoUrl))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.BicyclePhotoRequired);

        // Same canonical form the plate lookup uses (no spaces, dashes or dots, upper case), so lookup and uniqueness agree.
        string? normalizedPlate = null;
        if (vehicleType != VehicleDomainType.Bicycle)
        {
            if (string.IsNullOrWhiteSpace(request.LicensePlate))
                return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidLicensePlate);
            normalizedPlate = plateNormalizer.Normalize(request.LicensePlate!);
            if (normalizedPlate.Length is < MinPlateLength or > MaxPlateLength || !normalizedPlate.All(char.IsAsciiLetterOrDigit))
                return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidLicensePlate);
            if (await vehicles.PlateExistsAsync(normalizedPlate, cancellationToken))
                return Result<AddedVehicleResult>.Failure(AddVehicleErrors.PlateAlreadyRegistered);
        }

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
