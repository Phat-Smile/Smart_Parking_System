using FluentValidation;
using MediatR;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;

public sealed record CheckInReservationCommand(
    Guid LotId,
    string PlateNumber,
    DateTimeOffset EntryTime,
    Guid? SessionId) : IRequest<Result<CheckInReservationResult>>;

public sealed record CheckInReservationResult(
    bool HasReservation,
    Guid? ReservationId,
    Guid? UserId,
    Guid? VehicleId,
    string? Message,
    DateTime? ScheduledArrivalTimeUtc = null);

public sealed class CheckInReservationCommandValidator : AbstractValidator<CheckInReservationCommand>
{
    public CheckInReservationCommandValidator()
    {
        RuleFor(c => c.LotId).NotEmpty().WithMessage("LotId không được để trống.");
        RuleFor(c => c.PlateNumber).NotEmpty().WithMessage("PlateNumber không được để trống.")
            .MaximumLength(Reservation.PlateNumberMaxLength);
    }
}

public sealed class CheckInReservationCommandHandler(
    IReservationRepository reservations) : IRequestHandler<CheckInReservationCommand, Result<CheckInReservationResult>>
{
    public async Task<Result<CheckInReservationResult>> Handle(CheckInReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await reservations.FindActiveConfirmedByLotAndPlateAsync(
            request.LotId,
            request.PlateNumber,
            request.EntryTime,
            cancellationToken);

        if (reservation is null)
        {
            return Result<CheckInReservationResult>.Success(new CheckInReservationResult(
                false,
                null,
                null,
                null,
                "Không tìm thấy đặt chỗ đang Confirmed trong khung giờ hợp lệ cho biển số này tại bãi."));
        }

        // MF-08: Chuyển trạng thái đặt chỗ sang Used và liên kết với Session
        reservation.Use(request.EntryTime, request.SessionId);
        await reservations.SaveChangesAsync(cancellationToken);

        return Result<CheckInReservationResult>.Success(new CheckInReservationResult(
            true,
            reservation.Id,
            reservation.UserId,
            reservation.VehicleId,
            "Check-in thành công. Trạng thái đặt chỗ đã chuyển sang Used.",
            reservation.AppointmentTime.UtcDateTime));
    }
}
