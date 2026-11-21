namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;

public sealed record ConfirmRefundRequestByOwnerResponse(
    Guid Id,
    string Status,
    Guid? OwnerConfirmedBy);
