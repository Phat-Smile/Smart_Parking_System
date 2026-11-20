using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
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
    public async Task Requests_through_trusted_proxy_with_different_x_forwarded_for_have_separate_rate_limit_buckets()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();
        const string trustedProxyIp = "172.18.0.2"; // In 172.16.0.0/12 trusted proxy CIDR

        // Client A gửi requests đến ngưỡng giới hạn TestPermitsInWindow qua trusted proxy
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var reqA = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqA.Headers.Add("X-Test-Remote-Ip", trustedProxyIp);
            reqA.Headers.Add("X-Forwarded-For", "203.0.113.1");
            using var resA = await client.SendAsync(reqA, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resA.StatusCode);
        }

        // Client B gửi request cũng qua trusted proxy nhưng với IP client khác -> có bucket riêng nên vẫn thành công HTTP 200
        {
            using var reqB = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqB.Headers.Add("X-Test-Remote-Ip", trustedProxyIp);
            reqB.Headers.Add("X-Forwarded-For", "203.0.113.2");
            using var resB = await client.SendAsync(reqB, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resB.StatusCode);
        }

        // Client A gửi thêm 1 request vượt ngưỡng -> bị chặn HTTP 429
        {
            using var reqAOverflow = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqAOverflow.Headers.Add("X-Test-Remote-Ip", trustedProxyIp);
            reqAOverflow.Headers.Add("X-Forwarded-For", "203.0.113.1");
            using var resAOverflow = await client.SendAsync(reqAOverflow, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, resAOverflow.StatusCode);
        }
    }

    [Fact]
    public async Task Requests_from_untrusted_remote_ip_ignore_spoofed_x_forwarded_for_and_share_rate_limit_bucket()
    {
        await using var app = await CreateTestAppAsync(TestPermitsInWindow);
        var client = app.GetTestClient();
        const string untrustedCallerIp = "198.51.100.25"; // External public IP NOT in KnownNetworks

        // Gửi các requests trong hạn mức với các header X-Forwarded-For giả mạo khác nhau từ cùng untrusted caller IP
        for (var i = 0; i < TestPermitsInWindow; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            req.Headers.Add("X-Test-Remote-Ip", untrustedCallerIp);
            req.Headers.Add("X-Forwarded-For", $"10.0.0.{10 + i}");
            using var res = await client.SendAsync(req, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Request tiếp theo dù đổi header X-Forwarded-For sang IP khác vẫn bị chặn 429 vì XFF bị bỏ qua và tính theo caller IP
        {
            using var reqSpoofed = new HttpRequestMessage(HttpMethod.Get, "/api/payment/public/sessions/fee?qr=valid-qr");
            reqSpoofed.Headers.Add("X-Test-Remote-Ip", untrustedCallerIp);
            reqSpoofed.Headers.Add("X-Forwarded-For", "10.0.0.99");
            using var resSpoofed = await client.SendAsync(reqSpoofed, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, resSpoofed.StatusCode);
        }
    }

    private static async Task<WebApplication> CreateTestAppAsync(int overridePermits)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            var trustedCidrs = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()
                ?? ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.1/8"];

            foreach (var cidr in trustedCidrs)
            {
                var parts = cidr.Split('/');
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var ip) && int.TryParse(parts[1], out var prefix))
                {
                    options.KnownNetworks.Add(new IPNetwork(ip, prefix));
                }
            }
        });
        builder.Services.AddPaymentAuthorization();
        builder.Services.AddPaymentRateLimiting(overridePermits: overridePermits);

        builder.Services.AddSingleton<ISender>(new DummySender());
        builder.Services.AddSingleton<IVnpayPaymentGateway>(new DummyVnpayGateway());
        builder.Services.AddSingleton<IParkingFeePaymentRepository>(new DummyPaymentRepository());

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Remote-Ip", out var remoteIpStr)
                && IPAddress.TryParse(remoteIpStr, out var ip))
            {
                context.Connection.RemoteIpAddress = ip;
            }
            return next();
        });
        app.UseForwardedHeaders();
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
