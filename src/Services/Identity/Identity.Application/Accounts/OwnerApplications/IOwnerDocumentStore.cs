namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public interface IOwnerDocumentStore
{
    Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
