namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record OwnerDocumentDto(
    Guid Id,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc);
