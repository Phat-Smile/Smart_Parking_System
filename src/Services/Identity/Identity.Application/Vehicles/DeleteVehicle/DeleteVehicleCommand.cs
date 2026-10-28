using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts;

namespace ParkingSystem.Identity.Application.Vehicles.DeleteVehicle;

public sealed record DeleteVehicleCommand(
    Guid AccountId,
    Guid VehicleId) : IRequest<Result<DeletedVehicleResult>>;

public sealed record DeletedVehicleResult(
    Guid VehicleId,
    string Message);

public static class DeleteVehicleErrors
{
    public static readonly Error AccountNotFound =
        new("identity.account_not_found", "Tài khoản không tồn tại.");

    public static readonly Error VehicleNotFound =
        new("identity.vehicle_not_found", "Phương tiện không tồn tại trong hệ thống.");

    public static readonly Error VehicleForbidden =
        new("identity.vehicle_forbidden", "Bạn không có quyền xóa phương tiện này.");
}

public sealed class DeleteVehicleCommandHandler(
    IAccountRepository accounts,
    IVehicleRepository vehicles) : IRequestHandler<DeleteVehicleCommand, Result<DeletedVehicleResult>>
{
    public async Task<Result<DeletedVehicleResult>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result<DeletedVehicleResult>.Failure(DeleteVehicleErrors.AccountNotFound);

        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result<DeletedVehicleResult>.Failure(DeleteVehicleErrors.VehicleNotFound);

        if (vehicle.AccountId != request.AccountId)
            return Result<DeletedVehicleResult>.Failure(DeleteVehicleErrors.VehicleForbidden);

        vehicles.Remove(vehicle);
        await vehicles.SaveChangesAsync(cancellationToken);

        return Result<DeletedVehicleResult>.Success(new DeletedVehicleResult(
            request.VehicleId,
            "Phương tiện đã được xóa thành công."));
    }
}
