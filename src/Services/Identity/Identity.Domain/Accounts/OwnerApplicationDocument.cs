namespace ParkingSystem.Identity.Domain.Accounts;

public sealed class OwnerApplicationDocument
{
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 100;
    public const int MaxDocumentTypeLength = 50;
    public const int Sha256Length = 64;
    public const int MaxStorageKeyLength = 64;

    private OwnerApplicationDocument() { }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string DocumentType { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public DateTimeOffset? PurgedAtUtc { get; private set; }

    public static OwnerApplicationDocument Create(
        Guid id,
        Guid accountId,
        string documentType,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string storageKey,
        DateTimeOffset uploadedAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Document id must not be empty.", nameof(id));
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account id must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (documentType.Trim().Length > MaxDocumentTypeLength)
            throw new ArgumentException($"Document type must not exceed {MaxDocumentTypeLength} characters.", nameof(documentType));

        var sanitizedFileName = SanitizeFileName(originalFileName);
        if (sanitizedFileName.Length > MaxFileNameLength)
            throw new ArgumentException($"Original file name must not exceed {MaxFileNameLength} characters.", nameof(originalFileName));

        if (contentType.Trim().Length > MaxContentTypeLength)
            throw new ArgumentException($"Content type must not exceed {MaxContentTypeLength} characters.", nameof(contentType));

        if (storageKey.Trim().Length > MaxStorageKeyLength)
            throw new ArgumentException($"Storage key must not exceed {MaxStorageKeyLength} characters.", nameof(storageKey));

        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Document size must be positive.");

        var trimmedSha = sha256.Trim();
        if (trimmedSha.Length != Sha256Length || !trimmedSha.All(Uri.IsHexDigit))
            throw new ArgumentException($"SHA-256 hash must be {Sha256Length} hexadecimal characters.", nameof(sha256));

        return new OwnerApplicationDocument
        {
            Id = id,
            AccountId = accountId,
            DocumentType = documentType.Trim(),
            OriginalFileName = sanitizedFileName,
            ContentType = contentType.Trim().ToLowerInvariant(),
            SizeBytes = sizeBytes,
            Sha256 = trimmedSha.ToLowerInvariant(),
            StorageKey = storageKey.Trim(),
            UploadedAtUtc = uploadedAtUtc
        };
    }

    public void ReplaceContent(
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string storageKey,
        DateTimeOffset uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Document size must be positive.");

        if (sha256.Trim().Length != Sha256Length)
            throw new ArgumentException($"SHA-256 hash must be {Sha256Length} characters long.", nameof(sha256));

        OriginalFileName = SanitizeFileName(originalFileName);
        ContentType = contentType.Trim().ToLowerInvariant();
        SizeBytes = sizeBytes;
        Sha256 = sha256.Trim().ToLowerInvariant();
        StorageKey = storageKey.Trim();
        UploadedAtUtc = uploadedAtUtc;
    }

    public static string SanitizeFileName(string fileName)
    {
        var cleaned = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new ArgumentException("Original file name must not be empty.", nameof(fileName));
        if (cleaned.Length > MaxFileNameLength)
            throw new ArgumentException($"Original file name must not exceed {MaxFileNameLength} characters.", nameof(fileName));
        return cleaned;
    }
}
