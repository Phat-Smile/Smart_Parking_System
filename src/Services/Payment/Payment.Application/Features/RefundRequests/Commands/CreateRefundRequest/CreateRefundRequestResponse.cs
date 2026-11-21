namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;

public sealed record CreateRefundRequestResponse(
    Guid Id,
    string Status);
