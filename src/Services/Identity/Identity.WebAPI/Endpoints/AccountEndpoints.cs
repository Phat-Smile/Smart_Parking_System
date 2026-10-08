using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
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
    public sealed record RefreshTokenRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);

    private static readonly Dictionary<string, int> RefreshErrorStatusCodes = new()
    {
        [RefreshTokenErrors.InvalidRefreshToken.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.RefreshTokenExpired.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.SessionExpired.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.SessionRevoked.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.TokenReuseDetected.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.AccountSuspended.Code] = StatusCodes.Status403Forbidden,
        [RefreshTokenErrors.AccountDisabled.Code] = StatusCodes.Status403Forbidden,
        [RefreshTokenErrors.EmailNotVerified.Code] = StatusCodes.Status403Forbidden
    };

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01 & US-AUTH-001: registration is a public endpoint. Validation failures become 400 in the shared exception handler.
        app.MapPost("/api/identity/accounts", async (RegisterAccountRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RegisterAccountCommand(
                        request.FullName ?? string.Empty,
                        request.Email,
                        request.Password,
                        request.PhoneNumber),
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return result.Value!.IsNewAccount
                    ? Results.Created((string?)null, result.Value)
                    : Results.Ok(result.Value);
            })
            .WithName("RegisterAccount")
            .Produces<RegisteredAccount>(StatusCodes.Status201Created)
            .Produces<RegisteredAccount>(StatusCodes.Status200OK)
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
        app.MapPost("/api/identity/accounts/login", async (
                LoginRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

                var result = await sender.Send(
                    new LoginCommand(request.Email, request.Password, deviceInfo, ipAddress),
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

        // US-AUTH-005: refresh access token and rotate refresh token (public endpoint).
        app.MapPost("/api/identity/accounts/refresh", async (
                RefreshTokenRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = RefreshErrorStatusCodes.GetValueOrDefault(result.Error!.Code, StatusCodes.Status400BadRequest);

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Code,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Refresh Access Token and rotate Refresh Token")
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // US-AUTH-005: logout current session (public endpoint, authenticated via Refresh Token).
        app.MapPost("/api/identity/accounts/logout", async (
                LogoutRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);

                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Logout current session")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        // US-AUTH-005: logout all active sessions on all devices (authenticated endpoint).
        app.MapPost("/api/identity/accounts/logout-all", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new LogoutAllCommand(accountId), cancellationToken);

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithName("LogoutAll")
            .WithSummary("Logout all active sessions on all devices")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
