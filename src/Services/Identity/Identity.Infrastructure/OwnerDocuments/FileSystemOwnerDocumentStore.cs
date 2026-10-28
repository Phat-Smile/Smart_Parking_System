using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;

namespace ParkingSystem.Identity.Infrastructure.OwnerDocuments;

internal sealed class FileSystemOwnerDocumentStore(IOptions<OwnerDocumentsOptions> options) : IOwnerDocumentStore
{
    private readonly string _rootPath = options.Value.EffectiveRootPath;

    public async Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        // Sanitize key to prevent path traversal
        var safeKey = Path.GetFileName(storageKey);
        var targetPath = Path.Combine(_rootPath, safeKey);
        var tempPath = Path.Combine(_rootPath, $"{safeKey}.tmp_{Guid.NewGuid():N}");

        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            File.Move(tempPath, targetPath, overwrite: true);
            return safeKey;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore cleanup error */ }
            }
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var safeKey = Path.GetFileName(storageKey);
        var path = Path.Combine(_rootPath, safeKey);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var safeKey = Path.GetFileName(storageKey);
        var path = Path.Combine(_rootPath, safeKey);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(false);
    }
}
