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
using ParkingSystem.Identity.Application.Accounts.AdminAccounts;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.Sessions;
using ParkingSystem.Identity.Application.Accounts.GetCurrentAccount;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;
using AccountEndpoints = IdentityWebApi::ParkingSystem.Identity.WebAPI.Endpoints.AccountEndpoints;
using IdentityAuthorization = IdentityWebApi::ParkingSystem.Identity.WebAPI.Security.IdentityAuthorization;
using RefreshTokenCookie = IdentityWebApi::ParkingSystem.Identity.WebAPI.Security.RefreshTokenCookie;

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
        RefreshTokenCookie.AddRefreshTokenCookie(builder.Services, builder.Configuration);
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
        var problem = await ProblemAsync(response);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, problem.GetProperty("errorCode").GetString());
        Assert.Equal(600, problem.GetProperty("retryAfterSeconds").GetInt32());
        AssertNoStore(response);
    }

    [Fact]
    public async Task Successful_login_carries_tokens_and_is_not_cacheable()
    {
        _sender.Respond(Result<LoginResponse>.Success(new LoginResponse(
            "access", "Bearer", 900, "refresh", new AccountDto(Guid.NewGuid(), "a@example.test", null, "EmailVerified", "Customer"), DateTimeOffset.UtcNow.AddDays(7))));

        using var response = await PostAsync("/api/identity/accounts/login", new { email = "a@example.test", password = "Right@1234" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Retry-After"));
        AssertNoStore(response);
        var body = await ProblemAsync(response);
        Assert.Equal("access", body.GetProperty("accessToken").GetString());
        Assert.False(body.TryGetProperty("refreshToken", out _));
        var cookie = RefreshCookieHeader(response);
        Assert.StartsWith("sp_refresh=refresh;", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/identity/accounts", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_reads_the_cookie_rotates_it_and_keeps_the_token_out_of_the_body()
    {
        _sender.Respond(Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(
            "access-2", "Bearer", 900, "refresh-2", new AccountDto(Guid.NewGuid(), "a@example.test", null, "EmailVerified", "Customer"), DateTimeOffset.UtcNow.AddDays(7))));

        using var response = await SendWithCookieAsync("/api/identity/accounts/refresh", "sp_refresh=refresh-1", body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("refresh-1", Assert.IsType<RefreshTokenCommand>(Assert.Single(_sender.Requests)).RefreshToken);
        Assert.StartsWith("sp_refresh=refresh-2;", RefreshCookieHeader(response));
        Assert.False((await ProblemAsync(response)).TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Refresh_falls_back_to_the_body_for_clients_without_cookies()
    {
        _sender.Respond(Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(
            "access-2", "Bearer", 900, "refresh-2", new AccountDto(Guid.NewGuid(), "a@example.test", null, "EmailVerified", "Customer"), DateTimeOffset.UtcNow.AddDays(7))));

        using var response = await PostAsync("/api/identity/accounts/refresh", new { refreshToken = "body-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("body-token", Assert.IsType<RefreshTokenCommand>(Assert.Single(_sender.Requests)).RefreshToken);
    }

    [Fact]
    public async Task Refresh_without_cookie_or_body_is_401_and_never_reaches_the_handler()
    {
        using var response = await SendWithCookieAsync("/api/identity/accounts/refresh", cookie: null, body: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(RefreshTokenErrors.InvalidRefreshToken.Code, await ErrorCodeAsync(response));
        Assert.Empty(_sender.Requests);
        AssertCookieCleared(response);
    }

    [Fact]
    public async Task Logout_uses_the_cookie_and_clears_it()
    {
        _sender.Respond(Result<LogoutResponse>.Success(new LogoutResponse()));

        using var response = await SendWithCookieAsync("/api/identity/accounts/logout", "sp_refresh=refresh-1", body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("refresh-1", Assert.IsType<LogoutCommand>(Assert.Single(_sender.Requests)).RefreshToken);
        AssertCookieCleared(response);
    }

    [Fact]
    public async Task Logout_without_any_token_is_still_200_and_does_nothing()
    {
        using var response = await SendWithCookieAsync("/api/identity/accounts/logout", cookie: null, body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_sender.Requests);
        AssertCookieCleared(response);
    }

    [Fact]
    public async Task Me_requires_a_token_and_reads_the_account_from_it()
    {
        var caller = VerifiedAccount("me@example.test");
        _sender.Respond(Result<CurrentAccount>.Success(new CurrentAccount(
            caller.Id, caller.Email, null, null, "Customer", "EmailVerified", true, false, Now)));

        using var anonymous = await _client.GetAsync("/api/identity/accounts/me", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/identity/accounts/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(caller));
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(caller.Id, Assert.IsType<GetCurrentAccountQuery>(Assert.Single(_sender.Requests)).AccountId);
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
        AssertCookieCleared(response);
    }

    [Fact]
    public async Task Resend_cooldown_returns_429_with_the_remaining_seconds()
    {
        _sender.Respond(Result<ResentOtpResult>.Failure(ResendOtpErrors.CooldownActive(42)));

        using var response = await PostAsync("/api/identity/accounts/resend-otp", new { email = "a@example.test" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(42), response.Headers.RetryAfter?.Delta);
        Assert.Equal(42, (await ProblemAsync(response)).GetProperty("retryAfterSeconds").GetInt32());
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

    [Fact]
    public async Task Sessions_list_marks_the_current_device_from_the_sid_claim()
    {
        var caller = VerifiedAccount("devices@example.test");
        var currentSession = Guid.NewGuid();
        _sender.Respond(Result<IReadOnlyList<SessionInfo>>.Success([]));

        using var anonymous = await _client.GetAsync("/api/identity/accounts/sessions", TestContext.Current.CancellationToken);
        using var response = await SendAsync(HttpMethod.Get, "/api/identity/accounts/sessions", null, IssueToken(caller, sessionId: currentSession));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<ListSessionsQuery>(Assert.Single(_sender.Requests));
        Assert.Equal(caller.Id, query.AccountId);
        Assert.Equal(currentSession, query.CurrentSessionId);
    }

    [Fact]
    public async Task Revoking_the_current_device_also_clears_its_cookie_but_another_device_does_not()
    {
        var caller = VerifiedAccount("devices@example.test");
        var currentSession = Guid.NewGuid();
        var otherSession = Guid.NewGuid();
        _sender.Respond(Result<RevokedSession>.Success(new RevokedSession(otherSession, "ok")));

        using var other = await SendAsync(HttpMethod.Delete, $"/api/identity/accounts/sessions/{otherSession}", null, IssueToken(caller, sessionId: currentSession));
        using var current = await SendAsync(HttpMethod.Delete, $"/api/identity/accounts/sessions/{currentSession}", null, IssueToken(caller, sessionId: currentSession));

        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.False(other.Headers.Contains("Set-Cookie"));
        AssertCookieCleared(current);
        Assert.All(_sender.Requests, request => Assert.Equal(caller.Id, Assert.IsType<RevokeSessionCommand>(request).AccountId));
    }

    [Fact]
    public async Task Missing_session_maps_to_404()
    {
        _sender.Respond(Result<RevokedSession>.Failure(SessionErrors.SessionNotFound));

        using var response = await SendAsync(HttpMethod.Delete, $"/api/identity/accounts/sessions/{Guid.NewGuid()}", null, IssueToken(VerifiedAccount("x@example.test")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(SessionErrors.SessionNotFound.Code, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.Staff)]
    [InlineData(AccountRole.ParkingLotOwner)]
    public async Task Only_a_system_admin_can_list_suspend_or_activate_accounts(AccountRole role)
    {
        var caller = VerifiedAccount("caller@example.test");
        caller.AssignRole(role);
        var token = IssueToken(caller);

        using var list = await SendAsync(HttpMethod.Get, "/api/identity/admin/accounts", null, token);
        using var suspend = await PostAsync($"/api/identity/admin/accounts/{Guid.NewGuid()}/suspend", new { }, token);
        using var activate = await PostAsync($"/api/identity/admin/accounts/{Guid.NewGuid()}/activate", new { }, token);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, suspend.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, activate.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Admin_search_passes_the_filters_and_self_suspend_maps_to_403()
    {
        var admin = VerifiedAccount("admin@example.test");
        admin.AssignRole(AccountRole.SystemAdmin);
        var token = IssueToken(admin);
        _sender.Respond(Result<AccountPage>.Success(new AccountPage([], 2, 10, 0)));

        using var list = await SendAsync(HttpMethod.Get, "/api/identity/admin/accounts?search=nguyen&role=Staff&status=EmailVerified&page=2&pageSize=10", null, token);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(new SearchAccountsQuery("nguyen", "Staff", "EmailVerified", 2, 10), Assert.Single(_sender.Requests));

        _sender.Respond(Result<ChangedAccountStatus>.Failure(ChangeAccountStatusErrors.CannotChangeOwnStatus));
        using var self = await PostAsync($"/api/identity/admin/accounts/{admin.Id}/suspend", new { }, token);

        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        var command = Assert.IsType<ChangeAccountStatusCommand>(_sender.Requests[^1]);
        Assert.Equal(admin.Id, command.AdminId);
        Assert.Equal(AccountStatusChange.Suspend, command.Change);
    }

    private static Account VerifiedAccount(string email)
    {
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        return account;
    }

    private string IssueToken(Account account, string issuer = Issuer, string audience = Audience, string? privateKeyPath = null, Guid? sessionId = null)
    {
        using var jwtIssuer = new JwtTokenIssuer(Options.Create(new JwtIssuerOptions
        {
            Issuer = issuer,
            Audience = audience,
            PrivateKeyPath = privateKeyPath ?? _privateKeyPath,
            ExpirationMinutes = 15
        }));
        return jwtIssuer.IssueAccessToken(account, sessionId ?? Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    private Task<HttpResponseMessage> PostAsync(string path, object body, string? accessToken = null) =>
        SendAsync(HttpMethod.Post, path, body, accessToken);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithCookieAsync(string path, string? cookie, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string RefreshCookieHeader(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("sp_refresh=", StringComparison.Ordinal));

    // Xoá cookie = giá trị rỗng và thời điểm hết hạn trong quá khứ.
    private static void AssertCookieCleared(HttpResponseMessage response)
    {
        var cookie = RefreshCookieHeader(response);
        Assert.StartsWith("sp_refresh=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

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
