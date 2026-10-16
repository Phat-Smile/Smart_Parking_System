using Microsoft.AspNetCore.Http;

namespace ParkingSystem.Payment.WebAPI.Common;

public class ApiResponse<T>
{
    public T? Result { get; set; }
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ApiResponse<T> Success(T result, string message, int statusCode)
    {
        return new ApiResponse<T>
        {
            Result = result,
            IsSuccess = true,
            StatusCode = statusCode,
            Message = message
        };
    }

    public static ApiResponse<T> Failure(string message, int statusCode)
    {
        return new ApiResponse<T>
        {
            Result = default,
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message
        };
    }
}
