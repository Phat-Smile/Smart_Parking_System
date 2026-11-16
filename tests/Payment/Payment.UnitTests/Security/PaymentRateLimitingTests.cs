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
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.WebAPI.Endpoints;
using ParkingSystem.Payment.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Security;

// Kiểm thử giới hạn tần suất (Rate Limiting) trên các route thanh toán công khai ẩn danh của Payment service.
// Đảm bảo chống tấn công vét cạn / spam request vào các route tra cứu trạng thái và xác thực chữ ký VNPay (US-AUTH-007).
public sealed class PaymentRateLimitingTests
{
    private const int TestPermitsInWindow = 3;

    [Fact]
    public void Constants_are_properly_configured()
    {
        Assert.Equal("AnonymousPublicStatusPolicy", PaymentRateLimiting.PolicyName);
        Assert.Equal("RateLimiting:PublicStatus", PaymentRateLimiting.SectionName);
        Assert.Equal(30, PaymentRateLimiting.DefaultPermitsPerMinute);
        Assert.Equal("payment.rate_limited", PaymentRateLimiting.RejectedErrorCode);
    }

    [Fact]
    public async Task Public_status_allows_requests_up_to_limit_and_rejects_with_429_beyond()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();

        // 3 requests trong quota -> HTTP 200
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var response = await client.GetAsync("/api/payment/parking-sessions/public-status?txnRef=TXN_OK", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Request thứ 4 vượt ngưỡng -> HTTP 429 Too Many Requests
        using var rejected = await client.GetAsync("/api/payment/parking-sessions/public-status?txnRef=TXN_OK", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);

        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentRateLimiting.RejectedErrorCode, body.GetProperty("errorCode").GetString());
        Assert.Equal(StatusCodes.Status429TooManyRequests, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task VnPay_return_verify_is_protected_by_the_same_rate_limiting_policy()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();

        // 3 requests đầu -> HTTP 200
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var response = await client.GetAsync("/api/payment/parking-sessions/vnpay-return-verify?vnp_TxnRef=TXN_123", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Request thứ 4 -> HTTP 429
        using var rejected = await client.GetAsync("/api/payment/parking-sessions/vnpay-return-verify?vnp_TxnRef=TXN_123", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentRateLimiting.RejectedErrorCode, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Different_ips_via_x_forwarded_for_have_independent_quotas()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();

        // Client A (IP 10.0.0.1) dùng hết quota
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/payment/parking-sessions/public-status?txnRef=TXN_A");
            req.Headers.Add("X-Forwarded-For", "10.0.0.1");
            using var res = await client.SendAsync(req, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Client A gửi tiếp -> bị 429
        {
            using var reqA = new HttpRequestMessage(HttpMethod.Get, "/api/payment/parking-sessions/public-status?txnRef=TXN_A");
            reqA.Headers.Add("X-Forwarded-For", "10.0.0.1");
            using var resA = await client.SendAsync(reqA, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, resA.StatusCode);
        }

        // Client B (IP 10.0.0.2) vẫn được phép gửi vì có bucket riêng biệt
        {
            using var reqB = new HttpRequestMessage(HttpMethod.Get, "/api/payment/parking-sessions/public-status?txnRef=TXN_B");
            reqB.Headers.Add("X-Forwarded-For", "10.0.0.2");
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
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
            string? returnUrl = null) => "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?mock=true";

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
