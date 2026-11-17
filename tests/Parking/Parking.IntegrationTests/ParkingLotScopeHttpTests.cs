using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// TECH-12 ở tầng HTTP: role trong token quyết định vào được endpoint, phân công quyết định thao tác được trên bãi nào.
public sealed class ParkingLotScopeHttpTests
{
    private const string UserId = "11111111-1111-1111-1111-111111111111";
    private const string RoleHeader = "X-Test-Role";
    private static readonly string AssignmentRoute = $"/api/parking/admin/lots/{Guid.NewGuid()}/assignments/{Guid.NewGuid()}";

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("Staff", HttpStatusCode.Forbidden)]
    [InlineData("ParkingLotOwner", HttpStatusCode.Forbidden)]
    public async Task Only_a_system_admin_can_assign_or_remove_lot_members(string? role, HttpStatusCode expected)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        using var put = await SendAsync(client, HttpMethod.Put, AssignmentRoute, role, new { role = "Staff" });
        using var delete = await SendAsync(client, HttpMethod.Delete, AssignmentRoute, role, body: null);

        Assert.Equal(expected, put.StatusCode);
        Assert.Equal(expected, delete.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("Staff", HttpStatusCode.Forbidden)]
    public async Task Pricing_endpoint_rejects_unauthenticated_customer_and_staff(string? role, HttpStatusCode expected)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var pricingRoute = $"/api/parking/lots/{Guid.NewGuid()}/pricing/Car";

        using var put = await SendAsync(client, HttpMethod.Put, pricingRoute, role, new
        {
            billingMode = "Block",
            blockMinutes = 30,
            price = 10000m,
            overnightFee = 50000m
        });

        Assert.Equal(expected, put.StatusCode);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Staff_or_owner_not_assigned_to_the_lot_get_403_lot_access_denied(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app.GetTestClient(), HttpMethod.Post, "/api/parking/sessions", role,
            new { lotId = Guid.NewGuid(), plateNumber = "51A-123.45", vehicleType = "Car" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ParkingLotAccessErrors.AccessDenied.Code, problem.GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Scanning_a_session_qr_of_a_lot_the_caller_is_not_assigned_to_returns_403(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app.GetTestClient(), HttpMethod.Get, "/api/parking/sessions/by-qr/qr-of-another-lot", role, body: null);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ParkingLotAccessErrors.AccessDenied.Code, problem.GetProperty("errorCode").GetString());
    }

    // US-STAFF-004: tìm phiên theo biển số, cấp lại QR và xác minh tay đều chặn người không được gắn với bãi.
    [Theory]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Exit_verification_endpoints_return_403_for_a_lot_the_caller_is_not_assigned_to(string role)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var sessionRoute = $"/api/parking/sessions/{Guid.NewGuid()}";

        using var findByPlate = await SendAsync(client, HttpMethod.Get, $"/api/parking/sessions/open?lotId={Guid.NewGuid()}&plateNumber=51A-123.45", role, body: null);
        using var reissue = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/reissue-qr", role, new { reason = "Customer lost the QR" });
        using var verify = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/manual-verify", role, new { reason = "Plate misread at the exit gate" });

        foreach (var response in new[] { findByPlate, reissue, verify })
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ParkingLotAccessErrors.AccessDenied.Code, problem.GetProperty("errorCode").GetString());
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task Exit_verification_endpoints_are_for_staff_and_owner_only(string? role, HttpStatusCode expected)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var sessionRoute = $"/api/parking/sessions/{Guid.NewGuid()}";

        using var findByPlate = await SendAsync(client, HttpMethod.Get, $"/api/parking/sessions/open?lotId={Guid.NewGuid()}&plateNumber=51A-123.45", role, body: null);
        using var reissue = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/reissue-qr", role, new { reason = "Customer lost the QR" });
        using var verify = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/manual-verify", role, new { reason = "Plate misread at the exit gate" });

        Assert.Equal(expected, findByPlate.StatusCode);
        Assert.Equal(expected, reissue.StatusCode);
        Assert.Equal(expected, verify.StatusCode);
    }

    [Fact]
    public async Task Reissue_and_manual_verify_without_a_reason_return_400()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var sessionRoute = $"/api/parking/sessions/{Guid.NewGuid()}";

        using var reissue = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/reissue-qr", "Staff", new { reason = "  " });
        using var verify = await SendAsync(client, HttpMethod.Post, $"{sessionRoute}/manual-verify", "Staff", new { });

        Assert.Equal(HttpStatusCode.BadRequest, reissue.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string route, string? role, object? body)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddApplicationServices();
        builder.Services.AddSingleton<IBookingCheckInClient, NoMatchingBookingCheckInClient>();
        builder.Services.AddSingleton<IBookingNoSlotReporter, NoMatchingBookingCheckInClient>();
        builder.Services.AddParkingAuthorization();
        builder.Services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(HeaderRoleAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, FixedCurrentUser>();
        // Bãi tồn tại và đã duyệt, nhưng người gọi không được gắn với bãi nào.
        builder.Services.AddSingleton<IParkingLotDetailReader, ApprovedLotReader>();
        builder.Services.AddSingleton<IParkingLotAccess, NoLotAccess>();
        builder.Services.AddSingleton<IParkingSessionRepository, UnusedSessionRepository>();
        builder.Services.AddSingleton<IPlateOwnerLookup, NoPlateOwner>();
        builder.Services.AddSingleton<IClock, ParkingSystem.BuildingBlocks.Time.SystemClock>();
        builder.Services.AddSingleton<IMotorbikeZoneCounter, UnusedMotorbikeZones>();
        builder.Services.AddSingleton<ISlotDesignator, UnusedSlotDesignator>();
        builder.Services.AddSingleton<IAvailableCarSlotCounter, UnusedAvailableSlots>();
        builder.Services.AddSingleton<IIncidentRepository, UnusedIncidents>();
        builder.Services.AddSingleton<IHeldQuotaReader, UnusedHeldQuotaReader>();
        builder.Services.AddSingleton<IZoneLabelReader, UnusedZoneLabels>();
        builder.Services.AddSingleton<IParkingUnitOfWork, UnusedUnitOfWork>();
        builder.Services.AddSingleton<IPricingPolicyRepository, UnusedPricingPolicyRepository>();
        builder.Services.AddSingleton<IAuditWriter, UnusedAuditWriter>();
        builder.Services.AddSingleton<IAuditLog, UnusedAuditLog>();
        builder.Services.AddSingleton<ILotOwnershipService, UnusedLotOwnershipService>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingSessionEndpoints();
        app.MapParkingLotAssignmentEndpoints();
        app.MapPricingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    // Không có header thì coi như chưa đăng nhập; có header thì role lấy từ header.
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
                [new Claim("sub", UserId), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public string? SubjectId => UserId;

        public bool IsAuthenticated => true;
    }

    private sealed class ApprovedLotReader : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingLotDetail?>(new ParkingLotDetail(parkingLotId, "Lot A", "1 Test Street", 10.77m, 106.70m, "Flat"));
    }

    private sealed class NoLotAccess : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }

    // Các thao tác ghi không được gọi tới: request bị chặn ở bước kiểm quyền theo bãi. Tra QR hoặc theo id thì trả về một phiên của bãi khác.
    private sealed class UnusedSessionRepository : IParkingSessionRepository
    {
        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(ParkingSession.Open(sessionId, Guid.NewGuid(), "51A-123.45", VehicleType.Car, DateTimeOffset.UtcNow, "qr-of-another-lot", null, null));
        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(ParkingSession.Open(Guid.NewGuid(), Guid.NewGuid(), "51A-123.45", VehicleType.Car, DateTimeOffset.UtcNow, qrToken, null, null));
        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) => FindByIdAsync(sessionId, cancellationToken);

        public void Add(ParkingSession session) => throw new InvalidOperationException();
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    // Không được gọi tới: request bị chặn ở bước kiểm quyền theo bãi, trước khi đụng tới chỗ xe máy.
    // Không được gọi tới: request bị chặn ở bước kiểm quyền theo bãi, trước khi hỏi Booking hay chỉ định chỗ ô tô (US-PARK-007).
    private sealed class UnusedSlotDesignator : ISlotDesignator
    {
        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedAvailableSlots : IAvailableCarSlotCounter
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedIncidents : IIncidentRepository
    {
        public void Add(Incident incident) => throw new InvalidOperationException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();

        public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) => throw new InvalidOperationException();

        public Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken) =>
            throw new InvalidOperationException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedHeldQuotaReader : IHeldQuotaReader
    {
        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedZoneLabels : IZoneLabelReader
    {
        public Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedMotorbikeZones : IMotorbikeZoneCounter
    {
        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedUnitOfWork : IParkingUnitOfWork
    {
        public Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnusedPricingPolicyRepository : IPricingPolicyRepository
    {
        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) => Task.FromResult<PricingPolicy?>(null);
        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class UnusedAuditWriter : IAuditWriter
    {
        public void Log(Guid actorId, string action, string targetId, string details) { }
    }

    // Không được gọi tới: request bị chặn ở bước kiểm quyền theo bãi, nên không có gì để ghi audit.
    private sealed class UnusedAuditLog : IAuditLog
    {
        public void Record(AuditEntry entry) => throw new InvalidOperationException();
    }

    private sealed class UnusedLotOwnershipService : ILotOwnershipService
    {
        public Task<bool> LotExistsAsync(Guid lotId) => Task.FromResult(true);
        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) => Task.FromResult(false);
        public Task<bool> IsLotApprovedAsync(Guid lotId) => Task.FromResult(true);
    }
}
