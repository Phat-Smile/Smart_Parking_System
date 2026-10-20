using System.Globalization;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.GetMe;
using ParkingSystem.Identity.Application.Accounts.GoogleLogin;
using ParkingSystem.Identity.Application.Accounts.ListSessions;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using ParkingSystem.Identity.Application.Accounts.RevokeSession;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Application.Accounts.Admin.ActivateAccount;
using ParkingSystem.Identity.Application.Accounts.Admin.SearchAccounts;
using ParkingSystem.Identity.Application.Accounts.Admin.SuspendAccount;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Profiles.UpsertProfile;
using ParkingSystem.Identity.Application.Vehicles.AddVehicle;
using ParkingSystem.Identity.Application.Vehicles.GetVehicles;
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
        string? PhoneNumber,

        /// <summary>
        /// [Tùy chọn] Loại tài khoản đăng ký: "Customer" (mặc định) hoặc "ParkingLotOwner".
        /// </summary>
        /// <example>Customer</example>
        string? AccountType = null);

    /// <summary>
    /// Dữ liệu yêu cầu từ chối hồ sơ đăng ký chủ bãi.
    /// </summary>
    public sealed record RejectOwnerApplicationRequest(
        /// <summary>[Bắt buộc] Lý do từ chối (1-500 ký tự).</summary>
        /// <example>Hồ sơ thiếu giấy phép kinh doanh bãi đỗ xe hợp lệ.</example>
        string Reason);

    /// <summary>
    /// Dữ liệu yêu cầu nộp hồ sơ xét duyệt làm Chủ bãi đỗ xe.
    /// </summary>
    public sealed record ApplyOwnerApplicationRequest(
        /// <summary>[Tùy chọn] Mã số thuế (10 hoặc 13 chữ số).</summary>
        /// <example>0312345678</example>
        string? TaxCode,
        /// <summary>[Tùy chọn] Tên doanh nghiệp hoặc hộ kinh doanh.</summary>
        /// <example>Công ty TNHH Bãi xe Thông minh</example>
        string? BusinessName,
        /// <summary>[Tùy chọn] Số điện thoại liên hệ di động Việt Nam.</summary>
        /// <example>0912345678</example>
        string? PhoneNumber,
        /// <summary>[Tùy chọn] Họ và tên người đại diện.</summary>
        /// <example>Nguyễn Văn A</example>
        string? FullName);

    /// <summary>
    /// Dữ liệu yêu cầu nộp lại hồ sơ xét duyệt làm Chủ bãi sau khi bị từ chối.
    /// </summary>
    public sealed record ResubmitOwnerApplicationRequest(
        /// <summary>[Tùy chọn] Mã số thuế đã điều chỉnh.</summary>
        /// <example>0312345678</example>
        string? TaxCode,
        /// <summary>[Tùy chọn] Tên doanh nghiệp/hộ kinh doanh.</summary>
        /// <example>Công ty TNHH Bãi xe Thông minh</example>
        string? BusinessName);

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

    /// <summary>
    /// Dữ liệu cập nhật hồ sơ cá nhân.
    /// </summary>
    public sealed record UpsertProfileRequest(
        /// <summary>Họ và tên đầy đủ (bắt buộc, tối đa 100 ký tự).</summary>
        /// <example>Nguyễn Văn A</example>
        string FullName,
        /// <summary>Ngày sinh (ISO 8601 date, tùy chọn).</summary>
        /// <example>1995-05-15</example>
        DateOnly? DateOfBirth,
        /// <summary>Số CCCD/CMND (tùy chọn, tối đa 20 ký tự).</summary>
        /// <example>012345678901</example>
        string? CitizenIdNumber,
        /// <summary>Số bằng lái xe (tùy chọn, tối đa 20 ký tự).</summary>
        /// <example>B2-012345</example>
        string? DriverLicenseNumber,
        /// <summary>URL ảnh đại diện (tùy chọn, tối đa 500 ký tự).</summary>
        /// <example>https://cdn.example.com/avatar.jpg</example>
        string? AvatarUrl);

    /// <summary>
    /// Dữ liệu yêu cầu thêm phương tiện.
    /// </summary>
    public sealed record AddVehicleRequest(
        /// <summary>Biển số xe (bắt buộc, tối đa 20 ký tự). Tự động chuẩn hóa thành chữ hoa.</summary>
        /// <example>51A-123.45</example>
        string LicensePlate,
        /// <summary>Loại phương tiện: Car hoặc Motorbike.</summary>
        /// <example>Car</example>
        string VehicleType,
        /// <summary>Tên gợi nhớ (tùy chọn, tối đa 50 ký tự).</summary>
        /// <example>Xe đi làm</example>
        string? NickName);

    /// <summary>
    /// Dữ liệu yêu cầu đổi vai trò tài khoản.
    /// </summary>
    public sealed record ChangeAccountRoleRequest(
        /// <summary>Vai trò mới: Customer, Staff, ParkingLotOwner hoặc SystemAdmin (không phân biệt hoa thường).</summary>
        /// <example>Staff</example>
        string? Role);

    private const string RefreshCookieName = "sp_refresh";
    private const int RefreshCookieMaxAgeDays = 30;

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
        [UnlockAccountErrors.AccountNotFound.Code] = StatusCodes.Status404NotFound,
        [ChangeAccountRoleErrors.CannotChangeOwnRole.Code] = StatusCodes.Status403Forbidden
    };

    // 423 and 429 errors carry the seconds to wait; clients read them from the standard header.
    private static void SetRetryAfter(HttpContext httpContext, ParkingSystem.BuildingBlocks.Results.Error? error)
    {
        if (error?.RetryAfterSeconds is int retryAfter)
            httpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
    }

    // Responses that carry tokens must not be stored by browsers or proxies.
    private static RouteHandlerBuilder WithNoStore(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            return await next(context);
        });

    private static IResult AccountProblem(HttpContext httpContext, ParkingSystem.BuildingBlocks.Results.Error error)
    {
        SetRetryAfter(httpContext, error);
        return Results.Problem(
            statusCode: LoginErrorStatusCodes.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            title: error.Message,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }

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
                        request.PhoneNumber,
                        request.AccountType),
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

                SetRetryAfter(httpContext, result.Error);

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

                SetRetryAfter(httpContext, result.Error);

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
                {
                    httpContext.Response.Cookies.Append(RefreshCookieName, result.Value!.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/api/identity/accounts",
                        MaxAge = TimeSpan.FromDays(RefreshCookieMaxAgeDays),
                        Secure = !httpContext.Request.IsHttps ? false : true
                    });
                    return Results.Ok(result.Value);
                }

                return AccountProblem(httpContext, result.Error!);
            })
            .WithNoStore()
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
                {
                    httpContext.Response.Cookies.Append(RefreshCookieName, result.Value!.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/api/identity/accounts",
                        MaxAge = TimeSpan.FromDays(RefreshCookieMaxAgeDays),
                        Secure = !httpContext.Request.IsHttps ? false : true
                    });
                    return Results.Ok(result.Value);
                }

                return AccountProblem(httpContext, result.Error!);
            })
            .WithNoStore()
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
        // Cookie takes priority; body fallback supports mobile clients.
        app.MapPost("/api/identity/accounts/refresh", async (
                RefreshTokenRequest? request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var token = httpContext.Request.Cookies[RefreshCookieName]
                    ?? request?.RefreshToken
                    ?? string.Empty;

                var result = await sender.Send(new RefreshTokenCommand(token), cancellationToken);

                if (result.IsSuccess)
                {
                    httpContext.Response.Cookies.Append(RefreshCookieName, result.Value!.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Path = "/api/identity/accounts",
                        MaxAge = TimeSpan.FromDays(RefreshCookieMaxAgeDays),
                        Secure = !httpContext.Request.IsHttps ? false : true
                    });
                    return Results.Ok(result.Value);
                }

                var statusCode = RefreshErrorStatusCodes.GetValueOrDefault(result.Error!.Code, StatusCodes.Status400BadRequest);

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .AllowAnonymous()
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("RefreshToken")
            .WithSummary("Làm mới token truy cập")
            .WithDescription("Sử dụng Refresh Token để cấp lại Access Token mới và xoay vòng Refresh Token.")
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // US-AUTH-005: logout current session. Reads refresh token from cookie or body; always clears cookie.
        app.MapPost("/api/identity/accounts/logout", async (
                LogoutRequest? request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var token = httpContext.Request.Cookies[RefreshCookieName]
                    ?? request?.RefreshToken
                    ?? string.Empty;

                httpContext.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
                {
                    Path = "/api/identity/accounts"
                });

                var result = await sender.Send(new LogoutCommand(token), cancellationToken);

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
                HttpContext httpContext,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(
                    new ChangePasswordCommand(accountId, request.CurrentPassword, request.NewPassword),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(httpContext, result.Error!);
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
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new UnlockAccountCommand(accountId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(httpContext, result.Error!);
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

        // Admin đổi vai trò tài khoản; AdminId lấy từ token để chặn tự đổi vai trò của chính mình.
        app.MapPut("/api/identity/admin/accounts/{accountId:guid}/role", async (
                Guid accountId,
                ChangeAccountRoleRequest request,
                HttpContext httpContext,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Unauthorized();

                var result = await sender.Send(new ChangeAccountRoleCommand(adminId, accountId, request.Role ?? string.Empty), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(httpContext, result.Error!);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("ChangeAccountRole")
            .WithSummary("Đổi vai trò tài khoản (Admin)")
            .WithDescription("Đổi vai trò Customer / Staff / ParkingLotOwner / SystemAdmin. Không tự đổi vai trò của mình; đổi xong người dùng bị đăng xuất khỏi mọi thiết bị.")
            .Produces<ChangedAccountRole>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // US-PROFILE-001: Get current user's account and profile data.
        app.MapGet("/api/identity/accounts/me", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new GetMeQuery(accountId), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization()
            .WithTags("5. Hồ sơ & Phiên (Profile & Sessions)")
            .WithName("GetMe")
            .WithSummary("Lấy thông tin tài khoản và hồ sơ cá nhân")
            .Produces<MeResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // US-AUTH-010: List all active sessions for the current account.
        app.MapGet("/api/identity/accounts/sessions", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                Guid.TryParse(currentUser.SessionId, out var currentSessionId);
                var result = await sender.Send(new ListSessionsQuery(accountId, currentSessionId == Guid.Empty ? null : currentSessionId), cancellationToken);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("5. Hồ sơ & Phiên (Profile & Sessions)")
            .WithName("ListSessions")
            .WithSummary("Danh sách phiên đăng nhập đang hoạt động")
            .Produces<ListSessionsResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // US-AUTH-010: Revoke a specific session (log out another device).
        app.MapDelete("/api/identity/accounts/sessions/{sessionId:guid}", async (
                Guid sessionId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new RevokeSessionCommand(accountId, sessionId), cancellationToken);

                if (!result.IsSuccess)
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });

                // If the revoked session is the caller's own current session, expire the refresh cookie.
                if (Guid.TryParse(currentUser.SessionId, out var currentSessionId) && sessionId == currentSessionId)
                    httpContext.Response.Cookies.Delete(RefreshCookieName);

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("5. Hồ sơ & Phiên (Profile & Sessions)")
            .WithName("RevokeSession")
            .WithSummary("Thu hồi phiên làm việc trên thiết bị khác")
            .Produces<RevokedSession>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // US-ADMIN-003: Tìm kiếm và phân trang danh sách tài khoản (Admin).
        app.MapGet("/api/identity/admin/accounts", async (
                string? search,
                string? role,
                string? status,
                int page,
                int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SearchAccountsQuery(search, role, status, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("SearchAccounts")
            .WithSummary("Tìm kiếm danh sách tài khoản (Admin)")
            .Produces<SearchAccountsResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // US-ADMIN-003: Tạm đình chỉ tài khoản người dùng.
        app.MapPost("/api/identity/admin/accounts/{accountId:guid}/suspend", async (
                Guid accountId,
                HttpContext httpContext,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Unauthorized();

                var result = await sender.Send(new SuspendAccountCommand(adminId, accountId), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error!.Code switch
                {
                    "identity.account_not_found" => StatusCodes.Status404NotFound,
                    "identity.admin_cannot_suspend_self" or "identity.cannot_suspend_admin" => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                };

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("SuspendAccount")
            .WithSummary("Tạm đình chỉ tài khoản (Admin)")
            .Produces<SuspendedAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // US-ADMIN-003: Kích hoạt lại tài khoản đang bị đình chỉ.
        app.MapPost("/api/identity/admin/accounts/{accountId:guid}/activate", async (
                Guid accountId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ActivateAccountCommand(accountId), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error!.Code switch
                {
                    "identity.account_not_found" => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status400BadRequest
                };

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("ActivateAccount")
            .WithSummary("Kích hoạt lại tài khoản (Admin)")
            .Produces<ActivatedAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // US-PROFILE-001/002: Create or update the current user's profile (upsert).
        app.MapPut("/api/identity/accounts/me/profile", async (
                UpsertProfileRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(
                    new UpsertProfileCommand(
                        accountId,
                        request.FullName,
                        request.DateOfBirth,
                        request.CitizenIdNumber,
                        request.DriverLicenseNumber,
                        request.AvatarUrl),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization()
            .WithTags("5. Hồ sơ & Phiên (Profile & Sessions)")
            .WithName("UpsertProfile")
            .WithSummary("Tạo hoặc cập nhật hồ sơ cá nhân")
            .WithDescription("Upsert: tạo mới nếu chưa có, cập nhật nếu đã có. Bắt buộc: fullName.")
            .Produces<ProfileResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // US-VEH-001: Add a vehicle to the current account.
        app.MapPost("/api/identity/accounts/me/vehicles", async (
                AddVehicleRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(
                    new AddVehicleCommand(accountId, request.LicensePlate, request.VehicleType, request.NickName),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Created((string?)null, result.Value);

                var statusCode = result.Error!.Code switch
                {
                    "identity.vehicle_plate_already_registered" => StatusCodes.Status409Conflict,
                    "identity.account_not_found" => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status400BadRequest
                };

                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error.Message,
                    detail: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization()
            .WithTags("6. Phương tiện (Vehicles)")
            .WithName("AddVehicle")
            .WithSummary("Thêm phương tiện vào tài khoản")
            .WithDescription("Đăng ký biển số xe với tài khoản để hệ thống nhận diện xe ra/vào bãi đỗ. Bắt buộc: licensePlate, vehicleType (Car hoặc Motorbike).")
            .Produces<AddedVehicleResult>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // US-VEH-001: List all vehicles for the current account.
        app.MapGet("/api/identity/accounts/me/vehicles", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new GetVehiclesQuery(accountId), cancellationToken);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("6. Phương tiện (Vehicles)")
            .WithName("GetVehicles")
            .WithSummary("Danh sách phương tiện của tài khoản")
            .Produces<GetVehiclesResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // US-ADMIN-001: Lấy thông tin trạng thái xét duyệt Chủ bãi của tài khoản hiện tại.
        app.MapGet("/api/identity/accounts/me/owner-application", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new GetMyOwnerApplicationQuery(accountId), cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("GetMyOwnerApplication")
            .WithSummary("Xem trạng thái xét duyệt Chủ bãi của tài khoản")
            .Produces<MyOwnerApplicationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // US-ADMIN-001: Nộp hồ sơ xét duyệt nâng cấp tài khoản lên Chủ bãi.
        app.MapPost("/api/identity/accounts/me/owner-application/apply", async (
                ApplyOwnerApplicationRequest? request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var command = new ApplyForOwnerApplicationCommand(
                    accountId,
                    request?.TaxCode,
                    request?.BusinessName,
                    request?.PhoneNumber,
                    request?.FullName);

                var result = await sender.Send(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.account_not_found" => StatusCodes.Status404NotFound,
                        "identity.invalid_tax_code" => StatusCodes.Status400BadRequest,
                        _ => StatusCodes.Status409Conflict
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("ApplyForOwnerApplication")
            .WithSummary("Nộp hồ sơ xét duyệt nâng cấp lên Chủ bãi")
            .Produces<OwnerApplicationActionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // US-ADMIN-001: Gửi lại đơn xét duyệt Chủ bãi sau khi bị từ chối.
        app.MapPost("/api/identity/accounts/me/owner-application/resubmit", async (
                ResubmitOwnerApplicationRequest? request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var command = new ResubmitOwnerApplicationCommand(
                    accountId,
                    request?.TaxCode,
                    request?.BusinessName);

                var result = await sender.Send(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        _ => StatusCodes.Status409Conflict
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("ResubmitOwnerApplication")
            .WithSummary("Gửi lại đơn xét duyệt Chủ bãi")
            .Produces<OwnerApplicationActionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // US-ADMIN-001: Danh sách hồ sơ xét duyệt Chủ bãi (Dành cho Quản trị viên).
        app.MapGet("/api/identity/admin/owner-applications", async (
                string? status,
                int page,
                int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new ListOwnerApplicationsQuery(status, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize);
                var result = await sender.Send(query, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("ListOwnerApplications")
            .WithSummary("Danh sách đơn xét duyệt Chủ bãi (Admin)")
            .Produces<ListOwnerApplicationsResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // US-ADMIN-001: Phê duyệt hồ sơ đăng ký Chủ bãi (Dành cho Quản trị viên).
        app.MapPost("/api/identity/admin/owner-applications/{id:guid}/approve", async (
                Guid id,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Unauthorized();

                var result = await sender.Send(new ApproveOwnerApplicationCommand(adminId, id), cancellationToken);
                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        _ => StatusCodes.Status409Conflict
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("ApproveOwnerApplication")
            .WithSummary("Phê duyệt hồ sơ Chủ bãi (Admin)")
            .Produces<OwnerApplicationActionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // US-ADMIN-001: Từ chối hồ sơ đăng ký Chủ bãi kèm lý do (Dành cho Quản trị viên).
        app.MapPost("/api/identity/admin/owner-applications/{id:guid}/reject", async (
                Guid id,
                RejectOwnerApplicationRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Unauthorized();

                var result = await sender.Send(new RejectOwnerApplicationCommand(adminId, id, request.Reason), cancellationToken);
                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        "identity.owner_reject_reason_required" => StatusCodes.Status400BadRequest,
                        _ => StatusCodes.Status409Conflict
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("RejectOwnerApplication")
            .WithSummary("Từ chối hồ sơ Chủ bãi (Admin)")
            .Produces<OwnerApplicationActionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
