using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Application.Cccd;
using ParkingSystem.Identity.Infrastructure.Cccd;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Cccd;

// TECH-19: CCCD is optional, so a verifier problem must never look like "your CCCD is wrong" and must never break the caller.
public sealed class CccdVerifierTests
{
    private const string KnownCccd = "000000000001";

    [Fact]
    public async Task Stub_recognises_a_known_test_cccd()
    {
        var result = await new StubCccdVerifier().VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Valid, result.Status);
        Assert.True(result.IsValid);
        Assert.Equal("TEST USER ONE", result.FullName);
        Assert.Equal(new DateOnly(2000, 1, 1), result.DateOfBirth);
    }

    [Fact]
    public async Task Stub_reports_an_unknown_cccd_as_invalid_not_unavailable()
    {
        var result = await new StubCccdVerifier().VerifyAsync("999999999999", TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Invalid, result.Status);
        Assert.False(result.IsValid);
        Assert.Null(result.FullName);
    }

    [Fact]
    public async Task Stub_honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StubCccdVerifier().VerifyAsync(KnownCccd, cts.Token));
    }

    [Fact]
    public async Task Http_verifier_posts_the_cccd_to_the_simulator_contract()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var verifier = Verifier(async request =>
        {
            seen = request;
            body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return Json(HttpStatusCode.OK, """{"valid":true,"fullName":"A","dob":"2001-03-15","gender":"M"}""");
        });

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/vneid/cccd/verify", seen.RequestUri!.AbsolutePath);
        Assert.Equal(KnownCccd, JsonDocument.Parse(body!).RootElement.GetProperty("cccd").GetString());
        Assert.Equal(CccdVerificationStatus.Valid, result.Status);
        Assert.Equal(new DateOnly(2001, 3, 15), result.DateOfBirth);
    }

    [Fact]
    public async Task Http_verifier_keeps_a_valid_result_when_the_birth_date_is_unreadable()
    {
        var verifier = Verifier(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"valid":true,"fullName":"A","dob":"not-a-date","gender":"F"}""")));

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Valid, result.Status);
        Assert.Null(result.DateOfBirth);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"valid":false}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"errorCode":"cccd.invalid_format"}""")]
    public async Task Http_verifier_treats_unknown_or_malformed_numbers_as_invalid(HttpStatusCode status, string payload)
    {
        var verifier = Verifier(_ => Task.FromResult(Json(status, payload)));

        var result = await verifier.VerifyAsync("123", TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Invalid, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Http_verifier_reports_a_failing_simulator_as_unavailable(HttpStatusCode status)
    {
        var verifier = Verifier(_ => Task.FromResult(Json(status, "{}")));

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Unavailable, result.Status);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Http_verifier_reports_a_network_failure_as_unavailable()
    {
        var verifier = Verifier(_ => throw new HttpRequestException("connection refused"));

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Http_verifier_reports_a_timeout_as_unavailable()
    {
        var verifier = Verifier(_ => throw new TaskCanceledException("HttpClient.Timeout elapsed"));

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Http_verifier_reports_a_malformed_response_as_unavailable()
    {
        var verifier = Verifier(_ => Task.FromResult(Json(HttpStatusCode.OK, "<html>oops</html>")));

        var result = await verifier.VerifyAsync(KnownCccd, TestContext.Current.CancellationToken);

        Assert.Equal(CccdVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Http_verifier_lets_the_callers_own_cancellation_through()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var verifier = Verifier(request => Task.FromCanceled<HttpResponseMessage>(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifier.VerifyAsync(KnownCccd, cts.Token));
    }

    [Fact]
    public void Stub_is_the_default_provider()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        Assert.IsType<StubCccdVerifier>(provider.GetRequiredService<ICccdVerifier>());
    }

    [Fact]
    public void Http_provider_requires_a_base_url()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["VneidApi:Provider"] = "Http", ["VneidApi:BaseUrl"] = "" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<VneidApiOptions>>().Value);
    }

    [Fact]
    public void Http_provider_with_a_base_url_uses_the_simulator_client()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["VneidApi:Provider"] = "Http", ["VneidApi:BaseUrl"] = "http://vneid-simulator:8000" });

        Assert.IsType<VneidCccdVerifier>(provider.GetRequiredService<ICccdVerifier>());
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCccdVerification(configuration);
        return services.BuildServiceProvider();
    }

    private static VneidCccdVerifier Verifier(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) =>
        new(new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("http://vneid-simulator:8000") });

    private static HttpResponseMessage Json(HttpStatusCode status, string payload) =>
        new(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
