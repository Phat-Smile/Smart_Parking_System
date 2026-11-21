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
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;
using ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.WebAPI.Endpoints;
using ParkingSystem.Payment.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

public sealed class RefundRequestHttpIntegrationTests
{
    private const string RoleHeader = "X-Test-Role";
    private const string AmrHeader = "X-Test-Amr";
    private const string SubHeader = "X-Test-Sub";
    private static readonly Guid DefaultUserId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Customer_can_create_refund_request_201()
    {
        var requestId = Guid.NewGuid();
        var sender = new FakeSender().Returns<CreateRefundRequestCommand, CreateRefundRequestResponse>(
            Result<CreateRefundRequestResponse>.Success(new CreateRefundRequestResponse(requestId, "Requested")));
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.CreateRefundRequestDto(
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố trừ tiền 2 lần");

        using var response = await SendAsync(app, HttpMethod.Post, "/api/payment/refund-requests", "Customer", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(requestId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("Requested", body.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("ParkingLotOwner")]
    [InlineData("LotOwner")]
    [InlineData("Staff")]
    [InlineData("SystemAdmin")]
    public async Task Non_customer_creating_refund_request_returns_403(string role)
    {
        var sender = new FakeSender();
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.CreateRefundRequestDto(
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố");

        using var response = await SendAsync(app, HttpMethod.Post, "/api/payment/refund-requests", role, payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_creating_refund_request_returns_401()
    {
        var sender = new FakeSender();
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.CreateRefundRequestDto(
            LotId: Guid.NewGuid(),
            Amount: 20000m,
            Reason: "Sự cố");

        using var response = await SendAsync(app, HttpMethod.Post, "/api/payment/refund-requests", null, payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_confirm_refund_request_200()
    {
        var requestId = Guid.NewGuid();
        var ownerId = DefaultUserId;
        var sender = new FakeSender().Returns<ConfirmRefundRequestByOwnerCommand, ConfirmRefundRequestByOwnerResponse>(
            Result<ConfirmRefundRequestByOwnerResponse>.Success(
                new ConfirmRefundRequestByOwnerResponse(requestId, "Requested", ownerId)));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/owner-confirmation", "ParkingLotOwner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(requestId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal(ownerId.ToString(), body.GetProperty("ownerConfirmedBy").GetString());
    }

    [Fact]
    public async Task Customer_confirming_owner_returns_403()
    {
        var requestId = Guid.NewGuid();
        var sender = new FakeSender();
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/owner-confirmation", "Customer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_mfa_can_reject_refund_request_200()
    {
        var requestId = Guid.NewGuid();
        var adminId = DefaultUserId;
        var now = DateTimeOffset.UtcNow;
        var sender = new FakeSender().Returns<RejectRefundRequestCommand, RejectRefundRequestResponse>(
            Result<RejectRefundRequestResponse>.Success(
                new RejectRefundRequestResponse(requestId, "Rejected", adminId, now)));
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.RejectRefundRequestDto("Không đủ điều kiện");

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/reject", "SystemAdmin", payload, amr: "mfa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.Equal(adminId.ToString(), body.GetProperty("reviewedBy").GetString());
    }

    [Fact]
    public async Task Admin_without_mfa_rejecting_refund_request_returns_403()
    {
        // AdminPolicy requires amr = mfa
        var requestId = Guid.NewGuid();
        var sender = new FakeSender();
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.RejectRefundRequestDto("Từ chối");

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/reject", "SystemAdmin", payload, amr: "none");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_rejecting_refund_request_returns_403()
    {
        var requestId = Guid.NewGuid();
        var sender = new FakeSender();
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.RejectRefundRequestDto("Từ chối");

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/reject", "ParkingLotOwner", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_omitting_lot_id_on_list_returns_400()
    {
        var sender = new FakeSender().Returns<ListRefundRequestsQuery, RefundRequestsPagedDto>(
            Result<RefundRequestsPagedDto>.Failure(RefundRequestErrors.LotIdRequired));
        await using var app = await CreateAppAsync(sender);

        using var response = await SendAsync(
            app, HttpMethod.Get, "/api/payment/refund-requests", "ParkingLotOwner");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(RefundRequestErrors.LotIdRequired.Code, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Admin_reject_before_owner_confirmation_returns_409()
    {
        var requestId = Guid.NewGuid();
        var sender = new FakeSender().Returns<RejectRefundRequestCommand, RejectRefundRequestResponse>(
            Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.OwnerNotConfirmed));
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.RejectRefundRequestDto("Chưa có chủ bãi xác nhận");

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/reject", "SystemAdmin", payload, amr: "mfa");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(RefundRequestErrors.OwnerNotConfirmed.Code, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Creator_cannot_review_returns_403()
    {
        var requestId = Guid.NewGuid();
        var sender = new FakeSender().Returns<RejectRefundRequestCommand, RejectRefundRequestResponse>(
            Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.CreatorCannotReview));
        await using var app = await CreateAppAsync(sender);

        var payload = new RefundRequestEndpoints.RejectRefundRequestDto("Tự duyệt yêu cầu của mình");

        using var response = await SendAsync(
            app, HttpMethod.Post, $"/api/payment/refund-requests/{requestId}/reject", "SystemAdmin", payload, amr: "mfa");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(RefundRequestErrors.CreatorCannotReview.Code, body.GetProperty("errorCode").GetString());
    }

    // --- Helpers ---
    private static async Task<HttpResponseMessage> SendAsync(
        WebApplication app,
        HttpMethod method,
        string url,
        string? role,
        object? body = null,
        string? amr = null,
        string? sub = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        if (amr is not null)
            request.Headers.Add(AmrHeader, amr);
        if (sub is not null)
            request.Headers.Add(SubHeader, sub);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(content).RootElement;
    }

    private static async Task<WebApplication> CreateAppAsync(FakeSender sender)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddPaymentAuthorization();
        builder.Services.AddPaymentRateLimiting();
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
        builder.Services.AddSingleton<ISender>(sender);

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapRefundRequestEndpoints();
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

            var sub = Request.Headers.TryGetValue(SubHeader, out var subVal) && !string.IsNullOrWhiteSpace(subVal)
                ? subVal.ToString()
                : DefaultUserId.ToString();

            var claims = new List<Claim>
            {
                new("sub", sub),
                new(PaymentAuthorization.RoleClaimType, role.ToString())
            };

            if (Request.Headers.TryGetValue(AmrHeader, out var amr))
            {
                if (!string.IsNullOrWhiteSpace(amr) && amr.ToString() != "none")
                {
                    claims.Add(new Claim(PaymentAuthorization.AuthenticationMethodsClaimType, amr.ToString()));
                }
            }
            else if (role.ToString() == "SystemAdmin")
            {
                claims.Add(new Claim(PaymentAuthorization.AuthenticationMethodsClaimType, PaymentAuthorization.MfaMethod));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public string? SubjectId => accessor.HttpContext?.User.FindFirst("sub")?.Value;
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }

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
