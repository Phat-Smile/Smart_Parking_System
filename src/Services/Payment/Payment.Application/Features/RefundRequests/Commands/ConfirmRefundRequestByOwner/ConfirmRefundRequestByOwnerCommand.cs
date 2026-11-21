using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;

public sealed record ConfirmRefundRequestByOwnerCommand(
    Guid Id,
    Guid CallerId,
    string? CallerRole) : IRequest<Result<ConfirmRefundRequestByOwnerResponse>>;
