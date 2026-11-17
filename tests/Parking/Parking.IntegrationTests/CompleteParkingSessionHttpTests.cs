using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
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
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PAY-006 at the HTTP layer: route, roles and how each business error maps to a status code.
// The handler is replaced here; ExitPaymentIntegrationTests covers it on PostgreSQL.
public sealed class CompleteParkingSessionHttpTests
{
    private const string CallerId = "11111111-1111-1111-1111-111111111111";
    private const string RoleHeader = "X-Test-Role";
    private static readonly Guid SessionId = Guid.Parse("2b684220-8663-48a3-aed6-a1ef40748c3a");
    private static readonly string Route = $"/api/parking/sessions/{SessionId}/complete";

    [Fact]
    public async Task Returns_401_without_a_token()
    {
        await using var app = await CreateAppAsync(Result<CompletedParkingSession>.Success(Completed()));

        using var response = await PostAsync(app, role: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Returns_403_for_a_customer()
    {
        await using var app = await CreateAppAsync(Result<CompletedParkingSession>.Success(Completed()));

        using var response = await PostAsync(app, "Customer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Returns_200_with_the_completed_session_for_staff()
    {
        await using var app = await CreateAppAsync(Result<CompletedParkingSession>.Success(Completed()));

        using var response = await PostAsync(app, "Staff");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionId, body.GetProperty("sessionId").GetGuid());
        Assert.Equal("Completed", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("completedAt", out _));
    }

    public static TheoryData<string, int> BusinessErrors => new()
    {
        { CompleteParkingSessionErrors.SessionNotFound.Code, 404 },
        { ParkingLotAccessErrors.AccessDenied.Code, 403 },
        { CompleteParkingSessionErrors.SessionNotOpen.Code, 409 },
        { CompleteParkingSessionErrors.ExitNotVerified.Code, 409 },
        { CompleteParkingSessionErrors.PaymentRequired.Code, 409 },
        { CompleteParkingSessionErrors.ExitWindowExpired.Code, 409 }
    };

    [Theory]
    [MemberData(nameof(BusinessErrors))]
    public async Task Business_errors_keep_their_code_and_status(string errorCode, int expectedStatus)
    {
        Error[] errors =
        [
            CompleteParkingSessionErrors.SessionNotFound,
            ParkingLotAccessErrors.AccessDenied,
            CompleteParkingSessionErrors.SessionNotOpen,
            CompleteParkingSessionErrors.ExitNotVerified,
            CompleteParkingSessionErrors.PaymentRequired,
            CompleteParkingSessionErrors.ExitWindowExpired
        ];
        await using var app = await CreateAppAsync(Result<CompletedParkingSession>.Failure(errors.Single(error => error.Code == errorCode)));

        using var response = await PostAsync(app, "Staff");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Passes_the_staff_reason_to_the_command()
    {
        // US-STAFF-004: staff checked a misread plate in person and send the reason with complete.
        var sender = new FixedSender(Result<CompletedParkingSession>.Success(Completed()));
        await using var app = await CreateAppAsync(sender);

        using var response = await PostAsync(app, "Staff", manualVerificationReason: "OCR misread");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<CompleteParkingSessionCommand>(sender.LastRequest);
        Assert.Equal("OCR misread", command.ManualVerificationReason);
    }

    [Fact]
    public async Task Without_a_reason_the_command_has_none()
    {
        var sender = new FixedSender(Result<CompletedParkingSession>.Success(Completed()));
        await using var app = await CreateAppAsync(sender);

        using var response = await PostAsync(app, "Staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.IsType<CompleteParkingSessionCommand>(sender.LastRequest).ManualVerificationReason);
    }

    private static CompletedParkingSession Completed() => new(SessionId, "Completed", DateTimeOffset.UtcNow);

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, string? role, string? manualVerificationReason = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(new { plateNumber = "51A-123.45", qrToken = "session-qr", manualVerificationReason })
        };
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<WebApplication> CreateAppAsync(Result<CompletedParkingSession> result) =>
        CreateAppAsync(new FixedSender(result));

    private static async Task<WebApplication> CreateAppAsync(FixedSender sender)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddParkingAuthorization();
        builder.Services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(HeaderRoleAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, FixedCurrentUser>();
        builder.Services.AddSingleton<ISender>(sender);

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingSessionEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class HeaderRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeaderRole";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", CallerId), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public string? SubjectId => CallerId;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedSender(Result<CompletedParkingSession> result) : ISender
    {
        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)(object)result);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
