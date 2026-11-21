using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;

public sealed record RejectRefundRequestCommand(
    Guid Id,
    Guid AdminId,
    string? AdminRole,
    string Reason) : IRequest<Result<RejectRefundRequestResponse>>;
