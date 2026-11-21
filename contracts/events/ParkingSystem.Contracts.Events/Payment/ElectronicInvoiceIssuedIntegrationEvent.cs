namespace ParkingSystem.Contracts.Events.Payment;

/// <summary>
/// payment.electronic-invoice-issued.v1: published when a parking session fee payment completes successfully
/// and an electronic invoice / receipt is issued with genuine payment ledger data.
/// Complies with NFR-15 anonymization requirements (MaskedPlateNumber).
/// </summary>
public sealed record ElectronicInvoiceIssuedIntegrationEvent(
    Guid SessionId,
    Guid PaymentId,
    Guid ParkingLotId,
    string MaskedPlateNumber,
    long TotalFee,
    long PaidAmount,
    long AmountDue,
    DateTimeOffset PaymentTimeUtc,
    DateTimeOffset ExitWindowExpiresUtc,
    Guid? AccountId = null);
