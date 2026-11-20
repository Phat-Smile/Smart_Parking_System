namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetPublicParkingSessionFee;

/// <summary>
/// Public preview response for anonymous QR parking fee queries (Q-V2c).
/// Strictly excludes PII and paymentUrl (conforms to Tin 200 Condition 4 & NFR-15).
/// </summary>
public sealed record PublicParkingSessionFeeResponse(
    Guid SessionId,
    string MaskedPlateNumber,
    string Status,
    DateTimeOffset EntryTimeUtc,
    DateTimeOffset CalculatedAtUtc,
    int DurationMinutes,
    string CalculationMethod,
    long TotalFee,
    long PaidAmount,
    long AmountDue);
