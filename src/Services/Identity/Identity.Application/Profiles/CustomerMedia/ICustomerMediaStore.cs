namespace ParkingSystem.Identity.Application.Profiles.CustomerMedia;

public sealed record CustomerMediaResult(Guid Id, string Kind, string ContentType,
    long SizeBytes, DateTimeOffset UploadedAtUtc, string Url);

public interface ICustomerMediaStore
{
    Task<bool> ExistsAsync(Guid accountId, Guid mediaId, string kind, CancellationToken ct);
    Task<CustomerMediaResult> SaveAsync(Guid accountId, string kind, Stream content, long sizeBytes, CancellationToken ct);
    Task<(Stream Content, string ContentType)?> OpenReadAsync(Guid accountId, Guid mediaId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid accountId, Guid mediaId, CancellationToken ct);
    Task DeleteIdentityMediaAsync(Guid accountId, CancellationToken ct);
}
