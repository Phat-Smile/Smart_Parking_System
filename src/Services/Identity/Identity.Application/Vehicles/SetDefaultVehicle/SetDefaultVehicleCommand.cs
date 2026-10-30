using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;

namespace ParkingSystem.Identity.Application.Vehicles.SetDefaultVehicle;

public sealed record SetDefaultVehicleCommand(
    Guid AccountId,
    Guid VehicleId) : IRequest<Result<Unit>>;

public static class SetDefaultVehicleErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error VehicleNotFound =
        new("identity.vehicle_not_found", "Phương tiện không tồn tại hoặc không thuộc quyền sở hữu của bạn.");
}

public sealed class SetDefaultVehicleCommandValidator : AbstractValidator<SetDefaultVehicleCommand>
{
    public SetDefaultVehicleCommandValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty().WithMessage("AccountId không được để trống.");
        RuleFor(c => c.VehicleId).NotEmpty().WithMessage("VehicleId không được để trống.");
    }
}

public sealed class SetDefaultVehicleCommandHandler(
    IAccountRepository accounts,
    IVehicleRepository vehicles) : IRequestHandler<SetDefaultVehicleCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(SetDefaultVehicleCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<Unit>.Failure(SetDefaultVehicleErrors.AccountNotFound);

        var userVehicles = await vehicles.ListByAccountIdAsync(request.AccountId, cancellationToken);
        var targetVehicle = userVehicles.FirstOrDefault(v => v.Id == request.VehicleId);
        if (targetVehicle is null)
            return Result<Unit>.Failure(SetDefaultVehicleErrors.VehicleNotFound);

        // US-VEH-003: Đặt xe chỉ định làm mặc định và hủy cờ mặc định của các xe khác
        foreach (var v in userVehicles)
        {
            if (v.Id == targetVehicle.Id)
            {
                v.SetAsDefault();
            }
            else if (v.IsDefault)
            {
                v.UnsetDefault();
            }
        }

        await vehicles.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
