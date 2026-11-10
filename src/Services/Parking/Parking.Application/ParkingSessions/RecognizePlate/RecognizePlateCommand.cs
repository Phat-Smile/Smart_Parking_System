using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingSessions.RecognizePlate;

public sealed record RecognizePlateCommand(
    Guid LotId,
    string ImagePayload,
    string GateDirection,
    Guid ActorUserId) : IRequest<Result<RecognizePlateResponse>>;

public sealed record MatchedBookingDto(
    Guid ReservationId,
    Guid? UserId,
    string? SlotCode);

public sealed record MatchedSessionDto(
    Guid SessionId,
    string PlateNumber,
    DateTimeOffset EntryTimeUtc,
    string Status,
    string? SlotCode);

public sealed record RecognizePlateResponse(
    string DetectedPlate,
    string NormalizedPlate,
    string VehicleType,
    double Confidence,
    bool IsConfident,
    DateTimeOffset DetectedAtUtc,
    string SuggestedAction,
    MatchedBookingDto? MatchedBooking = null,
    MatchedSessionDto? MatchedSession = null,
    string? BoundingBox = null,
    string? Provider = null);

public static class RecognizePlateErrors
{
    public static readonly Error EmptyImage =
        new("parking.ocr.empty_image", "Dữ liệu hình ảnh biển số không được để trống.");

    public static readonly Error InvalidDirection =
        new("parking.ocr.invalid_direction", "Hướng cổng kiểm soát chỉ chấp nhận 'Entry' hoặc 'Exit'.");

    public static readonly Error LotAccessDenied =
        new("parking.ocr.lot_access_denied", "Bạn không có quyền thao tác cổng tại bãi đỗ xe này.");

    public static readonly Error LotNotFound =
        new("parking.ocr.lot_not_found", "Bãi đỗ xe không tồn tại hoặc chưa được phê duyệt.");

    public static readonly Error UnreadablePlate =
        new("parking.ocr.unreadable_plate", "Không thể trích xuất biển số xe từ hình ảnh này. Vui lòng thử lại hoặc nhập tay.");
}

public sealed class RecognizePlateCommandValidator : AbstractValidator<RecognizePlateCommand>
{
    public RecognizePlateCommandValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ImagePayload)
            .NotEmpty().WithMessage(RecognizePlateErrors.EmptyImage.Message)
            .MinimumLength(10);
        RuleFor(x => x.GateDirection)
            .Must(dir => string.Equals(dir, "Entry", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(dir, "Exit", StringComparison.OrdinalIgnoreCase))
            .WithMessage(RecognizePlateErrors.InvalidDirection.Message);
    }
}
