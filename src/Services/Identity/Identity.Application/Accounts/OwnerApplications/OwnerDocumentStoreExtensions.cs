namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

internal static class OwnerDocumentStoreExtensions
{
    // The database row is the source of truth. A file that cannot be removed is garbage for the retention cleanup,
    // not a reason to fail a request whose row change is already (or never was) committed.
    public static async Task TryDeleteAsync(this IOwnerDocumentStore store, string storageKey)
    {
        try
        {
            await store.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception)
        {
            // See above.
        }
    }
}
