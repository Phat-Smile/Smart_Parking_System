using System.Globalization;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.GoogleLogin;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
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
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record GoogleLoginRequest(string IdToken);
    public sealed record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);

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

    private static readonly Dictionary<string, int> LoginErrorStatusCodes = new()
    {
        [LoginErrors.InvalidCredentials.Code] = StatusCodes.Status401Unauthorized,
        [LoginErrors.EmailNotVerified.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountSuspended.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountDisabled.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountTemporarilyLocked.Code] = StatusCodes.Status423Locked,
        [GoogleLoginErrors.InvalidGoogleToken.Code] = StatusCodes.Status401Unauthorized,
        [GoogleLoginErrors.GoogleLoginUnavailable.Code] = StatusCodes.Status503ServiceUnavailable
    };

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01 & US-AUTH-001: registration is a public endpoint. Validation failures become 400 in the shared exception handler.
        app.MapPost("/api/identity/accounts", async (RegisterAccountRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RegisterAccountCommand(
                        request.FullName,
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

                return Results.Created((string?)null, result.Value);
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
        app.MapPost("/api/identity/accounts/resend-otp", async (ResendOtpRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ResendOtpCommand(request.Email),
                    cancellationToken);

                if (result.Error?.RetryAfterSeconds is int retryAfter)
                    httpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

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

        // US-AUTH-006: request a password reset code (public; same 200 for every well-formed email).
        app.MapPost("/api/identity/accounts/forgot-password", async (ForgotPasswordRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);

                if (result.Error?.RetryAfterSeconds is int retryAfter)
                    httpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too Many Requests",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("ForgotPassword")
            .Produces<ForgotPasswordResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // US-AUTH-006: set a new password with the emailed code (public endpoint).
        app.MapPost("/api/identity/accounts/reset-password", async (ResetPasswordRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ResetPasswordCommand(request.Email, request.OtpCode, request.NewPassword),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("ResetPassword")
            .Produces<PasswordResetResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

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

                var statusCode = LoginErrorStatusCodes.GetValueOrDefault(result.Error!.Code, StatusCodes.Status400BadRequest);

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

        // US-AUTH-002: sign in or sign up with a Google ID token obtained by the frontend (public endpoint).
        app.MapPost("/api/identity/accounts/google", async (
                GoogleLoginRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GoogleLoginCommand(
                        request.IdToken,
                        httpContext.Request.Headers.UserAgent.ToString(),
                        httpContext.Connection.RemoteIpAddress?.ToString()),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                return Results.Problem(
                    statusCode: LoginErrorStatusCodes.GetValueOrDefault(result.Error!.Code, StatusCodes.Status400BadRequest),
                    title: result.Error.Code,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("GoogleLogin")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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
