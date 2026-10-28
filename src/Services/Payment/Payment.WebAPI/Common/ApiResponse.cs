namespace ParkingSystem.Payment.WebAPI.Common;

/// <summary>
/// Cấu trúc phản hồi chuẩn của API Payment.
/// </summary>
public sealed class ApiResponse<T>
{
    public T? Result { get; init; }
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ApiResponse<T> Success(T? result, string message, int statusCode = 200) =>
        new()
        {
            Result = result,
            IsSuccess = true,
            StatusCode = statusCode,
            Message = message
        };

    public static ApiResponse<T> Failure(string message, int statusCode = 400) =>
        new()
        {
            Result = default,
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message
        };
}
