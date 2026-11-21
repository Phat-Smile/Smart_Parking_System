using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;

public sealed record ListRefundRequestsQuery(
    Guid CallerId,
    string? CallerRole,
    RefundStatus? Status = null,
    Guid? LotId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<RefundRequestsPagedDto>>;
