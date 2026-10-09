namespace ParkingSystem.BuildingBlocks.Results;

public sealed record Error(string Code, string Message)
{
    // Set on throttling errors so the HTTP layer can send a Retry-After header with the real remaining time.
    public int? RetryAfterSeconds { get; init; }
}

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
}
