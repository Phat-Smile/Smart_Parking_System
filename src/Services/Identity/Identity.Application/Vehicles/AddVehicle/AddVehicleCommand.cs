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

    public static readonly Error InvalidLicensePlate =
        new("identity.invalid_license_plate", "Biển số xe không hợp lệ. Chỉ gồm chữ cái và chữ số (có thể có dấu - . hoặc khoảng trắng), dài 5 đến 12 ký tự.");

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

        // Same canonical form the plate lookup uses (no spaces, dashes or dots, upper case), so lookup and uniqueness agree.
        var normalizedPlate = plateNormalizer.Normalize(request.LicensePlate);
        if (normalizedPlate.Length is < MinPlateLength or > MaxPlateLength || !normalizedPlate.All(char.IsAsciiLetterOrDigit))
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.InvalidLicensePlate);

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
        if (await vehicles.SaveNewVehicleAsync(cancellationToken) == SaveVehicleResult.DuplicatePlateConflict)
            return Result<AddedVehicleResult>.Failure(AddVehicleErrors.PlateAlreadyRegistered);

        return Result<AddedVehicleResult>.Success(new AddedVehicleResult(
            vehicle.Id,
            vehicle.AccountId,
            vehicle.LicensePlate,
            vehicle.VehicleType.ToString(),
            vehicle.NickName,
            vehicle.CreatedAtUtc));
    }
}
