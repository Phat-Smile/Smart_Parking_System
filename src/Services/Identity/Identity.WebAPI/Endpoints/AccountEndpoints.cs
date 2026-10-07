using MediatR;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;

namespace ParkingSystem.Identity.WebAPI.Endpoints;

internal static class AccountEndpoints
{
    public sealed record RegisterAccountRequest(
        string? FullName,
        string Email,
        string Password,
        string? PhoneNumber);
    public sealed record VerifyEmailRequest(string Email, string OtpCode);
    public sealed record ResendOtpRequest(string Email);
    public sealed record LoginRequest(string Email, string Password);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01 & US-AUTH-001: registration is a public endpoint. Validation failures become 400 in the shared exception handler.
        app.MapPost("/api/identity/accounts", async (RegisterAccountRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RegisterAccountCommand(
                        request.FullName ?? "Registered User",
                        request.Email,
                        request.Password,
                        request.PhoneNumber),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created((string?)null, result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "The email address is already registered.",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("RegisterAccount")
            .Produces<RegisteredAccount>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // US-AUTH-003: verify email OTP (public endpoint).
        app.MapPost("/api/identity/accounts/verify-email", async (VerifyEmailRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new VerifyEmailOtpCommand(request.Email, request.OtpCode),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("VerifyEmailOtp")
            .Produces<VerifiedEmailAccount>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // US-AUTH-003: resend verification OTP (public endpoint).
        app.MapPost("/api/identity/accounts/resend-otp", async (ResendOtpRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ResendOtpCommand(request.Email),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too Many Requests",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("ResendEmailOtp")
            .Produces<ResentOtpResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // US-AUTH-004: user login (public endpoint).
        app.MapPost("/api/identity/accounts/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new LoginCommand(request.Email, request.Password),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error!.Code switch
                {
                    "identity.invalid_credentials" => StatusCodes.Status401Unauthorized,
                    "identity.email_not_verified" => StatusCodes.Status403Forbidden,
                    "identity.account_suspended" => StatusCodes.Status403Forbidden,
                    "identity.account_disabled" => StatusCodes.Status403Forbidden,
                    "identity.account_temporarily_locked" => StatusCodes.Status423Locked,
                    _ => StatusCodes.Status400BadRequest
                };

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Code,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("Login")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked);

        return app;
    }
}
