using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record OwnerDocumentRequirementItem(
    string DocumentType,
    string Name,
    bool Required,
    IReadOnlyList<string> AcceptedContentTypes,
    long MaxSizeBytes);

public interface IOwnerDocumentRequirements
{
    IReadOnlyList<OwnerDocumentRequirementItem> GetRequirements(OwnerType ownerType);
    bool IsAllowed(OwnerType ownerType, string documentType);
    IReadOnlyList<string> GetMissingRequiredDocuments(OwnerType ownerType, IEnumerable<string> uploadedDocumentTypes);
    long MaxFileSizeBytes { get; }
    int MaxFilesPerApplication { get; }
}
