namespace ParkingSystem.Identity.Persistence.Profiles;

public sealed class CustomerMedia
{
    private CustomerMedia() { }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    public static CustomerMedia Create(Guid accountId, string kind, string contentType,
        long sizeBytes, string storageKey, DateTimeOffset uploadedAtUtc) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Kind = kind, ContentType = contentType,
        SizeBytes = sizeBytes, StorageKey = storageKey, UploadedAtUtc = uploadedAtUtc
    };
}
