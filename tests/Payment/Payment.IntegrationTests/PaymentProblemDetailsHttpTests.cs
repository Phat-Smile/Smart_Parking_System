using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;
using ParkingSystem.Payment.WebAPI.Endpoints;
using ParkingSystem.Payment.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

// Demo 1 step 14: wallet routes return the DTO on success and ProblemDetails with errorCode on failure,
// like the other services, so the web app reads errors in one way. The handlers are replaced by a fake sender.
public sealed class PaymentProblemDetailsHttpTests
{
    private const string RoleHeader = "X-Test-Role";
    private const string UserId = "d0000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task Wallet_balance_is_the_dto_itself()
    {
        var sender = new FakeSender().Returns<GetMyWalletQuery, WalletBalanceDto>(
            Result<WalletBalanceDto>.Success(new WalletBalanceDto { Balance = 150000m }));
        await using var app = await CreateAppAsync(sender);

        var body = await ReadJsonAsync(await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/me", "Customer"), HttpStatusCode.OK);

        Assert.Equal(150000m, body.GetProperty("balance").GetDecimal());
        Assert.False(body.TryGetProperty("isSuccess", out _));
        Assert.False(body.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task Wallet_balance_failure_is_a_problem_with_its_error_code()
    {
        var sender = new FakeSender().Returns<GetMyWalletQuery, WalletBalanceDto>(
            Result<WalletBalanceDto>.Failure(new Error("payment.invalid_user", "Invalid user identification.")));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/me", "Customer");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "payment.invalid_user");
    }

    [Fact]
    public async Task New_top_up_is_201_and_a_replayed_top_up_is_200_both_as_the_dto()
    {
        var created = new FakeSender().Returns<CreateWalletTopupCommand, WalletTopupResultDto>(
            Result<WalletTopupResultDto>.Success(new WalletTopupResultDto { TopupId = "T1", Status = "Pending", PaymentUrl = "https://sandbox.vnpayment.vn/x" }));
        var replayed = new FakeSender().Returns<CreateWalletTopupCommand, WalletTopupResultDto>(
            Result<WalletTopupResultDto>.Success(new WalletTopupResultDto { TopupId = "T1", Status = "Pending", IsReplay = true }));

        await using (var app = await CreateAppAsync(created))
        {
            var body = await ReadJsonAsync(await SendTopUpAsync(app), HttpStatusCode.Created);
            Assert.Equal("T1", body.GetProperty("topupId").GetString());
            Assert.Equal("https://sandbox.vnpayment.vn/x", body.GetProperty("paymentUrl").GetString());
        }

        await using (var app = await CreateAppAsync(replayed))
        {
            var body = await ReadJsonAsync(await SendTopUpAsync(app), HttpStatusCode.OK);
            Assert.Equal("T1", body.GetProperty("topupId").GetString());
        }
    }

    [Theory]
    [InlineData("payment.idempotency_key_reused", HttpStatusCode.Conflict)]
    [InlineData("payment.topup_expired", HttpStatusCode.Conflict)]
    [InlineData("payment.balance_limit_exceeded", HttpStatusCode.BadRequest)]
    public async Task Top_up_failures_are_problems_with_their_error_code(string code, HttpStatusCode expected)
    {
        var sender = new FakeSender().Returns<CreateWalletTopupCommand, WalletTopupResultDto>(
            Result<WalletTopupResultDto>.Failure(new Error(code, "message")));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendTopUpAsync(app);

        await AssertProblemAsync(response, expected, code);
    }

    [Fact]
    public async Task Wallet_history_failures_are_problems()
    {
        var customerOnly = new FakeSender().Returns<GetWalletTransactionsQuery, WalletTransactionsPagedDto>(
            Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.CustomerOnly));
        var badPage = new FakeSender().Returns<GetWalletTransactionsQuery, WalletTransactionsPagedDto>(
            Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidPage));

        await using (var app = await CreateAppAsync(customerOnly))
            await AssertProblemAsync(await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/transactions", "Staff"),
                HttpStatusCode.Forbidden, "payment.customer_only");

        await using (var app = await CreateAppAsync(badPage))
            await AssertProblemAsync(await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/transactions?page=0", "Customer"),
                HttpStatusCode.BadRequest, "payment.invalid_page");
    }

    [Fact]
    public async Task Top_up_status_not_found_is_a_404_problem()
    {
        var sender = new FakeSender().Returns<GetWalletTopupStatusQuery, WalletTopupStatusDto>(
            Result<WalletTopupStatusDto>.Failure(PaymentErrors.Topup.NotFound));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/top-up/T404", "Customer");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "payment.topup_not_found");
    }

    [Theory]
    [InlineData("payment.session_id_invalid", HttpStatusCode.BadRequest)]
    [InlineData("payment.session_not_found", HttpStatusCode.NotFound)]
    [InlineData("payment.session_access_denied", HttpStatusCode.Forbidden)]
    [InlineData("payment.session_not_active", HttpStatusCode.Conflict)]
    [InlineData("payment.parking_service_unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task Fee_failures_are_problems_with_their_error_code(string code, HttpStatusCode expected)
    {
        var sender = new FakeSender().Returns<GetParkingSessionFeeQuery, ParkingSessionFeeResponse>(
            Result<ParkingSessionFeeResponse>.Failure(new Error(code, "message")));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(app, HttpMethod.Get, $"/api/payment/parking-sessions/{Guid.NewGuid()}/fee", "Staff");

        await AssertProblemAsync(response, expected, code);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Audit_log_failure_is_a_400_problem()
    {
        var sender = new FakeSender().Returns<GetAuditLogsQuery, AuditLogsPagedDto>(
            Result<AuditLogsPagedDto>.Failure(new Error("payment.invalid_page", "message")));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(app, HttpMethod.Get, "/api/payment/admin/audit-logs", "SystemAdmin");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "payment.invalid_page");
    }

    [Fact]
    public async Task No_token_is_401_without_the_old_envelope()
    {
        await using var app = await CreateAppAsync(new FakeSender());

        using var response = await SendAsync(app, HttpMethod.Get, "/api/payment/wallets/me", role: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("isSuccess", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cash_payment_requires_authentication()
    {
        await using var app = await CreateAppAsync(new FakeSender());
        var sessionId = Guid.NewGuid();

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/payment/parking-sessions/{sessionId}/pay/cash",
            role: null,
            body: new { expectedAmountDue = 20000L },
            header: ("X-Idempotency-Key", Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cash_payment_requires_staff_or_owner_role()
    {
        await using var app = await CreateAppAsync(new FakeSender());
        var sessionId = Guid.NewGuid();

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/payment/parking-sessions/{sessionId}/pay/cash",
            role: "Customer",
            body: new { expectedAmountDue = 20000L },
            header: ("X-Idempotency-Key", Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cash_payment_missing_idempotency_key_is_bad_request()
    {
        await using var app = await CreateAppAsync(new FakeSender());
        var sessionId = Guid.NewGuid();

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/payment/parking-sessions/{sessionId}/pay/cash",
            role: "Staff",
            body: new { expectedAmountDue = 20000L });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, PayParkingFeeErrors.IdempotencyKeyInvalid.Code);
    }

    [Fact]
    public async Task Cash_payment_invalid_amount_is_bad_request()
    {
        await using var app = await CreateAppAsync(new FakeSender());
        var sessionId = Guid.NewGuid();

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/payment/parking-sessions/{sessionId}/pay/cash",
            role: "Staff",
            body: new { expectedAmountDue = 0L },
            header: ("X-Idempotency-Key", Guid.NewGuid().ToString()));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, PayParkingFeeErrors.ExpectedAmountInvalid.Code);
    }

    [Fact]
    public async Task Cash_payment_success_returns_dto()
    {
        var sessionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var staffId = Guid.Parse(UserId);
        var txId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var exitExpires = now.AddMinutes(15);

        var expectedDto = new ConfirmCashParkingFeePaymentResponse(
            TransactionId: txId,
            SessionId: sessionId,
            AmountPaid: 20000L,
            PaymentMethod: "Cash",
            ConfirmedByStaffId: staffId,
            ParkingLotId: lotId,
            PaymentTimeUtc: now,
            ExitWindowExpiresUtc: exitExpires);

        var sender = new FakeSender().Returns<ConfirmCashParkingFeePaymentCommand, ConfirmCashParkingFeePaymentResponse>(
            Result<ConfirmCashParkingFeePaymentResponse>.Success(expectedDto));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/payment/parking-sessions/{sessionId}/pay/cash",
            role: "Staff",
            body: new { expectedAmountDue = 20000L, reason = "Thu tiền mặt" },
            header: ("X-Idempotency-Key", Guid.NewGuid().ToString()));

        var body = await ReadJsonAsync(response, HttpStatusCode.OK);

        Assert.Equal(txId, body.GetProperty("transactionId").GetGuid());
        Assert.Equal(sessionId, body.GetProperty("sessionId").GetGuid());
        Assert.Equal(20000L, body.GetProperty("amountPaid").GetInt64());
        Assert.Equal("Cash", body.GetProperty("paymentMethod").GetString());
        Assert.Equal(staffId, body.GetProperty("confirmedByStaffId").GetGuid());
        Assert.Equal(lotId, body.GetProperty("parkingLotId").GetGuid());
        Assert.False(body.TryGetProperty("isSuccess", out _));
    }

    private static Task<HttpResponseMessage> SendTopUpAsync(WebApplication app) =>
        SendAsync(app, HttpMethod.Post, "/api/payment/wallets/top-up", "Customer", new { amount = 100000 },
            ("Idempotency-Key", Guid.NewGuid().ToString()));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {text}");
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected, string errorCode)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {text}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(text).RootElement;
        Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
        Assert.False(problem.TryGetProperty("isSuccess", out _));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        WebApplication app, HttpMethod method, string route, string? role, object? body = null, (string Name, string Value)? header = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        if (header is { } extra)
            request.Headers.Add(extra.Name, extra.Value);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> CreateAppAsync(FakeSender sender)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddPaymentAuthorization();
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
        builder.Services.AddSingleton<ISender>(sender);

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWalletEndpoints();
        app.MapParkingFeeEndpoints();
        app.MapAuditLogEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeader";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", UserId), new Claim(PaymentAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public string? SubjectId => accessor.HttpContext?.User.FindFirst("sub")?.Value;

        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }

    // Answers each request type with a canned result, so the test checks only the HTTP mapping.
    private sealed class FakeSender : ISender
    {
        private readonly Dictionary<Type, object> _responses = [];

        public FakeSender Returns<TRequest, TValue>(Result<TValue> result)
        {
            _responses[typeof(TRequest)] = result;
            return this;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)_responses[request.GetType()]);

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
