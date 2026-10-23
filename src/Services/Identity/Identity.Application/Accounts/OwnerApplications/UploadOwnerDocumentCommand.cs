using System.Security.Cryptography;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record UploadOwnerDocumentCommand(
    Guid AccountId,
    string DocumentType,
    string FileName,
    Stream Stream,
    long SizeBytes) : IRequest<Result<OwnerDocumentDto>>;

public sealed class UploadOwnerDocumentCommandHandler(
    IAccountRepository accounts,
    IOwnerDocumentRepository documents,
    IOwnerDocumentStore store,
    IOwnerDocumentRequirements requirements,
    IClock clock) : IRequestHandler<UploadOwnerDocumentCommand, Result<OwnerDocumentDto>>
{
    public async Task<Result<OwnerDocumentDto>> Handle(UploadOwnerDocumentCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.OwnerApplicationStatus is null)
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.ApplicationNotFound);

        if (account.OwnerApplicationStatus == OwnerApplicationStatus.Approved)
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.ApplicationLocked);

        if (!account.OwnerType.HasValue)
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.OwnerTypeRequired);

        var docType = request.DocumentType?.Trim() ?? string.Empty;
        if (!requirements.IsAllowed(account.OwnerType.Value, docType))
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.DocumentInvalidType);

        if (request.SizeBytes <= 0 || request.SizeBytes > requirements.MaxFileSizeBytes)
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.DocumentTooLarge);

        // Inspect header bytes (up to 16 bytes) for MIME verification
        var header = new byte[16];
        var readHeaderBytes = await request.Stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        var detectedType = OwnerDocumentContentInspector.DetectContentType(header.AsSpan(0, readHeaderBytes));
        if (detectedType is null)
            return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.DocumentUnsupportedContent);

        // Reset stream position if seekable; if not, wrap in a combined stream
        Stream streamToHashAndSave;
        if (request.Stream.CanSeek)
        {
            request.Stream.Position = 0;
            streamToHashAndSave = request.Stream;
        }
        else
        {
            var headerStream = new MemoryStream(header, 0, readHeaderBytes);
            streamToHashAndSave = new CompositeStream(headerStream, request.Stream);
        }

        var existing = await documents.FindByAccountAndTypeAsync(account.Id, docType, cancellationToken);
        if (existing is null)
        {
            var currentCount = await documents.CountByAccountAsync(account.Id, cancellationToken);
            if (currentCount >= requirements.MaxFilesPerApplication)
                return Result<OwnerDocumentDto>.Failure(OwnerApplicationErrors.DocumentLimitReached);
        }

        var storageKey = $"{Guid.NewGuid():N}.dat";
        string sha256Hex;
        string oldStorageKeyToDelete = existing?.StorageKey ?? string.Empty;

        // Compute SHA-256 while streaming to store or temp buffer
        using (var sha = SHA256.Create())
        using (var hashingStream = new CryptoStream(streamToHashAndSave, sha, CryptoStreamMode.Read))
        {
            await store.SaveAsync(hashingStream, storageKey, cancellationToken);
            sha256Hex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        var now = clock.UtcNow;
        OwnerApplicationDocument resultDoc;

        if (existing is not null)
        {
            existing.ReplaceContent(
                request.FileName,
                detectedType,
                request.SizeBytes,
                sha256Hex,
                storageKey,
                now);
            resultDoc = existing;
        }
        else
        {
            resultDoc = OwnerApplicationDocument.Create(
                Guid.NewGuid(),
                account.Id,
                docType,
                request.FileName,
                detectedType,
                request.SizeBytes,
                sha256Hex,
                storageKey,
                now);
            documents.Add(resultDoc);
        }

        await documents.SaveChangesAsync(cancellationToken);

        // Cleanup old file asynchronously if replacing
        if (!string.IsNullOrEmpty(oldStorageKeyToDelete))
        {
            _ = store.DeleteAsync(oldStorageKeyToDelete, CancellationToken.None);
        }

        return Result<OwnerDocumentDto>.Success(new OwnerDocumentDto(
            resultDoc.Id,
            resultDoc.DocumentType,
            resultDoc.OriginalFileName,
            resultDoc.ContentType,
            resultDoc.SizeBytes,
            resultDoc.UploadedAtUtc));
    }

    private sealed class CompositeStream(Stream first, Stream second) : Stream
    {
        private bool _firstExhausted;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_firstExhausted)
            {
                var n = first.Read(buffer, offset, count);
                if (n > 0) return n;
                _firstExhausted = true;
            }
            return second.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_firstExhausted)
            {
                var n = await first.ReadAsync(buffer, cancellationToken);
                if (n > 0) return n;
                _firstExhausted = true;
            }
            return await second.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                first.Dispose();
                second.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
