using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public interface IOwnerDocumentRepository
{
    void Add(OwnerApplicationDocument document);
    void Remove(OwnerApplicationDocument document);
    Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken);
    Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
