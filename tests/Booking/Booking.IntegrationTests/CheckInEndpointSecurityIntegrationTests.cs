extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using ParkingSystem.Booking.Application;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Identity;
using Xunit;

using BookingReservationEndpoints = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints;
using BookingCheckInApiRequest = BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.CheckInReservationApiRequest;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class CheckInEndpointSecurityIntegrationTests
{
    private const string ExpectedArrivalKey = "test_arrival_secret_key_12345";
    private static readonly Guid LotId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid VehicleId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly string PlateNumber = "51A-123.45";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string SubjectId => UserId.ToString();
        public string? Email => "user@smartparking.vn";
        public string? Role => "Customer";
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim("sub", UserId.ToString()),
                new Claim(ClaimTypes.Role, "Customer")
            ], "TestScheme");

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void DiscardPendingChanges() { }
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);
        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => !status.HasValue || r.Status == status.Value).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId &&
                (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
            Guid lotId,
            string plateNumber,
            DateTimeOffset arrivalTime,
            CancellationToken cancellationToken)
        {
            var normalized = LicensePlateNormalizer.Normalize(plateNumber);
            var res = _list.FirstOrDefault(r =>
                r.LotId == lotId &&
                LicensePlateNormalizer.Normalize(r.PlateNumber) == normalized &&
                r.Status == ReservationStatus.Confirmed &&
                arrivalTime >= r.WindowStartAt.AddMinutes(-15) &&
                arrivalTime < Reservation.GetNoShowCutoff(r.AppointmentTime));
            return Task.FromResult(res);
        }

        public void Add(Reservation reservation) => _list.Add(reservation);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Reservation CreateConfirmedReservation(DateTimeOffset apptTime)
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            UserId,
            VehicleId,
            PlateNumber,
            LotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            apptTime,
            prepaidAmount: 50000m,
            createdAt: apptTime.AddHours(-2));

        res.Confirm(apptTime.AddHours(-2));
        return res;
    }

    private sealed class CheckInSender(IReservationRepository repo) : ISender
    {
        private readonly CheckInReservationCommandHandler _handler = new(repo);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CheckInReservationCommand command)
            {
                return (Task<TResponse>)(object)_handler.Handle(command, cancellationToken);
            }

            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new NotImplementedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (request is CheckInReservationCommand command)
            {
                return Task.FromResult<object?>(_handler.Handle(command, cancellationToken));
            }

            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private static async Task<(WebApplication App, FakeReservationRepository Repo)> CreateAppAsync(string? arrivalKeyConfig)
    {
        var inMemoryConfig = new Dictionary<string, string?>();
        if (arrivalKeyConfig is not null)
        {
            inMemoryConfig["ArrivalIntegration:Key"] = arrivalKeyConfig;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(inMemoryConfig);

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null);
        builder.Services.AddAuthorization();

        var repo = new FakeReservationRepository();
        builder.Services.AddSingleton<IReservationRepository>(repo);
        builder.Services.AddSingleton<ISender>(new CheckInSender(repo));
        builder.Services.AddScoped<ICurrentUser, FakeCurrentUser>();

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        BookingReservationEndpoints.MapReservationEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        return (app, repo);
    }

    [Fact]
    public async Task Old_public_route_returns_404_and_does_not_change_data()
    {
        await using var context = (await CreateAppAsync(ExpectedArrivalKey)).App;
        var client = context.GetTestClient();
        var repo = context.Services.GetRequiredService<IReservationRepository>() as FakeReservationRepository;

        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        repo!.Add(reservation);

        var payload = new BookingCheckInApiRequest(
            LotId,
            PlateNumber,
            apptTime,
            Guid.NewGuid());

        // Call old public route
        var response = await client.PostAsJsonAsync("/api/booking/reservations/check-in", payload, TestContext.Current.CancellationToken);

        // Must return 404 (or 401) because route is removed from /api/booking group
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 404 or 401, but got {response.StatusCode}");

        // Invariant: Data is NOT changed
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Null(reservation.SessionId);
    }

    [Fact]
    public async Task Internal_route_without_key_returns_403()
    {
        await using var context = (await CreateAppAsync(ExpectedArrivalKey)).App;
        var client = context.GetTestClient();
        var repo = context.Services.GetRequiredService<IReservationRepository>() as FakeReservationRepository;

        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        repo!.Add(reservation);

        var payload = new BookingCheckInApiRequest(
            LotId,
            PlateNumber,
            apptTime,
            Guid.NewGuid());

        // POST without X-Arrival-Service-Key
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/check-in")
        {
            Content = JsonContent.Create(payload)
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Null(reservation.SessionId);
    }

    [Fact]
    public async Task Internal_route_with_wrong_key_returns_403()
    {
        await using var context = (await CreateAppAsync(ExpectedArrivalKey)).App;
        var client = context.GetTestClient();
        var repo = context.Services.GetRequiredService<IReservationRepository>() as FakeReservationRepository;

        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        repo!.Add(reservation);

        var payload = new BookingCheckInApiRequest(
            LotId,
            PlateNumber,
            apptTime,
            Guid.NewGuid());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/check-in")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Arrival-Service-Key", "invalid_secret_key_999");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Null(reservation.SessionId);
    }

    [Fact]
    public async Task Internal_route_with_config_missing_returns_503()
    {
        // Null arrivalKeyConfig -> ArrivalIntegration:Key is missing
        await using var context = (await CreateAppAsync(arrivalKeyConfig: null)).App;
        var client = context.GetTestClient();
        var repo = context.Services.GetRequiredService<IReservationRepository>() as FakeReservationRepository;

        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        repo!.Add(reservation);

        var payload = new BookingCheckInApiRequest(
            LotId,
            PlateNumber,
            apptTime,
            Guid.NewGuid());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/check-in")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Arrival-Service-Key", "some_key");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Null(reservation.SessionId);
    }

    [Fact]
    public async Task Internal_route_with_correct_key_returns_200_and_reservation_becomes_Used()
    {
        await using var context = (await CreateAppAsync(ExpectedArrivalKey)).App;
        var client = context.GetTestClient();
        var repo = context.Services.GetRequiredService<IReservationRepository>() as FakeReservationRepository;

        var apptTime = DateTimeOffset.UtcNow;
        var reservation = CreateConfirmedReservation(apptTime);
        repo!.Add(reservation);

        var sessionId = Guid.NewGuid();
        var payload = new BookingCheckInApiRequest(
            LotId,
            PlateNumber,
            apptTime,
            sessionId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/booking/reservations/check-in")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Arrival-Service-Key", ExpectedArrivalKey);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<CheckInReservationResult>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(content);
        Assert.True(content.HasReservation);
        Assert.Equal(reservation.Id, content.ReservationId);
        Assert.Equal(UserId, content.UserId);
        Assert.Equal(VehicleId, content.VehicleId);

        // Domain Invariant: reservation transitions to Used and records SessionId
        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(sessionId, reservation.SessionId);
    }

    [Fact]
    public void Gateway_route_table_does_not_contain_internal_routes()
    {
        var root = FindRepositoryRoot();
        var gatewayConfigPath = Path.Combine(root, "src", "Gateway", "appsettings.json");
        Assert.True(File.Exists(gatewayConfigPath), $"Gateway appsettings not found at {gatewayConfigPath}");

        var json = File.ReadAllText(gatewayConfigPath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("ReverseProxy", out var reverseProxy) &&
            reverseProxy.TryGetProperty("Routes", out var routes))
        {
            foreach (var routeProp in routes.EnumerateObject())
            {
                var routeName = routeProp.Name;
                var routeObj = routeProp.Value;
                if (routeObj.TryGetProperty("Match", out var match) &&
                    match.TryGetProperty("Path", out var pathElement))
                {
                    var path = pathElement.GetString() ?? string.Empty;
                    Assert.DoesNotContain("/internal", path, StringComparison.OrdinalIgnoreCase);
                    Assert.False(path.StartsWith("/internal", StringComparison.OrdinalIgnoreCase),
                        $"Gateway route '{routeName}' exposes internal path '{path}'");
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ParkingSystem.slnx")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate ParkingSystem.slnx from the test working directory.");
    }
}
