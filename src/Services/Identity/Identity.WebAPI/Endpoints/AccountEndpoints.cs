using System.Globalization;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.GoogleLogin;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.WebAPI.Security;

namespace ParkingSystem.Identity.WebAPI.Endpoints;

internal static class AccountEndpoints
{
    /// <summary>
    /// Dữ liệu yêu cầu đăng ký tài khoản người dùng mới.
    /// </summary>
    public sealed record RegisterAccountRequest(
        /// <summary>
        /// [Tùy chọn] Họ và tên đầy đủ (2-100 ký tự). Có thể cập nhật sau ở trang Hồ sơ cá nhân.
        /// </summary>
        /// <example>Nguyễn Văn A</example>
        string? FullName,

        /// <summary>
        /// [Bắt buộc] Địa chỉ email hợp lệ dùng để đăng nhập và nhận OTP xác thực.
        /// </summary>
        /// <example>user@example.com</example>
        [property: System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email là bắt buộc, không được để trống.")]
        [property: System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Email không đúng định dạng hợp lệ.")]
        string Email,

        /// <summary>
        /// [Bắt buộc] Mật khẩu bảo mật (tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt).
        /// </summary>
        /// <example>SecurePassword123!</example>
        [property: System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mật khẩu là bắt buộc, không được để trống.")]
        string Password,

        /// <summary>
        /// [Tùy chọn] Số điện thoại di động Việt Nam (10 chữ số, bắt đầu bằng 0 hoặc +84).
        /// </summary>
        /// <example>0912345678</example>
        string? PhoneNumber);
    /// <summary>
    /// Dữ liệu yêu cầu xác thực email bằng mã OTP.
    /// </summary>
    public sealed record VerifyEmailRequest(
        /// <summary>Địa chỉ email đã đăng ký cần xác thực.</summary>
        /// <example>user@example.com</example>
        string Email,
        /// <summary>Mã OTP 6 chữ số đã nhận qua email.</summary>
        /// <example>123456</example>
        string OtpCode);

    /// <summary>
    /// Dữ liệu yêu cầu gửi lại mã OTP xác thực email.
    /// </summary>
    public sealed record ResendOtpRequest(
        /// <summary>Địa chỉ email cần nhận lại mã OTP.</summary>
        /// <example>user@example.com</example>
        string Email);

    /// <summary>
    /// Dữ liệu yêu cầu đăng nhập tài khoản.
    /// </summary>
    public sealed record LoginRequest(
        /// <summary>Địa chỉ email đã đăng ký.</summary>
        /// <example>user@example.com</example>
        string Email,
        /// <summary>Mật khẩu đăng nhập.</summary>
        /// <example>SecurePassword123!</example>
        string Password);

    /// <summary>
    /// Dữ liệu yêu cầu làm mới Access Token.
    /// </summary>
    public sealed record RefreshTokenRequest(
        /// <summary>Mã làm mới (Refresh Token) hợp lệ đã nhận khi đăng nhập.</summary>
        /// <example>sample_refresh_token_string</example>
        string RefreshToken);

    /// <summary>
    /// Dữ liệu yêu cầu đăng xuất phiên hiện tại.
    /// </summary>
    public sealed record LogoutRequest(
        /// <summary>Mã làm mới (Refresh Token) của phiên cần đăng xuất.</summary>
        /// <example>sample_refresh_token_string</example>
        string RefreshToken);

    /// <summary>
    /// Dữ liệu yêu cầu mã đặt lại mật khẩu khi quên mật khẩu.
    /// </summary>
    public sealed record ForgotPasswordRequest(
        /// <summary>Địa chỉ email của tài khoản cần đặt lại mật khẩu.</summary>
        /// <example>user@example.com</example>
        string Email);

    /// <summary>
    /// Dữ liệu yêu cầu đăng nhập bằng Google.
    /// </summary>
    public sealed record GoogleLoginRequest(
        /// <summary>Google ID Token nhận được từ Google Identity Services trên frontend/mobile.</summary>
        /// <example>eyJhbGciOiJSUzI1NiIsImtpZCI6Ij...sample_google_id_token</example>
        string IdToken);

    /// <summary>
    /// Dữ liệu yêu cầu đổi mật khẩu tài khoản hiện tại.
    /// </summary>
    public sealed record ChangePasswordRequest(
        /// <summary>Mật khẩu hiện tại của tài khoản.</summary>
        /// <example>CurrentPassword123!</example>
        string CurrentPassword,
        /// <summary>Mật khẩu mới cần đổi.</summary>
        /// <example>NewSecurePassword123!</example>
        string NewPassword);

    /// <summary>
    /// Dữ liệu yêu cầu đặt lại mật khẩu bằng mã OTP.
    /// </summary>
    public sealed record ResetPasswordRequest(
        /// <summary>Địa chỉ email của tài khoản.</summary>
        /// <example>user@example.com</example>
        string Email,
        /// <summary>Mã OTP 6 chữ số nhận qua email.</summary>
        /// <example>123456</example>
        string OtpCode,
        /// <summary>Mật khẩu mới cần đặt.</summary>
        /// <example>NewSecurePassword123!</example>
        string NewPassword);

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
        [LoginErrors.PasswordNotSet.Code] = StatusCodes.Status401Unauthorized,
        [LoginErrors.EmailNotVerified.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountSuspended.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountDisabled.Code] = StatusCodes.Status403Forbidden,
        [LoginErrors.AccountTemporarilyLocked.Code] = StatusCodes.Status423Locked,
        [GoogleLoginErrors.InvalidGoogleToken.Code] = StatusCodes.Status401Unauthorized,
        [GoogleLoginErrors.GoogleLoginUnavailable.Code] = StatusCodes.Status503ServiceUnavailable,
        [ChangePasswordErrors.InvalidCurrentPassword.Code] = StatusCodes.Status400BadRequest,
        [UnlockAccountErrors.AccountNotFound.Code] = StatusCodes.Status404NotFound
    };

    private static IResult AccountProblem(ParkingSystem.BuildingBlocks.Results.Error error) =>
        Results.Problem(
            statusCode: LoginErrorStatusCodes.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            title: error.Message,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01 & US-AUTH-001: Đăng ký tài khoản người dùng mới (Public Endpoint).
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
            .WithTags("1. Đăng ký & Kích hoạt (Registration)")
            .WithName("RegisterAccount")
            .WithSummary("Đăng ký tài khoản")
            .WithDescription("Bắt buộc: email, password. Tùy chọn: fullName, phoneNumber.")
            .Produces<RegisteredAccount>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

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
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("1. Đăng ký & Kích hoạt (Registration)")
            .WithName("VerifyEmailOtp")
            .WithSummary("Xác thực email bằng mã OTP")
            .WithDescription("Bắt buộc: email, otpCode (6 chữ số).")
            .Produces<VerifiedEmailAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

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
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("1. Đăng ký & Kích hoạt (Registration)")
            .WithName("ResendEmailOtp")
            .WithSummary("Gửi lại mã OTP xác thực email")
            .WithDescription("Gửi lại mã OTP nếu tài khoản đang chờ kích hoạt. Cooldown 60 giây và giới hạn 5 lần/giờ.")
            .Produces<ResentOtpResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

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
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("3. Quản lý mật khẩu (Password Management)")
            .WithName("ForgotPassword")
            .WithSummary("Yêu cầu mã đặt lại mật khẩu")
            .WithDescription("Gửi mã OTP về email nếu tài khoản tồn tại và đã kích hoạt. Trả về generic 200 để chống dò tài khoản.")
            .Produces<ForgotPasswordResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

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
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("3. Quản lý mật khẩu (Password Management)")
            .WithName("ResetPassword")
            .WithSummary("Đặt lại mật khẩu")
            .WithDescription("Đặt mật khẩu mới bằng mã OTP xác thực đã nhận được qua email.")
            .Produces<PasswordResetResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

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
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("Login")
            .WithSummary("Đăng nhập tài khoản")
            .WithDescription("Đăng nhập bằng email và mật khẩu để nhận Access Token và Refresh Token.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status423Locked);

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
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("GoogleLogin")
            .WithSummary("Đăng nhập bằng Google")
            .WithDescription("Đăng nhập hoặc đăng ký tự động bằng Google ID Token.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status423Locked)
            .Produces(StatusCodes.Status503ServiceUnavailable);

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
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .AllowAnonymous()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("RefreshToken")
            .WithSummary("Làm mới token truy cập")
            .WithDescription("Sử dụng Refresh Token để cấp lại Access Token mới và xoay vòng Refresh Token.")
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("Logout")
            .WithSummary("Đăng xuất phiên hiện tại")
            .WithDescription("Thu hồi phiên làm việc gắn liền với Refresh Token.")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

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
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("LogoutAll")
            .WithSummary("Đăng xuất tất cả thiết bị")
            .WithDescription("Thu hồi toàn bộ phiên làm việc của tài khoản (yêu cầu Access Token).")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // US-AUTH-009: change the password of the signed-in account (taken from the token, never from the body).
        app.MapPost("/api/identity/accounts/change-password", async (
                ChangePasswordRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(
                    new ChangePasswordCommand(accountId, request.CurrentPassword, request.NewPassword),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(result.Error!);
            })
            .RequireAuthorization()
            .WithTags("3. Quản lý mật khẩu (Password Management)")
            .WithName("ChangePassword")
            .WithSummary("Đổi mật khẩu")
            .WithDescription("Thay đổi mật khẩu bằng mật khẩu hiện tại (yêu cầu Access Token).")
            .Produces<PasswordChangedResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status423Locked);

        // US-AUTH-008: an administrator clears the login lockout of an account.
        app.MapPost("/api/identity/admin/accounts/{accountId:guid}/unlock", async (
                Guid accountId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new UnlockAccountCommand(accountId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(result.Error!);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("UnlockAccount")
            .WithSummary("Mở khóa đăng nhập tài khoản (Admin)")
            .WithDescription("Dành cho Quản trị viên để gỡ bỏ trạng thái khóa sau 3 lần đăng nhập sai.")
            .Produces<UnlockedAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
