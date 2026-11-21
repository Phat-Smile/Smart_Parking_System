using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class AdminParkingEndpointsTests
{
    [Theory]
    [InlineData("?fromDate=not-a-date")]
    [InlineData("?fromDate=2026-11-21&toDate=2026-11-20")]
    public async Task Invalid_admin_date_filters_return_validation_problem(string query)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderRoleAuthenticationHandler.RoleHeader, ParkingAuthorization.SystemAdminRole);
        using var response = await client.GetAsync("/api/parking/admin/lots" + query,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Admin_date_filters_convert_full_vietnam_days_to_utc_boundaries()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderRoleAuthenticationHandler.RoleHeader, ParkingAuthorization.SystemAdminRole);

        using var response = await client.GetAsync(
            "/api/parking/admin/lots?fromDate=2026-11-01&toDate=2026-11-20",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reader = (EmptyAdminParkingLotReader)app.Services.GetRequiredService<IAdminParkingLotReader>();
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime(),
            reader.LastFromDate);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime(),
            reader.LastToDateExclusive);
    }

    [Fact]
    public async Task Invalid_analytics_period_returns_400_problem_details()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderRoleAuthenticationHandler.RoleHeader, ParkingAuthorization.SystemAdminRole);

        using var response = await client.GetAsync("/api/parking/admin/analytics/trends?period=90days",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Omitted_period_returns_seven_daily_entries()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderRoleAuthenticationHandler.RoleHeader, ParkingAuthorization.SystemAdminRole);

        using var response = await client.GetAsync("/api/parking/admin/analytics/trends",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(7, body.GetProperty("labels").GetArrayLength());
        Assert.Equal(7, body.GetProperty("newSubmissions").GetArrayLength());
        Assert.Equal(7, body.GetProperty("approvedCount").GetArrayLength());
    }

    [Fact]
    public async Task Non_admin_cannot_access_analytics_trends()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderRoleAuthenticationHandler.RoleHeader, ParkingAuthorization.ParkingLotOwnerRole);

        using var response = await client.GetAsync("/api/parking/admin/analytics/trends",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddApplicationServices();
        builder.Services.AddParkingAuthorization();
        builder.Services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(
                HeaderRoleAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddSingleton<IAdminParkingLotReader, EmptyAdminParkingLotReader>();
        builder.Services.AddSingleton<IAdminParkingAnalyticsReader, EmptyAdminParkingAnalyticsReader>();
        builder.Services.AddSingleton<IClock, ParkingSystem.BuildingBlocks.Time.SystemClock>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAdminParkingLotEndpoints();
        app.MapAdminParkingAnalyticsEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class EmptyAdminParkingLotReader : IAdminParkingLotReader
    {
        public DateTimeOffset? LastFromDate { get; private set; }
        public DateTimeOffset? LastToDateExclusive { get; private set; }

        public Task<PagedResult<AdminParkingLotItem>> ListForAdminAsync(
            ParkingLotApprovalStatus? status,
            DateTimeOffset? fromDate,
            DateTimeOffset? toDateExclusive,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastFromDate = fromDate;
            LastToDateExclusive = toDateExclusive;
            return Task.FromResult(new PagedResult<AdminParkingLotItem>([], 0, page, pageSize));
        }
    }

    private sealed class EmptyAdminParkingAnalyticsReader : IAdminParkingAnalyticsReader
    {
        public Task<AdminParkingAnalyticsCounts> GetCountsAsync(
            DateTime startUtc, DateTime endUtcExclusive, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminParkingAnalyticsCounts([], []));
    }

    private sealed class HeaderRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AdminParkingTest";
        public const string RoleHeader = "X-Test-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new(ParkingAuthorization.RoleClaimType, role.ToString()) };
            if (role.ToString() == ParkingAuthorization.SystemAdminRole)
                claims.Add(new(ParkingAuthorization.AuthenticationMethodsClaimType, ParkingAuthorization.MfaMethod));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
