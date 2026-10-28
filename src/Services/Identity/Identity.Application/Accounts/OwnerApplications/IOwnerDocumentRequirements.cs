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
    /// <summary>The checklist of <paramref name="ownerType"/>. Throws for a value that is not an <see cref="OwnerType"/>: it has no checklist, and an empty one would let a resubmit skip every document.</summary>
    IReadOnlyList<OwnerDocumentRequirementItem> GetRequirements(OwnerType ownerType);

    bool IsAllowed(OwnerType ownerType, string documentType);

    /// <summary>The checklist's own spelling of <paramref name="documentType"/> (matched in any casing) when the checklist of <paramref name="ownerType"/> accepts it, otherwise null. This is the form to store and to look up.</summary>
    string? ResolveDocumentType(OwnerType ownerType, string? documentType);

    /// <summary>The same lookup across every checklist: a document kept after the owner type changed is outside the new checklist but must stay findable and deletable.</summary>
    string? ResolveKnownDocumentType(string? documentType);

    IReadOnlyList<string> GetMissingRequiredDocuments(OwnerType ownerType, IEnumerable<string> uploadedDocumentTypes);
    long MaxFileSizeBytes { get; }
    int MaxFilesPerApplication { get; }
}
