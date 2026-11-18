using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.WebAPI.Endpoints;

public static class ArrivalHeldQuotaEndpoints
{
    public const string DefaultVehicleGroup = HeldQuotaVehicleGroupParser.DefaultVehicleGroup;
    public const string MotorbikeVehicleGroup = HeldQuotaVehicleGroupParser.MotorbikeVehicleGroup;

    public static IEndpointRouteBuilder MapArrivalHeldQuotaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/booking/held-quota", async Task<IResult> (
            Guid lotId,
            DateTimeOffset arrivalAt,
            string? vehicleGroup,
            IConfiguration configuration,
            HttpContext httpContext,
            IArrivalHeldQuotaReader reader,
            CancellationToken cancellationToken) =>
        {
            // Dedicated key is mandatory even when global ServiceAuth is disabled in development.
            // Existing ServiceKeyValidationMiddleware independently validates X-Service-Key if configured.
            var requiredKey = configuration["ArrivalIntegration:Key"];
            if (string.IsNullOrWhiteSpace(requiredKey))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var receivedKey = httpContext.Request.Headers["X-Arrival-Service-Key"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(requiredKey), Encoding.UTF8.GetBytes(receivedKey)))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (lotId == Guid.Empty || arrivalAt == default ||
                arrivalAt <= DateTimeOffset.UtcNow.AddMinutes(-1) ||
                arrivalAt > DateTimeOffset.UtcNow.AddDays(30))
                return Results.BadRequest();

            if (!HeldQuotaVehicleGroupParser.TryParse(vehicleGroup, out var resolvedGroup))
            {
                return Results.BadRequest();
            }

            if (string.Equals(resolvedGroup, nameof(VehicleGroup.Motorbike), StringComparison.Ordinal))
            {
                return Results.Ok(new ArrivalHeldQuotaResponse(lotId, arrivalAt, resolvedGroup, 0));
            }

            var count = await reader.CountAsync(lotId, arrivalAt, cancellationToken);
            return Results.Ok(new ArrivalHeldQuotaResponse(lotId, arrivalAt, resolvedGroup, count));
        })
        .AllowAnonymous() // Internal machine call; validated by a dedicated key here and middleware if configured.
        .WithName("GetInternalArrivalHeldQuota")
        .ExcludeFromDescription();
        return app;
    }

    public static bool TryResolveVehicleGroup(string? input, out string resolvedGroup) =>
        HeldQuotaVehicleGroupParser.TryParse(input, out resolvedGroup);
}

public sealed record ArrivalHeldQuotaResponse(
    Guid LotId,
    DateTimeOffset ArrivalAt,
    string VehicleGroup,
    int HeldQuota);
