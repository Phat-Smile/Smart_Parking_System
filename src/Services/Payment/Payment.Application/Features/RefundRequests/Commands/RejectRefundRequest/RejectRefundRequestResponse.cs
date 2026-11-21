namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;

public sealed record RejectRefundRequestResponse(
    Guid Id,
    string Status,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt);
