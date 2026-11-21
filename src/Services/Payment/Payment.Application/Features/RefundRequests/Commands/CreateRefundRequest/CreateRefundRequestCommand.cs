using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;

public sealed record CreateRefundRequestCommand(
    Guid CallerId,
    string? CallerRole,
    Guid LotId,
    decimal Amount,
    string Reason,
    ReferenceType? ReferenceType = null,
    string? ReferenceId = null) : IRequest<Result<CreateRefundRequestResponse>>;
