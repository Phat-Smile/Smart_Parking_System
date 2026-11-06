using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.Booking.Application.Reservations;

namespace ParkingSystem.Booking.WebAPI.Endpoints;

public static class ArrivalHeldQuotaEndpoints
{
    public static IEndpointRouteBuilder MapArrivalHeldQuotaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/booking/held-quota", async Task<IResult> (
            Guid lotId, DateTimeOffset arrivalAt,
            IConfiguration configuration, HttpContext httpContext, IArrivalHeldQuotaReader reader,
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

            return Results.Ok(new ArrivalHeldQuotaResponse(
                lotId, arrivalAt, await reader.CountAsync(lotId, arrivalAt, cancellationToken)));
        })
        .AllowAnonymous() // Internal machine call; validated by a dedicated key here and middleware if configured.
        .WithName("GetInternalArrivalHeldQuota")
        .ExcludeFromDescription();
        return app;
    }
}

public sealed record ArrivalHeldQuotaResponse(Guid LotId, DateTimeOffset ArrivalAt, int HeldQuota);
