using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetPublicParkingSessionFee;
using ParkingSystem.Payment.WebAPI.Endpoints;
using ParkingSystem.Payment.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Security;

public sealed class PublicParkingSessionFeeRateLimitingTests
{
    private const int TestPermitsInWindow = 2;

    [Fact]
    public void Public_fee_rate_limiting_constants_are_properly_configured()
    {
        Assert.Equal("AnonymousPublicFeePolicy", PaymentRateLimiting.PublicFeePolicyName);
        Assert.Equal("RateLimiting:PublicFee", PaymentRateLimiting.PublicFeeSectionName);
        Assert.Equal(20, PaymentRateLimiting.DefaultPublicFeePermitsPerMinute);
    }

    [Fact]
    public async Task Public_fee_endpoint_allows_requests_up_to_limit_and_returns_429_beyond()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();

        // 2 requests within limit -> HTTP 200
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var response = await client.GetAsync("/api/payment/public/sessions/fee?qr=valid-qr-123", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // 3rd request exceeds limit -> HTTP 429 Too Many Requests
        using var rejected = await client.GetAsync("/api/payment/public/sessions/fee?qr=valid-qr-123", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);

        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentRateLimiting.RejectedErrorCode, body.GetProperty("errorCode").GetString());
        Assert.Equal(StatusCodes.Status429TooManyRequests, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Different_ips_have_independent_rate_limit_buckets()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();

        // Client A consumes quota
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            req.Headers.Add("X-Forwarded-For", "192.168.1.10");
            using var res = await client.SendAsync(req, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Client A gets 429
        {
            using var reqA = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqA.Headers.Add("X-Forwarded-For", "192.168.1.10");
            using var resA = await client.SendAsync(reqA, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, resA.StatusCode);
        }

        // Client B with distinct IP still succeeds
        {
            using var reqB = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqB.Headers.Add("X-Forwarded-For", "192.168.1.20");
            using var resB = await client.SendAsync(reqB, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resB.StatusCode);
        }
    }

    private static async Task<WebApplication> CreateTestAppAsync(int overridePermits)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddPaymentAuthorization();
        builder.Services.AddPaymentRateLimiting(overridePermits: overridePermits);

        builder.Services.AddSingleton<ISender>(new DummySender());
        builder.Services.AddSingleton<IVnpayPaymentGateway>(new DummyVnpayGateway());
        builder.Services.AddSingleton<IParkingFeePaymentRepository>(new DummyPaymentRepository());

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseRateLimiter();
        app.MapParkingFeeEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class DummySender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetPublicParkingSessionFeeQuery)
            {
                var response = new PublicParkingSessionFeeResponse(
                    Guid.NewGuid(),
                    "51F-***.89",
                    "Active",
                    DateTimeOffset.UtcNow.AddMinutes(-45),
                    DateTimeOffset.UtcNow,
                    45,
                    "Block",
                    20000,
                    0,
                    20000);

                return Task.FromResult((TResponse)(object)Result<PublicParkingSessionFeeResponse>.Success(response));
            }

            throw new NotSupportedException();
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

    private sealed class DummyVnpayGateway : IVnpayPaymentGateway
    {
        public string BuildPaymentUrl(
            string txnRef,
            long amountVnd,
            string orderInfo,
            DateTimeOffset createdAtUtc,
            DateTimeOffset expiresAtUtc,
            string clientIp,
            string? returnUrl = null) => "https://sandbox.vnpayment.vn/test";

        public bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters) => true;
    }

    private sealed class DummyPaymentRepository : IParkingFeePaymentRepository
    {
        public Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct) => Task.FromResult(false);
        public Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct) => Task.FromResult<ParkingFeePayment?>(null);
        public Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct) => Task.CompletedTask;
        public Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct) => Task.FromResult<ParkingFeePayment?>(null);
        public Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync(ParkingFeePayment payment, CancellationToken ct) => Task.CompletedTask;
        public Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<ParkingFeePayment?>(null);
        public Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct) => Task.FromResult<ParkingFeePayment?>(null);
    }
}
