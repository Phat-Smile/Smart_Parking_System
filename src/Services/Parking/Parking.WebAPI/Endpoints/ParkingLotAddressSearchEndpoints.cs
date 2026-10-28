using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.SearchParkingLotsByAddress;
using ParkingSystem.Parking.WebAPI.Geocoding;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotAddressSearchEndpoints
{
    public static IEndpointRouteBuilder MapParkingLotAddressSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/lots/by-address", async (
                string? address,
                double? radiusKm,
                int? limit,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await sender.Send(new SearchParkingLotsByAddressQuery(
                        address ?? string.Empty,
                        radiusKm ?? 5,
                        limit ?? 20), cancellationToken);

                    return result is null
                        ? (IResult)Results.NotFound(new
                        {
                            errorCode = "geocoding.destination_not_found",
                            message = "Không tìm thấy địa chỉ. Hãy thử từ khóa khác hoặc chọn điểm trên bản đồ."
                        })
                        : Results.Ok(result);
                }
                catch (GeocodingRateLimitedException)
                {
                    return Results.Problem(
                        title: "Dịch vụ tìm địa chỉ đang bận.",
                        detail: "Vui lòng đợi rồi thử lại.",
                        statusCode: StatusCodes.Status429TooManyRequests);
                }
                catch (GeocodingProviderUnavailableException)
                {
                    return Results.Problem(
                        title: "Dịch vụ tìm địa chỉ tạm thời không khả dụng.",
                        detail: "Vui lòng thử lại hoặc chọn điểm trên bản đồ.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .AllowAnonymous()
            .WithName("SearchParkingLotsByAddress")
            .WithSummary("Find approved parking lots near a submitted destination address")
            .WithDescription("Geocodes on explicit user submission; not an autocomplete endpoint. Nominatim results are cached and upstream requests are limited to one per second per API process.")
            .Produces<AddressParkingSearchResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
