using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

public sealed class OwnerApplicationDocumentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();
    private const string ValidSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string ValidStorageKey = "0123456789abcdef0123456789abcdef.dat";

    [Fact]
    public void Create_with_valid_arguments_initializes_entity()
    {
        var doc = OwnerApplicationDocument.Create(
            DocumentId,
            AccountId,
            "PremisesRightProof",
            "hop_dong_thue_mat_bang.pdf",
            "application/pdf",
            1024 * 1024,
            ValidSha256,
            ValidStorageKey,
            Now);

        Assert.Equal(DocumentId, doc.Id);
        Assert.Equal(AccountId, doc.AccountId);
        Assert.Equal("PremisesRightProof", doc.DocumentType);
        Assert.Equal("hop_dong_thue_mat_bang.pdf", doc.OriginalFileName);
        Assert.Equal("application/pdf", doc.ContentType);
        Assert.Equal(1024 * 1024, doc.SizeBytes);
        Assert.Equal(ValidSha256, doc.Sha256);
        Assert.Equal(ValidStorageKey, doc.StorageKey);
        Assert.Equal(Now, doc.UploadedAtUtc);
        Assert.Null(doc.PurgedAtUtc);
    }

    [Fact]
    public void Create_sanitizes_path_traversal_in_filename()
    {
        var doc = OwnerApplicationDocument.Create(
            DocumentId,
            AccountId,
            "PremisesRightProof",
            "../../etc/passwd/fake_file.pdf",
            "application/pdf",
            2048,
            ValidSha256,
            ValidStorageKey,
            Now);

        Assert.Equal("fake_file.pdf", doc.OriginalFileName);
    }

    [Fact]
    public void Create_with_empty_id_throws_ArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                Guid.Empty,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Fact]
    public void Create_with_empty_account_id_throws_ArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                Guid.Empty,
                "PremisesRightProof",
                "doc.pdf",
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999999)]
    public void Create_with_non_positive_size_throws_ArgumentOutOfRangeException(long invalidSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                "application/pdf",
                invalidSize,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_or_whitespace_filename_throws_ArgumentException(string? invalidFileName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                invalidFileName!,
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Fact]
    public void Create_with_filename_exceeding_max_length_throws_ArgumentException()
    {
        var tooLongName = new string('a', 256) + ".pdf";

        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                tooLongName,
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_content_type_throws_ArgumentException(string? invalidContentType)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                invalidContentType!,
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Fact]
    public void Create_with_content_type_exceeding_max_length_throws_ArgumentException()
    {
        var tooLongContentType = new string('x', 101);

        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                tooLongContentType,
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_document_type_throws_ArgumentException(string? invalidDocType)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                invalidDocType!,
                "doc.pdf",
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Fact]
    public void Create_with_document_type_exceeding_max_length_throws_ArgumentException()
    {
        var tooLongDocType = new string('d', 51);

        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                tooLongDocType,
                "doc.pdf",
                "application/pdf",
                100,
                ValidSha256,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not64chars")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85")] // 63 chars
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855f")] // 65 chars
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85z")] // invalid non-hex char 'z'
    public void Create_with_invalid_sha256_throws_ArgumentException(string? invalidSha256)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                "application/pdf",
                100,
                invalidSha256!,
                ValidStorageKey,
                Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_storage_key_throws_ArgumentException(string? invalidStorageKey)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OwnerApplicationDocument.Create(
                DocumentId,
                AccountId,
                "PremisesRightProof",
                "doc.pdf",
                "application/pdf",
                100,
                ValidSha256,
                invalidStorageKey!,
                Now));
    }

    [Fact]
    public void ReplaceContent_updates_document_metadata_and_storage_key()
    {
        var doc = OwnerApplicationDocument.Create(
            DocumentId,
            AccountId,
            "PremisesRightProof",
            "old.pdf",
            "application/pdf",
            1000,
            ValidSha256,
            ValidStorageKey,
            Now);

        const string newSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string newKey = "newstoragekey1234567890abcdef.dat";
        var newTime = Now.AddHours(2);

        doc.ReplaceContent(
            "new_updated.pdf",
            "application/pdf",
            5000,
            newSha256,
            newKey,
            newTime);

        Assert.Equal("new_updated.pdf", doc.OriginalFileName);
        Assert.Equal(5000, doc.SizeBytes);
        Assert.Equal(newSha256, doc.Sha256);
        Assert.Equal(newKey, doc.StorageKey);
        Assert.Equal(newTime, doc.UploadedAtUtc);
    }

    [Fact]
    public void Account_SetOwnerType_updates_type_when_application_is_not_approved()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        Assert.Null(account.OwnerType);

        account.SetOwnerType(OwnerType.Individual);
        Assert.Equal(OwnerType.Individual, account.OwnerType);

        account.SetOwnerType(OwnerType.Enterprise);
        Assert.Equal(OwnerType.Enterprise, account.OwnerType);
    }

    [Fact]
    public void Account_SetOwnerType_throws_when_application_is_already_approved()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        account.ApproveOwnerApplication(Guid.NewGuid(), Now.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => account.SetOwnerType(OwnerType.Enterprise));
    }
}
