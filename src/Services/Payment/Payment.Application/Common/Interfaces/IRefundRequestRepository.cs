using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public sealed record RefundRequestListFilter(
    RefundStatus? Status = null,
    Guid? LotId = null,
    Guid? WalletId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record RefundRequestPage(
    IReadOnlyList<RefundRequest> Items,
    int TotalCount,
    int Page,
    int PageSize);

public interface IRefundRequestRepository
{
    Task AddAsync(RefundRequest request, CancellationToken cancellationToken = default);
    Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReloadAsync(RefundRequest request, CancellationToken cancellationToken = default);
    Task<RefundRequestPage> ListAsync(RefundRequestListFilter filter, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
