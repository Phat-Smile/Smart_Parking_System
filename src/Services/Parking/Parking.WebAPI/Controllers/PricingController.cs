using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;
using System;
using System.Threading.Tasks;

namespace ParkingSystem.Parking.WebAPI.Controllers;

[ApiController]
[Route("api/v1/parking-lots/{lotId:guid}/pricing")]
public class PricingController : ControllerBase
{
    private readonly ISender _sender;

    public PricingController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> ConfigurePricing(Guid lotId, [FromBody] ConfigurePricingRequest request)
    {
        // Trong thực tế, CallerId sẽ lấy từ JWT token (User.Identity.Name)
        // Hiện tại gán tạm một Guid giả định để test
        var callerId = Guid.NewGuid();

        var command = new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = request.VehicleType,
            BillingMode = request.BillingMode,
            BlockMinutes = request.BlockMinutes,
            Price = request.Price,
            OvernightFee = request.OvernightFee,
            ExpectedVersion = request.ExpectedVersion,
            Reason = request.Reason,
            CallerId = callerId
        };

        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            // Trả về 409 Conflict nếu là lỗi VersionConflict
            if (result.Error.Code == "parking.pricing_version_conflict")
            {
                return Conflict(result.Error);
            }
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}

// Lớp DTO để map cục JSON từ Frontend gửi lên
public class ConfigurePricingRequest
{
    public string VehicleType { get; set; } = string.Empty;
    public string BillingMode { get; set; } = string.Empty;
    public int? BlockMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal OvernightFee { get; set; }
    public int? ExpectedVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
}
