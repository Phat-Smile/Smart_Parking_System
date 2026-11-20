namespace ParkingSystem.Contracts.Events.Parking;

/// <summary>
/// parking.session-receipt-issued.v1: published when a parking session fee receipt is issued.
/// Complies with NFR-15 anonymization requirements (MaskedPlateNumber).
/// </summary>
public sealed record ParkingSessionReceiptIssuedIntegrationEvent(
    Guid SessionId,
    Guid LotId,
    string MaskedPlateNumber,
    long TotalFee,
    long PaidAmount,
    long AmountDue,
    DateTimeOffset EntryTimeUtc,
    DateTimeOffset ExitTimeUtc,
    Guid? AccountId = null);
