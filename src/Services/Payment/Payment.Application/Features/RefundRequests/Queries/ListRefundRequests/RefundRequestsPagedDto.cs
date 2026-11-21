namespace ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;

public sealed record RefundRequestsPagedDto(
    IReadOnlyList<RefundRequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
