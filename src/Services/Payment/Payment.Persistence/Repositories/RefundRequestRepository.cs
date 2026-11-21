using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

public sealed class RefundRequestRepository(PaymentDbContext dbContext) : IRefundRequestRepository
{
    public async Task AddAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.RefundRequests.AddAsync(request, cancellationToken);
    }

    public Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.RefundRequests.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task ReloadAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        return dbContext.Entry(request).ReloadAsync(cancellationToken);
    }

    public async Task<RefundRequestPage> ListAsync(RefundRequestListFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.RefundRequests.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.LotId.HasValue)
        {
            query = query.Where(x => x.LotId == filter.LotId.Value);
        }

        if (filter.WalletId.HasValue)
        {
            query = query.Where(x => x.WalletId == filter.WalletId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new RefundRequestPage(items, totalCount, filter.Page, filter.PageSize);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
