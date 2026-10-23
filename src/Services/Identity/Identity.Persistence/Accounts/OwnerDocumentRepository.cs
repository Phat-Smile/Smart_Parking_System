using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class OwnerDocumentRepository(IdentityDbContext dbContext) : IOwnerDocumentRepository
{
    public void Add(OwnerApplicationDocument document) => dbContext.OwnerApplicationDocuments.Add(document);

    public void Remove(OwnerApplicationDocument document) => dbContext.OwnerApplicationDocuments.Remove(document);

    public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken)
    {
        return dbContext.OwnerApplicationDocuments
            .FirstOrDefaultAsync(doc => doc.AccountId == accountId && doc.DocumentType == documentType, cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.OwnerApplicationDocuments
            .AsNoTracking()
            .Where(doc => doc.AccountId == accountId)
            .OrderBy(doc => doc.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.OwnerApplicationDocuments
            .CountAsync(doc => doc.AccountId == accountId, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
