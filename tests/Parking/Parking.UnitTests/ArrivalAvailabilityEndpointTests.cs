using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Endpoints;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ArrivalAvailabilityEndpointTests
{
    private sealed class StubLotReader : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingLotDetail?>(null);
    }

    private sealed class StubSnapshotReader : IArrivalSnapshotReader
    {
        public Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
            Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArrivalAvailabilitySnapshot>>([]);
    }

    [Fact]
    public async Task Invalid_query_parameters_returns_rfc7807_problem_details()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddSingleton<IParkingLotDetailReader, StubLotReader>();
        builder.Services.AddSingleton<IArrivalSnapshotReader, StubSnapshotReader>();
        builder.Services.AddHttpClient<ArrivalAvailabilityClient>();

        using var app = builder.Build();
        app.MapArrivalAvailabilityEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync(
            $"/api/parking/lots/{Guid.NewGuid()}/arrival-availability?vehicleType=InvalidType",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("about:blank", problem.GetProperty("type").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Invalid lotId, arrivalAt or vehicleType.", problem.GetProperty("title").GetString());
        Assert.Equal("parking.arrival_availability.invalid_parameters", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.TryGetProperty("instance", out var instance) && instance.GetString()!.Contains("arrival-availability"));
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }
}
