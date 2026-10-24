extern alias IdentityWebApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;
using AccountEndpoints = IdentityWebApi::ParkingSystem.Identity.WebAPI.Endpoints.AccountEndpoints;
using IdentityAuthorization = IdentityWebApi::ParkingSystem.Identity.WebAPI.Security.IdentityAuthorization;
using OwnerDocumentEndpoints = IdentityWebApi::ParkingSystem.Identity.WebAPI.Endpoints.OwnerDocumentEndpoints;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// The admin's view of an owner application's documents on a TestServer with the real endpoints, the real JWT validation and the
// real policy; the handlers are replaced by a scripted ISender, so each test pins one HTTP contract. The handler rules
// (checklist, scoping, missing file) are covered by the unit tests.
public sealed class OwnerDocumentReviewHttpTests : IAsyncLifetime, IDisposable
{
    private const string Issuer = "ParkingSystem.Identity";
    private const string Audience = "ParkingSystem";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj");

    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), $"identity-review-http-test-{Guid.NewGuid():N}");
    private readonly ScriptedSender _sender = new();
    private readonly CapturingLoggerProvider _logs = new();
    private string _privateKeyPath = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_keyDirectory);
        using var rsa = RSA.Create(2048);
        _privateKeyPath = Path.Combine(_keyDirectory, "private.pem");
        var publicKeyPath = Path.Combine(_keyDirectory, "public.pem");
        await File.WriteAllTextAsync(_privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        await File.WriteAllTextAsync(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = Issuer,
            ["Security:Jwt:Audience"] = Audience,
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Jwt:ClockSkewSeconds"] = "30"
        });
        builder.Logging.AddProvider(_logs);
        builder.AddParkingSystemSecurity();
        IdentityAuthorization.AddIdentityAuthorization(builder.Services);
        builder.Services.AddSingleton<ISender>(_sender);

        _app = builder.Build();
        _app.UseParkingSystemAuthentication();
        AccountEndpoints.MapAccountEndpoints(_app);
        OwnerDocumentEndpoints.MapOwnerDocumentEndpoints(_app);
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    [Theory]
    [InlineData("documents")]
    [InlineData("documents/PremisesRightProof")]
    public async Task Review_routes_require_a_token(string suffix)
    {
        using var response = await GetAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/{suffix}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData(AccountRole.Customer, "documents")]
    [InlineData(AccountRole.Customer, "documents/PremisesRightProof")]
    [InlineData(AccountRole.ParkingLotOwner, "documents")]
    [InlineData(AccountRole.ParkingLotOwner, "documents/PremisesRightProof")]
    [InlineData(AccountRole.Staff, "documents")]
    [InlineData(AccountRole.Staff, "documents/PremisesRightProof")]
    public async Task Review_routes_are_forbidden_to_everyone_but_the_system_admin(AccountRole role, string suffix)
    {
        var caller = AccountWithRole(role);

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/{suffix}", IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task List_returns_the_checklist_verdict_and_the_documents_without_any_storage_detail()
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        var applicantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _sender.Respond(Result<OwnerApplicationDocumentsDto>.Success(new OwnerApplicationDocumentsDto(
            applicantId,
            "Pending",
            "HouseholdBusiness",
            IsComplete: false,
            ["HouseholdBusinessRegistration"],
            [new OwnerDocumentDto(documentId, "PremisesRightProof", "mat_bang.pdf", "application/pdf", 29, Now)])));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{applicantId}/documents", IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetOwnerApplicationDocumentsQuery>(Assert.Single(_sender.Requests));
        Assert.Equal(applicantId, query.AccountId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(applicantId, body.GetProperty("accountId").GetGuid());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal("HouseholdBusiness", body.GetProperty("ownerType").GetString());
        Assert.False(body.GetProperty("isComplete").GetBoolean());
        Assert.Equal("HouseholdBusinessRegistration", Assert.Single(body.GetProperty("missingDocumentTypes").EnumerateArray()).GetString());
        var listed = Assert.Single(body.GetProperty("documents").EnumerateArray());
        Assert.Equal(documentId, listed.GetProperty("id").GetGuid());
        Assert.Equal("PremisesRightProof", listed.GetProperty("documentType").GetString());
        Assert.Equal("mat_bang.pdf", listed.GetProperty("fileName").GetString());
        Assert.False(listed.TryGetProperty("storageKey", out _));
        Assert.False(listed.TryGetProperty("sha256", out _));
        AssertPrivate(response);
    }

    [Fact]
    public async Task List_of_an_unknown_application_is_404_with_the_error_code()
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<OwnerApplicationDocumentsDto>.Failure(OwnerApplicationErrors.ApplicationNotFound));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/documents", IssueToken(admin));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("documents")]
    [InlineData("documents/PremisesRightProof")]
    public async Task An_application_id_that_is_not_a_guid_never_reaches_a_handler(string suffix)
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);

        using var response = await GetAsync($"/api/identity/admin/owner-applications/not-a-guid/{suffix}", IssueToken(admin));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Content_streams_the_file_as_an_attachment_that_is_never_cached_or_sniffed()
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        var applicantId = Guid.NewGuid();
        var stream = new TrackingStream(PdfBytes);
        _sender.Respond(Result<OwnerDocumentContent>.Success(
            new OwnerDocumentContent(stream, "PremisesRightProof", "application/pdf", "giấy phép.pdf", PdfBytes.Length)));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{applicantId}/documents/PremisesRightProof", IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetOwnerApplicationDocumentContentQuery>(Assert.Single(_sender.Requests));
        Assert.Equal(applicantId, query.AccountId);
        Assert.Equal("PremisesRightProof", query.DocumentType);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("giấy phép.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        AssertPrivate(response);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Equal("nosniff", Assert.Single(nosniff));
        Assert.True(stream.Disposed, "the file handle must be released once the response is sent");
    }

    // The name is the applicant's own text, kept as uploaded. Whatever it holds, the download must still work and must not add a header.
    [Theory]
    [InlineData("a\r\nX-Injected: 1.pdf")]
    [InlineData("quote\"name.pdf")]
    [InlineData("semi;colon,name.pdf")]
    [InlineData("tab\tname.pdf")]
    public async Task Content_with_an_awkward_file_name_still_downloads_and_adds_no_header(string fileName)
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<OwnerDocumentContent>.Success(
            new OwnerDocumentContent(new MemoryStream(PdfBytes), "PremisesRightProof", "application/pdf", fileName, PdfBytes.Length)));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/documents/PremisesRightProof", IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains("X-Injected"));
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Theory]
    [InlineData("identity.owner_application_not_found")]
    [InlineData("identity.owner_document_not_found")]
    [InlineData("identity.owner_document_file_unavailable")]
    public async Task Content_errors_are_404_with_the_error_code(string errorCode)
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<OwnerDocumentContent>.Failure(new Error(errorCode, "message")));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/documents/PremisesRightProof", IssueToken(admin));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeAsync(response));
    }

    // Looking at a person's identity papers leaves a trace: who looked at whose, which type. The file name is the applicant's
    // own text, so it stays out of the log.
    [Fact]
    public async Task Reading_a_file_is_logged_with_the_admin_the_application_and_the_type_but_not_the_file_name()
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        var applicantId = Guid.NewGuid();
        _sender.Respond(Result<OwnerDocumentContent>.Success(
            new OwnerDocumentContent(new MemoryStream(PdfBytes), "PremisesRightProof", "application/pdf", "cccd_nguyen_van_a.pdf", PdfBytes.Length)));

        using var response = await GetAsync($"/api/identity/admin/owner-applications/{applicantId}/documents/PremisesRightProof", IssueToken(admin));
        await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(_logs.Messages, message => message.Contains(applicantId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(admin.Id.ToString(), entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PremisesRightProof", entry);
        Assert.DoesNotContain("cccd_nguyen_van_a", entry);
    }

    // The decision endpoint keeps its mapping: every refusal about the state of the application is a 409 and carries its code.
    [Theory]
    [InlineData("identity.owner_documents_incomplete")]
    [InlineData("identity.owner_type_required")]
    [InlineData("identity.owner_type_invalid")]
    [InlineData("identity.owner_application_not_pending")]
    public async Task Approve_refusals_are_409_with_the_error_code(string errorCode)
    {
        var admin = AccountWithRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<OwnerApplicationActionResult>.Failure(new Error(errorCode, "message")));

        using var response = await PostAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/approve", IssueToken(admin));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.ParkingLotOwner)]
    public async Task Approve_is_forbidden_to_everyone_but_the_system_admin(AccountRole role)
    {
        using var response = await PostAsync($"/api/identity/admin/owner-applications/{Guid.NewGuid()}/approve", IssueToken(AccountWithRole(role)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    private static Account AccountWithRole(AccountRole role)
    {
        var account = Account.Register(Guid.NewGuid(), $"{role}@example.test".ToLowerInvariant(), "hash", null, Now);
        account.VerifyEmail();
        if (role != AccountRole.Customer)
            account.AssignRole(role);
        return account;
    }

    private string IssueToken(Account account)
    {
        using var jwtIssuer = new JwtTokenIssuer(Options.Create(new JwtIssuerOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            PrivateKeyPath = _privateKeyPath,
            ExpirationMinutes = 15
        }));
        return jwtIssuer.IssueAccessToken(account, DateTimeOffset.UtcNow);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? accessToken = null) =>
        SendAsync(HttpMethod.Get, path, accessToken);

    private Task<HttpResponseMessage> PostAsync(string path, string? accessToken = null) =>
        SendAsync(HttpMethod.Post, path, accessToken);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return problem.GetProperty("errorCode").GetString();
    }

    private static void AssertPrivate(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return [.. _messages];
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!category.Contains("OwnerDocument", StringComparison.Ordinal))
                    return;
                lock (messages)
                    messages.Add(formatter(state, exception));
            }
        }
    }

    // Returns the scripted result for any request and records what the endpoint sent.
    private sealed class ScriptedSender : ISender
    {
        private object? _response;

        public List<object> Requests { get; } = [];

        public void Respond(object response) => _response = response;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult((TResponse)(_response ?? throw new InvalidOperationException("No response scripted.")));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
