using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

public static class VnpayEndpoints
{
    public static IEndpointRouteBuilder MapVnpayEndpoints(this IEndpointRouteBuilder app)
    {
        // Tiếp nhận kết quả thanh toán từ VNPay IPN webhook
        app.MapGet("/api/payment/vnpay/ipn", async (
            HttpRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var queryParams = request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
            var command = new ProcessVnpayIpnCommand(queryParams);
            var response = await sender.Send(command, cancellationToken);
            return Results.Ok(response);
        })
        .AllowAnonymous()
        .WithName("ProcessVnpayIpn")
        .WithSummary("Tiếp nhận kết quả IPN từ VNPay")
        .Produces<VnpayIpnResponseDto>(StatusCodes.Status200OK);

        return app;
    }
}
