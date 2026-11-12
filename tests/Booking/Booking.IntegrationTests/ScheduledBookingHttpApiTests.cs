extern alias BookingWebApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
using ParkingSystem.Booking.Application;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ScheduledBookingHttpApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestUserId = Guid.NewGuid();

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string SubjectId => TestUserId.ToString();
        public string? Email => "test@smartparking.vn";
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
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim("sub", TestUserId.ToString()),
                new Claim(ClaimTypes.Role, "Customer")
            ], "TestScheme");

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakeVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(string rawOrCanonicalPlate, Guid expectedUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(expectedUserId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
        }
    }

    private sealed class FakeParkingLotCapacityProvider : IParkingLotCapacityProvider
    {
        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 100, 10, true, 0m)));
    }

    private sealed class FakeCoordinator : IReservationCapacityCoordinator
    {
        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation, int ceiling, int? availableSlotCount, TimeSpan lockTimeout, int maxActiveReservationsPerUser = 3, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<Reservation>.Success(reservation));
    }

    private async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = false;
            options.ValidateOnBuild = false;
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null);
        builder.Services.AddAuthorization();
        builder.Services.AddApplicationServices(new BookingCapacityOptions());

        builder.Services.AddSingleton<IClock, FakeClock>();
        builder.Services.AddScoped<ICurrentUser, FakeCurrentUser>();
        builder.Services.AddSingleton<IVehicleOwnershipClient, FakeVehicleOwnershipClient>();
        builder.Services.AddSingleton<IParkingLotCapacityProvider, FakeParkingLotCapacityProvider>();
        builder.Services.AddSingleton<IParkingPricingProvider>(new UnconfiguredParkingPricingProvider());
        builder.Services.AddSingleton<IReservationCapacityCoordinator, FakeCoordinator>();

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        BookingWebApi::ParkingSystem.Booking.WebAPI.Endpoints.ReservationEndpoints.MapReservationEndpoints(app);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Post_Holds_Invalid_Time_Window_Returns_400_Bad_Request()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Duration = 1500 (> 1440 max 24h limit) -> 400 Bad Request
        var payload = new
        {
            userId = TestUserId,
            vehicleId = Guid.NewGuid(),
            plateNumber = "51A-123.45",
            lotId = Guid.NewGuid(),
            vehicleGroup = "Car",
            reservationType = "Scheduled",
            appointmentTime = Now.AddHours(2),
            durationMinutes = 1500,
            prepaidAmount = 50000m
        };

        var response = await client.PostAsJsonAsync("/api/booking/holds", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Holds_Pricing_Unavailable_Returns_503_Service_Unavailable()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Valid time window (60 min), but UnconfiguredParkingPricingProvider returns CapacityInfoUnavailable -> 503
        var payload = new
        {
            userId = TestUserId,
            vehicleId = Guid.NewGuid(),
            plateNumber = "51A-123.45",
            lotId = Guid.NewGuid(),
            vehicleGroup = "Car",
            reservationType = "Scheduled",
            appointmentTime = Now.AddHours(2),
            durationMinutes = 60,
            prepaidAmount = 50000m
        };

        var response = await client.PostAsJsonAsync("/api/booking/holds", payload);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
