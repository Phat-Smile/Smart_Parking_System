using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.WebAPI.Common;

namespace ParkingSystem.Payment.WebAPI.Controllers;

[ApiController]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public abstract class ApiControllerBase : ControllerBase
{
    protected bool TryGetCurrentUserId(out Guid userId, out IActionResult? errorResult)
    {
        // Tìm thông tin User ID trong Token
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null)
        {
            claim = User.FindFirst("sub");
        }

        // Token không chứa thông tin User ID
        if (claim == null)
        {
            userId = Guid.Empty;
            errorResult = StatusCode(
                StatusCodes.Status401Unauthorized,
                ApiResponse<object>.Failure("Authentication is required.", StatusCodes.Status401Unauthorized)
            );
            return false;
        }

        // User ID trong Token bị rỗng chuỗi
        if (string.IsNullOrWhiteSpace(claim.Value))
        {
            userId = Guid.Empty;
            errorResult = StatusCode(
                StatusCodes.Status401Unauthorized,
                ApiResponse<object>.Failure("Authentication is required.", StatusCodes.Status401Unauthorized)
            );
            return false;
        }

        //  User ID trong Token không đúng định dạng GUID (chuỗi bậy bạ)
        var parsed = Guid.TryParse(claim.Value, out var parsedGuid);
        if (parsed == false)
        {
            userId = Guid.Empty;
            errorResult = StatusCode(
                StatusCodes.Status401Unauthorized,
                ApiResponse<object>.Failure("Invalid user identification in token.", StatusCodes.Status401Unauthorized)
            );
            return false;
        }

        userId = parsedGuid;
        errorResult = null;
        return true;
    }

    protected IActionResult HandleResult<T>(Result<T> result, string successMessage)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.Success(result.Value!, successMessage, StatusCodes.Status200OK);
            return Ok(response);
        }

        var statusCode = MapErrorToStatusCode(result.Error);
        var message = "An error occurred.";
        if (result.Error != null)
        {
            message = result.Error.Message;
        }

        var failureResponse = ApiResponse<T>.Failure(message, statusCode);
        return StatusCode(statusCode, failureResponse);
    }

    private int MapErrorToStatusCode(Error? error)
    {
        if (error == null)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (error.Code == "payment.customer_only")
        {
            return StatusCodes.Status403Forbidden;
        }

        if (error.Code == "payment.invalid_user")
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (error.Code.Contains("not_found", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status404NotFound;
        }

        return StatusCodes.Status400BadRequest;
    }
}
