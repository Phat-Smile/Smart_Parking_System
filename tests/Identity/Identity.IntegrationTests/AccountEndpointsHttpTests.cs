extern alias IdentityWebApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.GetMe;
using ParkingSystem.Identity.Application.Accounts.ListSessions;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.RevokeSession;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Accounts.Admin.ActivateAccount;
using ParkingSystem.Identity.Application.Accounts.Admin.SearchAccounts;
using ParkingSystem.Identity.Application.Accounts.Admin.SuspendAccount;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;
using AccountEndpoints = IdentityWebApi::ParkingSystem.Identity.WebAPI.Endpoints.AccountEndpoints;
using IdentityAuthorization = IdentityWebApi::ParkingSystem.Identity.WebAPI.Security.IdentityAuthorization;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// The real account endpoints and the shared JWT validation on a TestServer; the handlers are replaced by a
// scripted ISender so each test pins one HTTP contract (status, headers, where the account id comes from).
// Access tokens are signed by the real JwtTokenIssuer, so these tests also check the Identity -> API token contract.
public sealed class AccountEndpointsHttpTests : IAsyncLifetime, IDisposable
{
    private const string Issuer = "ParkingSystem.Identity";
    private const string Audience = "ParkingSystem";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), $"identity-http-test-{Guid.NewGuid():N}");
    private readonly ScriptedSender _sender = new();
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
        builder.AddParkingSystemSecurity();
        IdentityAuthorization.AddIdentityAuthorization(builder.Services);
        builder.Services.AddSingleton<ISender>(_sender);

        _app = builder.Build();
        _app.UseParkingSystemAuthentication();
        AccountEndpoints.MapAccountEndpoints(_app);
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    [Fact]
    public async Task Locked_login_returns_423_with_retry_after_and_is_not_cacheable()
    {
        _sender.Respond(Result<LoginResponse>.Failure(LoginErrors.AccountTemporarilyLocked with { RetryAfterSeconds = 600 }));

        using var response = await PostAsync("/api/identity/accounts/login", new { email = "a@example.test", password = "Wrong@1234" });

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(600), response.Headers.RetryAfter?.Delta);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, await ErrorCodeAsync(response));
        AssertNoStore(response);
    }

    [Fact]
    public async Task Successful_login_carries_tokens_and_is_not_cacheable()
    {
        _sender.Respond(Result<LoginResponse>.Success(new LoginResponse(
            "access", "Bearer", 900, "refresh", new AccountDto(Guid.NewGuid(), "a@example.test", null, "EmailVerified", "Customer"))));

        using var response = await PostAsync("/api/identity/accounts/login", new { email = "a@example.test", password = "Right@1234" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Retry-After"));
        AssertNoStore(response);
    }

    [Theory]
    [InlineData("identity.invalid_credentials", HttpStatusCode.Unauthorized)]
    [InlineData("identity.email_not_verified", HttpStatusCode.Forbidden)]
    [InlineData("identity.account_suspended", HttpStatusCode.Forbidden)]
    [InlineData("identity.account_disabled", HttpStatusCode.Forbidden)]
    public async Task Login_errors_map_to_their_status(string errorCode, HttpStatusCode expected)
    {
        _sender.Respond(Result<LoginResponse>.Failure(new Error(errorCode, "message")));

        using var response = await PostAsync("/api/identity/accounts/login", new { email = "a@example.test", password = "Wrong@1234" });

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("identity.refresh_token_reused", HttpStatusCode.Unauthorized)]
    [InlineData("identity.session_revoked", HttpStatusCode.Unauthorized)]
    [InlineData("identity.account_suspended", HttpStatusCode.Forbidden)]
    [InlineData("identity.account_disabled", HttpStatusCode.Forbidden)]
    public async Task Refresh_errors_map_to_their_status_and_are_not_cacheable(string errorCode, HttpStatusCode expected)
    {
        _sender.Respond(Result<RefreshTokenResponse>.Failure(new Error(errorCode, "message")));

        using var response = await PostAsync("/api/identity/accounts/refresh", new { refreshToken = "token" });

        Assert.Equal(expected, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Resend_cooldown_returns_429_with_the_remaining_seconds()
    {
        _sender.Respond(Result<ResentOtpResult>.Failure(ResendOtpErrors.CooldownActive(42)));

        using var response = await PostAsync("/api/identity/accounts/resend-otp", new { email = "a@example.test" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(42), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Logout_all_without_a_token_is_rejected_before_any_handler_runs()
    {
        using var response = await PostAsync("/api/identity/accounts/logout-all", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Logout_all_takes_the_account_from_the_token_and_ignores_an_account_id_in_the_body()
    {
        var caller = VerifiedAccount("caller@example.test");
        var otherAccountId = Guid.NewGuid();
        _sender.Respond(Result<LogoutResponse>.Success(new LogoutResponse()));

        using var response = await PostAsync("/api/identity/accounts/logout-all", new { accountId = otherAccountId }, IssueToken(caller));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<LogoutAllCommand>(Assert.Single(_sender.Requests));
        Assert.Equal(caller.Id, command.AccountId);
    }

    [Fact]
    public async Task Tokens_for_another_audience_or_signed_by_another_key_are_rejected()
    {
        var caller = VerifiedAccount("caller@example.test");
        using var otherKeys = new TemporaryKey();

        using var wrongAudience = await PostAsync("/api/identity/accounts/logout-all", new { }, IssueToken(caller, audience: "SomeOtherApi"));
        using var wrongIssuer = await PostAsync("/api/identity/accounts/logout-all", new { }, IssueToken(caller, issuer: "SomeoneElse"));
        using var wrongKey = await PostAsync("/api/identity/accounts/logout-all", new { }, IssueToken(caller, privateKeyPath: otherKeys.Path));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongIssuer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.Staff)]
    [InlineData(AccountRole.ParkingLotOwner)]
    public async Task Only_a_system_admin_can_unlock_an_account(AccountRole role)
    {
        var caller = VerifiedAccount("caller@example.test");
        caller.AssignRole(role);

        using var response = await PostAsync($"/api/identity/admin/accounts/{Guid.NewGuid()}/unlock", new { }, IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task A_system_admin_reaches_the_unlock_handler()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        var target = Guid.NewGuid();
        _sender.Respond(Result<UnlockedAccount>.Success(new UnlockedAccount(target, "ok")));

        using var response = await PostAsync($"/api/identity/admin/accounts/{target}/unlock", new { }, IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(target, Assert.IsType<UnlockAccountCommand>(Assert.Single(_sender.Requests)).AccountId);
    }

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.Staff)]
    [InlineData(AccountRole.ParkingLotOwner)]
    public async Task Only_a_system_admin_can_change_a_role(AccountRole role)
    {
        var caller = VerifiedAccount("caller@example.test");
        caller.AssignRole(role);

        using var response = await SendAsync(HttpMethod.Put, $"/api/identity/admin/accounts/{Guid.NewGuid()}/role", new { role = "SystemAdmin" }, IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Role_change_takes_the_admin_from_the_token_and_maps_self_change_to_403()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<ChangedAccountRole>.Failure(ChangeAccountRoleErrors.CannotChangeOwnRole));

        using var response = await SendAsync(HttpMethod.Put, $"/api/identity/admin/accounts/{admin.Id}/role", new { role = "Customer" }, IssueToken(admin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ChangeAccountRoleErrors.CannotChangeOwnRole.Code, await ErrorCodeAsync(response));
        var command = Assert.IsType<ChangeAccountRoleCommand>(Assert.Single(_sender.Requests));
        Assert.Equal(admin.Id, command.AdminId);
        Assert.Equal("Customer", command.Role);
    }

    // ── GET /api/identity/accounts/me ──────────────────────────────────────────

    [Fact]
    public async Task GetMe_without_token_returns_401()
    {
        using var response = await GetAsync("/api/identity/accounts/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task GetMe_takes_account_id_from_token()
    {
        var caller = VerifiedAccount("me@example.test");
        _sender.Respond(Result<MeResult>.Success(new MeResult(
            caller.Id, "me@example.test", null, null, "Customer", "EmailVerified", true, false, Now)));

        using var response = await GetAsync("/api/identity/accounts/me", IssueToken(caller));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetMeQuery>(Assert.Single(_sender.Requests));
        Assert.Equal(caller.Id, query.AccountId);
    }

    [Fact]
    public async Task GetMe_maps_not_found_to_404()
    {
        var caller = VerifiedAccount("me@example.test");
        _sender.Respond(Result<MeResult>.Failure(GetMeErrors.AccountNotFound));

        using var response = await GetAsync("/api/identity/accounts/me", IssueToken(caller));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GetMeErrors.AccountNotFound.Code, await ErrorCodeAsync(response));
    }

    // ── GET /api/identity/accounts/sessions ────────────────────────────────────

    [Fact]
    public async Task ListSessions_without_token_returns_401()
    {
        using var response = await GetAsync("/api/identity/accounts/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task ListSessions_takes_account_id_from_token()
    {
        var caller = VerifiedAccount("sessions@example.test");
        _sender.Respond(Result<ListSessionsResult>.Success(new ListSessionsResult([])));

        using var response = await GetAsync("/api/identity/accounts/sessions", IssueToken(caller));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<ListSessionsQuery>(Assert.Single(_sender.Requests));
        Assert.Equal(caller.Id, query.AccountId);
    }

    // ── DELETE /api/identity/accounts/sessions/{sessionId} ─────────────────────

    [Fact]
    public async Task RevokeSession_without_token_returns_401()
    {
        using var response = await DeleteAsync($"/api/identity/accounts/sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task RevokeSession_takes_account_id_from_token_and_session_id_from_route()
    {
        var caller = VerifiedAccount("revoke@example.test");
        var sessionId = Guid.NewGuid();
        _sender.Respond(Result<RevokedSession>.Success(new RevokedSession(sessionId, "ok")));

        using var response = await DeleteAsync($"/api/identity/accounts/sessions/{sessionId}", IssueToken(caller));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<RevokeSessionCommand>(Assert.Single(_sender.Requests));
        Assert.Equal(caller.Id, command.AccountId);
        Assert.Equal(sessionId, command.SessionId);
    }

    [Fact]
    public async Task RevokeSession_maps_not_found_to_404()
    {
        var caller = VerifiedAccount("revoke@example.test");
        _sender.Respond(Result<RevokedSession>.Failure(RevokeSessionErrors.SessionNotFound));

        using var response = await DeleteAsync($"/api/identity/accounts/sessions/{Guid.NewGuid()}", IssueToken(caller));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(RevokeSessionErrors.SessionNotFound.Code, await ErrorCodeAsync(response));
    }

    // ── GET /api/identity/admin/accounts ───────────────────────────────────────

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.Staff)]
    [InlineData(AccountRole.ParkingLotOwner)]
    public async Task SearchAccounts_non_admin_returns_403(AccountRole role)
    {
        var caller = VerifiedAccount("nonadmin@example.test");
        caller.AssignRole(role);

        using var response = await GetAsync("/api/identity/admin/accounts?page=1&pageSize=20", IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SearchAccounts_admin_reaches_handler()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<SearchAccountsResult>.Success(new SearchAccountsResult([], 0, 1, 20)));

        using var response = await GetAsync("/api/identity/admin/accounts?page=1&pageSize=20", IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.IsType<SearchAccountsQuery>(Assert.Single(_sender.Requests));
    }

    // ── POST /api/identity/admin/accounts/{id}/suspend ─────────────────────────

    [Fact]
    public async Task SuspendAccount_non_admin_returns_403()
    {
        var caller = VerifiedAccount("customer@example.test");

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{Guid.NewGuid()}/suspend", new { }, IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SuspendAccount_takes_admin_id_from_token_and_target_id_from_route()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        var targetId = Guid.NewGuid();
        _sender.Respond(Result<SuspendedAccount>.Success(new SuspendedAccount(targetId, "ok")));

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{targetId}/suspend", new { }, IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<SuspendAccountCommand>(Assert.Single(_sender.Requests));
        Assert.Equal(admin.Id, command.AdminId);
        Assert.Equal(targetId, command.AccountId);
    }

    [Theory]
    [InlineData("identity.account_not_found", HttpStatusCode.NotFound)]
    [InlineData("identity.admin_cannot_suspend_self", HttpStatusCode.Forbidden)]
    [InlineData("identity.cannot_suspend_admin", HttpStatusCode.Forbidden)]
    public async Task SuspendAccount_errors_map_to_correct_status(string errorCode, HttpStatusCode expected)
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<SuspendedAccount>.Failure(new Error(errorCode, "msg")));

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{Guid.NewGuid()}/suspend", new { }, IssueToken(admin));

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeAsync(response));
    }

    // ── POST /api/identity/admin/accounts/{id}/activate ────────────────────────

    [Fact]
    public async Task ActivateAccount_non_admin_returns_403()
    {
        var caller = VerifiedAccount("customer@example.test");

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{Guid.NewGuid()}/activate", new { }, IssueToken(caller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task ActivateAccount_admin_reaches_handler()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        var targetId = Guid.NewGuid();
        _sender.Respond(Result<ActivatedAccount>.Success(new ActivatedAccount(targetId, "ok")));

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{targetId}/activate", new { }, IssueToken(admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<ActivateAccountCommand>(Assert.Single(_sender.Requests));
        Assert.Equal(targetId, command.AccountId);
    }

    [Theory]
    [InlineData("identity.account_not_found", HttpStatusCode.NotFound)]
    [InlineData("identity.account_not_suspended", HttpStatusCode.BadRequest)]
    public async Task ActivateAccount_errors_map_to_correct_status(string errorCode, HttpStatusCode expected)
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        _sender.Respond(Result<ActivatedAccount>.Failure(new Error(errorCode, "msg")));

        using var response = await PostAsync(
            $"/api/identity/admin/accounts/{Guid.NewGuid()}/activate", new { }, IssueToken(admin));

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(errorCode, await ErrorCodeAsync(response));
    }

    private static Account VerifiedAccount(string email)
    {
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        return account;
    }

    private string IssueToken(Account account, string issuer = Issuer, string audience = Audience, string? privateKeyPath = null)
    {
        using var jwtIssuer = new JwtTokenIssuer(Options.Create(new JwtIssuerOptions
        {
            Issuer = issuer,
            Audience = audience,
            PrivateKeyPath = privateKeyPath ?? _privateKeyPath,
            ExpirationMinutes = 15
        }));
        return jwtIssuer.IssueAccessToken(account, DateTimeOffset.UtcNow);
    }

    private Task<HttpResponseMessage> PostAsync(string path, object body, string? accessToken = null) =>
        SendAsync(HttpMethod.Post, path, body, accessToken);

    private Task<HttpResponseMessage> GetAsync(string path, string? accessToken = null) =>
        SendAsync(HttpMethod.Get, path, accessToken);

    private Task<HttpResponseMessage> DeleteAsync(string path, string? accessToken = null) =>
        SendAsync(HttpMethod.Delete, path, accessToken);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return problem.GetProperty("errorCode").GetString();
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("no-cache", response.Headers.Pragma.Select(value => value.Name));
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

    private sealed class TemporaryKey : IDisposable
    {
        public TemporaryKey()
        {
            using var rsa = RSA.Create(2048);
            File.WriteAllText(Path, rsa.ExportPkcs8PrivateKeyPem());
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"identity-http-other-{Guid.NewGuid():N}.pem");

        public void Dispose() => File.Delete(Path);
    }

    // Returns the next scripted result for any request and records what the endpoint sent.
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
