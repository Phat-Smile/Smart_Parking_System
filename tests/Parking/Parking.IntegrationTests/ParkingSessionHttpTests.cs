using System.Net;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Tester report 2026-10-08 (API-001, DOC-004): a body that cannot be read must return 400 or 415, never 500.
// These requests fail while binding the body, before the handler runs, so no database is needed.
public sealed class ParkingSessionHttpTests
{
    private const string StaffId = "11111111-1111-1111-1111-111111111111";
    private const string SessionsRoute = "/api/parking/sessions";
    private const string CancelRoute = SessionsRoute + "/2b684220-8663-48a3-aed6-a1ef40748c3a/cancel";

    [Theory]
    [InlineData("Development", SessionsRoute, "{\"lotId\":")]
    [InlineData("Production", SessionsRoute, "{\"lotId\":")]
    [InlineData("Production", SessionsRoute, "{\"lotId\":123,\"plateNumber\":\"51A-123.45\",\"vehicleType\":\"Car\"}")]
    [InlineData("Production", SessionsRoute, "")]
    [InlineData("Production", CancelRoute, "{\"reason\":")]
    public async Task Unreadable_json_body_returns_400_request_malformed_in_every_environment(string environment, string route, string body)
    {
        await using var app = await CreateAppAsync(environment);

        using var response = await app.GetTestClient().PostAsync(
            route, new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("request.malformed", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Theory]
    [InlineData(SessionsRoute)]
    [InlineData(CancelRoute)]
    public async Task Non_json_content_type_returns_415(string route)
    {
        await using var app = await CreateAppAsync();

        using var response = await app.GetTestClient().PostAsync(
            route, new StringContent("hello", Encoding.UTF8, "text/plain"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Open_session_binds_incident_id_but_not_untrusted_timestamp()
    {
        var capture = new CaptureOpenCommand();
        await using var app = await CreateAppAsync(capture: capture);
        var incidentId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        using var response = await app.GetTestClient().PostAsJsonAsync(SessionsRoute, new
        {
            lotId, plateNumber = "51A-123.45", vehicleType = "Car", gateReadingIncidentId = incidentId,
            physicalDetectionTime = "2000-01-01T00:00:00Z", entryTime = "2000-01-01T00:00:00Z"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(incidentId, capture.Command!.GateReadingIncidentId);
        Assert.Equal(lotId, capture.Command.LotId);
        Assert.Equal(Guid.Parse(StaffId), capture.Command.ActorUserId);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(OpenParkingSessionErrors.InvalidGateReading.Code, problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Open_session_without_incident_id_still_binds_legacy_contract()
    {
        var capture = new CaptureOpenCommand();
        await using var app = await CreateAppAsync(capture: capture);
        using var response = await app.GetTestClient().PostAsJsonAsync(SessionsRoute, new
        {
            lotId = Guid.NewGuid(), plateNumber = "51A-123.45", vehicleType = "Car",
            physicalDetectionTime = "2000-01-01T00:00:00Z"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(capture.Command!.GateReadingIncidentId);
    }

    private sealed class CaptureOpenCommand : IRequestHandler<OpenParkingSessionCommand, Result<OpenedParkingSession>>
    {
        public OpenParkingSessionCommand? Command { get; private set; }
        public Task<Result<OpenedParkingSession>> Handle(OpenParkingSessionCommand request, CancellationToken cancellationToken)
        {
            Command = request;
            return Task.FromResult(Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.InvalidGateReading));
        }
    }

    private static async Task<WebApplication> CreateAppAsync(string environment = "Production", CaptureOpenCommand? capture = null)
    {
        // The tester ran Development; production must give the same errorCode (PR #31 review).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        // The repositories are not registered because these requests never reach a handler.
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddApplicationServices();
        if (capture is not null)
            builder.Services.AddSingleton<IRequestHandler<OpenParkingSessionCommand, Result<OpenedParkingSession>>>(capture);
        builder.Services.AddParkingAuthorization();
        // A fixed Staff identity replaces JWT validation; authorization itself still runs.
        builder.Services.AddAuthentication(StaffAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, StaffAuthenticationHandler>(StaffAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, StaffCurrentUser>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingSessionEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class StaffAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestStaff";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim("sub", StaffId), new Claim(ParkingAuthorization.RoleClaimType, ParkingAuthorization.StaffRole)],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class StaffCurrentUser : ICurrentUser
    {
        public string? SubjectId => StaffId;

        public bool IsAuthenticated => true;
    }
}
