using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Profiles.CustomerMedia;

namespace ParkingSystem.Identity.Persistence.Profiles;

internal sealed class CustomerMediaStore(IdentityDbContext db, IOwnerDocumentStore files) : ICustomerMediaStore
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedKinds =
        ["avatar", "identity-front", "identity-back", "vehicle-photo"];

    public Task<bool> ExistsAsync(Guid accountId, Guid mediaId, string kind, CancellationToken ct) =>
        db.Set<CustomerMedia>().AsNoTracking().AnyAsync(x => x.AccountId == accountId && x.Id == mediaId && x.Kind == kind, ct);

    public async Task<CustomerMediaResult> SaveAsync(Guid accountId, string kind, Stream content, long sizeBytes, CancellationToken ct)
    {
        if (!AllowedKinds.Contains(kind)) throw new ArgumentException("Invalid media kind.", nameof(kind));
        if (sizeBytes is <= 0 or > MaxBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (!await db.Accounts.AnyAsync(x => x.Id == accountId, ct)) throw new ArgumentException("Account not found.", nameof(accountId));
        if (await db.Set<CustomerMedia>().CountAsync(x => x.AccountId == accountId, ct) >= 24)
            throw new ArgumentException("Media limit reached.", nameof(accountId));

        await using var data = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) != 0)
        {
            if (data.Length + read > MaxBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            await data.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (data.Length != sizeBytes) throw new ArgumentException("File size does not match request.", nameof(sizeBytes));
        var contentType = DetectContentType(data.GetBuffer().AsSpan(0, (int)data.Length));
        if (contentType is null || (contentType == "application/pdf" && kind is "avatar" or "vehicle-photo"))
            throw new ArgumentException("Unsupported file content.", nameof(content));

        var storageKey = $"customer-{Guid.NewGuid():N}.dat";
        var saved = false;
        try
        {
            data.Position = 0;
            await files.SaveAsync(data, storageKey, ct);
            saved = true;
            var media = CustomerMedia.Create(accountId, kind, contentType, data.Length, storageKey, DateTimeOffset.UtcNow);
            db.Set<CustomerMedia>().Add(media);
            await db.SaveChangesAsync(ct);
            return ToResult(media);
        }
        catch
        {
            if (saved) await files.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<(Stream Content, string ContentType)?> OpenReadAsync(Guid accountId, Guid mediaId, CancellationToken ct)
    {
        var media = await db.Set<CustomerMedia>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == mediaId && x.AccountId == accountId, ct);
        if (media is null) return null;
        var stream = await files.OpenReadAsync(media.StorageKey, ct);
        return stream is null ? null : (stream, media.ContentType);
    }

    public async Task<bool> DeleteAsync(Guid accountId, Guid mediaId, CancellationToken ct)
    {
        var media = await db.Set<CustomerMedia>()
            .SingleOrDefaultAsync(x => x.Id == mediaId && x.AccountId == accountId, ct);
        if (media is null) return false;
        await DeleteStoredAsync(media, ct);
        return true;
    }

    public async Task DeleteIdentityMediaAsync(Guid accountId, CancellationToken ct)
    {
        var media = await db.Set<CustomerMedia>()
            .Where(x => x.AccountId == accountId && (x.Kind == "identity-front" || x.Kind == "identity-back"))
            .ToListAsync(ct);
        foreach (var item in media)
            await DeleteStoredAsync(item, ct);
    }

    private async Task DeleteStoredAsync(CustomerMedia media, CancellationToken ct)
    {
        var existingFile = await files.OpenReadAsync(media.StorageKey, ct);
        if (existingFile is not null)
        {
            await existingFile.DisposeAsync();
            if (!await files.DeleteAsync(media.StorageKey, ct))
                throw new IOException("Customer media file could not be deleted.");
        }
        db.Set<CustomerMedia>().Remove(media);
        await db.SaveChangesAsync(ct);
    }

    private static CustomerMediaResult ToResult(CustomerMedia media) => new(media.Id, media.Kind,
        media.ContentType, media.SizeBytes, media.UploadedAtUtc,
        $"/api/identity/accounts/me/media/{media.Id}");

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}
