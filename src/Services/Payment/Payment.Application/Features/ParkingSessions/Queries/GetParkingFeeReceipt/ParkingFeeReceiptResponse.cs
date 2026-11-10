namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;

public sealed record ParkingFeeReceiptResponse(
    Guid PaymentId,
    Guid SessionId,
    Guid ParkingLotId,
    Guid? AccountId,
    string PlateNumber,
    string VehicleType,
    string PaymentMethod,
    long TotalFee,
    long PrepaidAmount,
    long AmountPaid,
    DateTimeOffset PaymentTimeUtc,
    DateTimeOffset? ExitWindowExpiresUtc,
    string Status,
    DateTimeOffset? EntryTimeUtc,
    int? DurationMinutes,
    string? TransactionReference);
