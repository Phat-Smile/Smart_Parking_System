namespace ParkingSystem.Identity.Infrastructure.OwnerDocuments;

public sealed class OwnerDocumentsOptions
{
    public const string SectionName = "OwnerDocuments";

    public string? RootPath { get; set; }

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    public int MaxFilesPerApplication { get; set; } = 10;

    public string EffectiveRootPath =>
        !string.IsNullOrWhiteSpace(RootPath)
            ? RootPath
            : Path.Combine(AppContext.BaseDirectory, "data", "owner-documents");
}
