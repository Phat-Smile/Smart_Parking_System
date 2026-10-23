using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Infrastructure.OwnerDocuments;

public sealed class ConfigurationOwnerDocumentRequirements(IOptions<OwnerDocumentsOptions> options) : IOwnerDocumentRequirements
{
    private readonly OwnerDocumentsOptions _options = options.Value;

    private static readonly IReadOnlyList<string> StandardAcceptedMimes =
        ["application/pdf", "image/png", "image/jpeg"];

    public long MaxFileSizeBytes => _options.MaxFileSizeBytes;
    public int MaxFilesPerApplication => _options.MaxFilesPerApplication;

    public IReadOnlyList<OwnerDocumentRequirementItem> GetRequirements(OwnerType ownerType)
    {
        return ownerType switch
        {
            OwnerType.Individual =>
            [
                new OwnerDocumentRequirementItem(
                    "PremisesRightProof",
                    "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                    Required: true,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes),
                new OwnerDocumentRequirementItem(
                    "AuthorizationLetter",
                    "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                    Required: false,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes)
            ],
            OwnerType.HouseholdBusiness =>
            [
                new OwnerDocumentRequirementItem(
                    "HouseholdBusinessRegistration",
                    "Giấy chứng nhận đăng ký hộ kinh doanh",
                    Required: true,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes),
                new OwnerDocumentRequirementItem(
                    "PremisesRightProof",
                    "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                    Required: true,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes),
                new OwnerDocumentRequirementItem(
                    "AuthorizationLetter",
                    "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                    Required: false,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes)
            ],
            OwnerType.Enterprise =>
            [
                new OwnerDocumentRequirementItem(
                    "EnterpriseRegistration",
                    "Giấy chứng nhận đăng ký doanh nghiệp",
                    Required: true,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes),
                new OwnerDocumentRequirementItem(
                    "PremisesRightProof",
                    "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                    Required: true,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes),
                new OwnerDocumentRequirementItem(
                    "AuthorizationLetter",
                    "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                    Required: false,
                    StandardAcceptedMimes,
                    MaxFileSizeBytes)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, "Owner type is not one of the supported values.")
        };
    }

    public bool IsAllowed(OwnerType ownerType, string documentType) => ResolveDocumentType(ownerType, documentType) is not null;

    public string? ResolveDocumentType(OwnerType ownerType, string? documentType) =>
        Canonical(GetRequirements(ownerType), documentType);

    public string? ResolveKnownDocumentType(string? documentType) =>
        Canonical(Enum.GetValues<OwnerType>().SelectMany(GetRequirements), documentType);

    private static string? Canonical(IEnumerable<OwnerDocumentRequirementItem> items, string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return null;

        var wanted = documentType.Trim();
        return items
            .Select(item => item.DocumentType)
            .FirstOrDefault(candidate => string.Equals(candidate, wanted, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetMissingRequiredDocuments(OwnerType ownerType, IEnumerable<string> uploadedDocumentTypes)
    {
        var uploadedSet = new HashSet<string>(uploadedDocumentTypes.Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
        var requiredList = GetRequirements(ownerType).Where(r => r.Required).Select(r => r.DocumentType);

        return requiredList.Where(req => !uploadedSet.Contains(req)).ToList();
    }
}
