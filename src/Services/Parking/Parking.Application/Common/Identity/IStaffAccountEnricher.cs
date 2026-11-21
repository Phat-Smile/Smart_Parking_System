namespace ParkingSystem.Parking.Application.Common.Identity;

/// <summary>
/// Enriches staff assignments with identity details (DisplayName and masked email)
/// using a single batch lookup to Identity service.
/// </summary>
public interface IStaffAccountEnricher
{
    Task<IReadOnlyDictionary<Guid, (string DisplayName, string MaskedEmail)>> BatchLookupAccountsAsync(
        IReadOnlyList<Guid> accountIds,
        CancellationToken cancellationToken);
}
