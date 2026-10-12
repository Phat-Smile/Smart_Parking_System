using System.Globalization;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.AdminAccounts;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.GetCurrentAccount;
using ParkingSystem.Identity.Application.Accounts.GoogleLogin;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using ParkingSystem.Identity.Application.Accounts.Sessions;
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
        /// <summary>[Tùy chọn] Trình duyệt không cần gửi: refresh token nằm trong cookie HttpOnly. Chỉ dùng cho Postman / client không có cookie.</summary>
        /// <example>sample_refresh_token_string</example>
        string? RefreshToken);

    /// <summary>
    /// Token trả cho trình duyệt; refresh token không nằm trong body mà ở cookie HttpOnly sp_refresh.
    /// </summary>
    public sealed record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn, AccountDto Account);

    /// <summary>
    /// Dữ liệu yêu cầu đăng xuất phiên hiện tại.
    /// </summary>
    public sealed record LogoutRequest(
        /// <summary>[Tùy chọn] Trình duyệt không cần gửi: lấy từ cookie HttpOnly. Chỉ dùng cho Postman / client không có cookie.</summary>
        /// <example>sample_refresh_token_string</example>
        string? RefreshToken);

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
    /// Dữ liệu yêu cầu đổi vai trò tài khoản.
    /// </summary>
    public sealed record ChangeAccountRoleRequest(
        /// <summary>Vai trò mới: Customer, Staff, ParkingLotOwner hoặc SystemAdmin (không phân biệt hoa thường).</summary>
        /// <example>Staff</example>
        string? Role);

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
        [ChangeAccountRoleErrors.CannotChangeOwnRole.Code] = StatusCodes.Status403Forbidden,
        [SessionErrors.SessionNotFound.Code] = StatusCodes.Status404NotFound,
        [ChangeAccountStatusErrors.CannotChangeOwnStatus.Code] = StatusCodes.Status403Forbidden,
        [ChangeAccountStatusErrors.CannotChangeAdminStatus.Code] = StatusCodes.Status403Forbidden,
        [ChangeAccountStatusErrors.AccountNotVerified.Code] = StatusCodes.Status409Conflict
    };

    // Responses that carry tokens must not be stored by browsers or proxies.
    private static RouteHandlerBuilder WithNoStore(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            return await next(context);
        });

    // Mọi lỗi nghiệp vụ cùng một dạng; lỗi phải chờ (423, 429) có thêm retryAfterSeconds trong body và header Retry-After.
    private static IResult Problem(HttpContext httpContext, int statusCode, ParkingSystem.BuildingBlocks.Results.Error error)
    {
        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };
        if (error.RetryAfterSeconds is int retryAfter)
        {
            httpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
            extensions["retryAfterSeconds"] = retryAfter;
        }

        return Results.Problem(statusCode: statusCode, title: error.Message, detail: error.Message, extensions: extensions);
    }

    private static IResult AccountProblem(HttpContext httpContext, ParkingSystem.BuildingBlocks.Results.Error error) =>
        Problem(httpContext, LoginErrorStatusCodes.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest), error);

    // Refresh token vào cookie HttpOnly, body chỉ còn access token.
    private static IResult TokenResult(HttpContext httpContext, string accessToken, string tokenType, int expiresIn, string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc, AccountDto account)
    {
        RefreshTokenCookie.Set(httpContext, refreshToken, refreshTokenExpiresAtUtc);
        return Results.Ok(new AuthTokenResponse(accessToken, tokenType, expiresIn, account));
    }

    private static Guid? CurrentSessionId(HttpContext httpContext) =>
        Guid.TryParse(httpContext.User.FindFirst("sid")?.Value, out var sessionId) ? sessionId : null;

    private static async Task<IResult> ChangeStatusAsync(Guid accountId, AccountStatusChange change, ICurrentUser currentUser,
        HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
            return Results.Unauthorized();

        var result = await sender.Send(new ChangeAccountStatusCommand(adminId, accountId, change), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(httpContext, result.Error!);
    }

    private static IResult LoginResult(HttpContext httpContext, LoginResponse response) =>
        TokenResult(httpContext, response.AccessToken, response.TokenType, response.ExpiresIn, response.RefreshToken,
            response.RefreshTokenExpiresAtUtc, response.Account);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01 & US-AUTH-001: Đăng ký tài khoản người dùng mới (Public Endpoint).
        app.MapPost("/api/identity/accounts", async (RegisterAccountRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RegisterAccountCommand(
                        request.FullName,
                        request.Email,
                        request.Password,
                        request.PhoneNumber),
                    cancellationToken);

                if (!result.IsSuccess)
                    return Problem(httpContext, StatusCodes.Status409Conflict, result.Error!);

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
        app.MapPost("/api/identity/accounts/verify-email", async (VerifyEmailRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new VerifyEmailOtpCommand(
                        request.Email,
                        request.OtpCode,
                        httpContext.Request.Headers.UserAgent.ToString(),
                        httpContext.Connection.RemoteIpAddress?.ToString()),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : Problem(httpContext, StatusCodes.Status400BadRequest, result.Error!);
            })
            .WithTags("1. Đăng ký & Kích hoạt (Registration)")
            .WithName("VerifyEmailOtp")
            .WithSummary("Xác thực email bằng mã OTP")
            .WithDescription("Bắt buộc: email, otpCode (6 chữ số). Thành công thì đăng nhập luôn: `session` có cùng dạng với kết quả đăng nhập; `session` là null khi tài khoản đang bị tạm khóa đăng nhập.")
            .Produces<VerifiedEmailAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        // US-AUTH-003: resend verification OTP (public endpoint).
        app.MapPost("/api/identity/accounts/resend-otp", async (ResendOtpRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ResendOtpCommand(request.Email),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : Problem(httpContext, StatusCodes.Status429TooManyRequests, result.Error!);
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

                return result.IsSuccess ? Results.Ok(result.Value) : Problem(httpContext, StatusCodes.Status429TooManyRequests, result.Error!);
            })
            .WithTags("3. Quản lý mật khẩu (Password Management)")
            .WithName("ForgotPassword")
            .WithSummary("Yêu cầu mã đặt lại mật khẩu")
            .WithDescription("Gửi mã OTP về email nếu tài khoản tồn tại và đã kích hoạt. Trả về generic 200 để chống dò tài khoản.")
            .Produces<ForgotPasswordResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        // US-AUTH-006: set a new password with the emailed code (public endpoint).
        app.MapPost("/api/identity/accounts/reset-password", async (ResetPasswordRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ResetPasswordCommand(request.Email, request.OtpCode, request.NewPassword),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : Problem(httpContext, StatusCodes.Status400BadRequest, result.Error!);
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

                return result.IsSuccess ? LoginResult(httpContext, result.Value!) : AccountProblem(httpContext, result.Error!);
            })
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("Login")
            .WithSummary("Đăng nhập tài khoản")
            .WithDescription("Đăng nhập bằng email và mật khẩu. Body trả Access Token; Refresh Token đặt vào cookie HttpOnly sp_refresh.")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
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

                return result.IsSuccess ? LoginResult(httpContext, result.Value!) : AccountProblem(httpContext, result.Error!);
            })
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("GoogleLogin")
            .WithSummary("Đăng nhập bằng Google")
            .WithDescription("Đăng nhập hoặc đăng ký tự động bằng Google ID Token. Refresh Token đặt vào cookie HttpOnly sp_refresh.")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status423Locked)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        // US-AUTH-005: refresh access token and rotate refresh token (public endpoint).
        app.MapPost("/api/identity/accounts/refresh", async (
                RefreshTokenRequest? request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var refreshToken = RefreshTokenCookie.Read(httpContext, request?.RefreshToken);
                if (refreshToken is null)
                {
                    RefreshTokenCookie.Clear(httpContext);
                    return Problem(httpContext, StatusCodes.Status401Unauthorized, RefreshTokenErrors.InvalidRefreshToken);
                }

                var result = await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
                if (result.IsSuccess)
                {
                    var value = result.Value!;
                    return TokenResult(httpContext, value.AccessToken, value.TokenType, value.ExpiresIn, value.RefreshToken,
                        value.RefreshTokenExpiresAtUtc, value.Account);
                }

                // Phiên đã hỏng thì cookie cũ vô dụng; xoá để FE không gửi lại mãi.
                RefreshTokenCookie.Clear(httpContext);
                return Problem(httpContext, RefreshErrorStatusCodes.GetValueOrDefault(result.Error!.Code, StatusCodes.Status400BadRequest), result.Error);
            })
            .AllowAnonymous()
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("RefreshToken")
            .WithSummary("Làm mới token truy cập")
            .WithDescription("Lấy Refresh Token từ cookie sp_refresh (hoặc body nếu không có cookie), cấp Access Token mới và xoay vòng cookie. Trình duyệt gọi với credentials: include.")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // US-AUTH-005: logout current session (public endpoint, authenticated via Refresh Token).
        app.MapPost("/api/identity/accounts/logout", async (
                LogoutRequest? request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var refreshToken = RefreshTokenCookie.Read(httpContext, request?.RefreshToken);
                RefreshTokenCookie.Clear(httpContext);

                // Không có token thì coi như đã đăng xuất (idempotent).
                if (refreshToken is null)
                    return Results.Ok(new LogoutResponse());

                var result = await sender.Send(new LogoutCommand(refreshToken), cancellationToken);
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("Logout")
            .WithSummary("Đăng xuất phiên hiện tại")
            .WithDescription("Thu hồi phiên gắn với Refresh Token trong cookie sp_refresh (hoặc body) và xoá cookie.")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        // US-AUTH-005: logout all active sessions on all devices (authenticated endpoint).
        app.MapPost("/api/identity/accounts/logout-all", async (
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new LogoutAllCommand(accountId), cancellationToken);
                RefreshTokenCookie.Clear(httpContext);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("LogoutAll")
            .WithSummary("Đăng xuất tất cả thiết bị")
            .WithDescription("Thu hồi toàn bộ phiên làm việc của tài khoản (yêu cầu Access Token).")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // FE gọi sau khi tải lại trang (sau refresh) để lấy lại thông tin và role của người đang đăng nhập.
        app.MapGet("/api/identity/accounts/me", async (
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new GetCurrentAccountQuery(accountId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : AccountProblem(httpContext, result.Error!);
            })
            .RequireAuthorization()
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("GetCurrentAccount")
            .WithSummary("Thông tin tài khoản đang đăng nhập")
            .WithDescription("Trả email, họ tên, role, trạng thái của chủ access token (yêu cầu Access Token).")
            .Produces<CurrentAccount>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // Thiết bị đang đăng nhập của chính mình; phiên hiện tại nhận ra nhờ claim "sid" trong access token.
        app.MapGet("/api/identity/accounts/sessions", async (
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var result = await sender.Send(new ListSessionsQuery(accountId, CurrentSessionId(httpContext)), cancellationToken);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithNoStore()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("ListSessions")
            .WithSummary("Thiết bị đang đăng nhập")
            .WithDescription("Liệt kê các phiên còn hiệu lực của tài khoản; isCurrent = phiên của access token đang gọi.")
            .Produces<IReadOnlyList<SessionInfo>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

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
                    return AccountProblem(httpContext, result.Error!);

                // Tự đăng xuất thiết bị đang dùng thì cookie của thiết bị này cũng hết tác dụng.
                if (sessionId == CurrentSessionId(httpContext))
                    RefreshTokenCookie.Clear(httpContext);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("2. Đăng nhập & Phiên làm việc (Authentication)")
            .WithName("RevokeSession")
            .WithSummary("Đăng xuất một thiết bị")
            .WithDescription("Thu hồi một phiên của chính mình; phiên của người khác hoặc không tồn tại đều trả 404.")
            .Produces<RevokedSession>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

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

        // Danh sách người dùng cho màn /admin/users: tìm theo email hoặc họ tên, lọc role / trạng thái, phân trang.
        app.MapGet("/api/identity/admin/accounts", async (
                string? search,
                string? role,
                string? status,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SearchAccountsQuery(search, role, status, page ?? 1, pageSize ?? 20), cancellationToken);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithNoStore()
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("SearchAccounts")
            .WithSummary("Danh sách người dùng (Admin)")
            .WithDescription("Tìm theo email / họ tên; lọc role (Customer, Staff, ParkingLotOwner, SystemAdmin) và status; page từ 1, pageSize tối đa 100.")
            .Produces<AccountPage>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/api/identity/admin/accounts/{accountId:guid}/suspend", (
                Guid accountId, ICurrentUser currentUser, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
                ChangeStatusAsync(accountId, AccountStatusChange.Suspend, currentUser, httpContext, sender, cancellationToken))
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("SuspendAccount")
            .WithSummary("Đình chỉ tài khoản (Admin)")
            .WithDescription("Đình chỉ và đăng xuất người dùng khỏi mọi thiết bị. Không áp dụng cho chính mình và Admin khác.")
            .Produces<ChangedAccountStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/identity/admin/accounts/{accountId:guid}/activate", (
                Guid accountId, ICurrentUser currentUser, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
                ChangeStatusAsync(accountId, AccountStatusChange.Activate, currentUser, httpContext, sender, cancellationToken))
            .RequireAuthorization(IdentityAuthorization.AdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("ActivateAccount")
            .WithSummary("Kích hoạt lại tài khoản (Admin)")
            .WithDescription("Mở lại tài khoản bị đình chỉ / vô hiệu hoá. Tài khoản chưa xác thực email không kích hoạt thay được (409).")
            .Produces<ChangedAccountStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
