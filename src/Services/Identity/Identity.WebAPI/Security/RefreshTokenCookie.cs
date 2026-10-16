using Microsoft.Extensions.Options;

namespace ParkingSystem.Identity.WebAPI.Security;

public sealed class RefreshTokenCookieOptions
{
    public const string SectionName = "Security:RefreshCookie";

    public string Name { get; set; } = "sp_refresh";

    // Chỉ gửi tới refresh/logout, không đi kèm mọi request khác.
    public string Path { get; set; } = "/api/identity/accounts";

    // Để trống thì tự chọn: tắt ở Development (local chạy http), bật ở mọi môi trường khác.
    public bool? Secure { get; set; }
}

// Quyết định 10/10: refresh token nằm trong cookie HttpOnly để JS (và XSS) không đọc được; FE chỉ giữ access token trong bộ nhớ.
internal static class RefreshTokenCookie
{
    public static IServiceCollection AddRefreshTokenCookie(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RefreshTokenCookieOptions>(configuration.GetSection(RefreshTokenCookieOptions.SectionName));
        return services;
    }

    public static void Set(HttpContext httpContext, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        var options = Options(httpContext);
        httpContext.Response.Cookies.Append(options.Name, refreshToken, Build(httpContext, options, expiresAtUtc));
    }

    public static void Clear(HttpContext httpContext)
    {
        var options = Options(httpContext);
        httpContext.Response.Cookies.Delete(options.Name, Build(httpContext, options, expiresAtUtc: null));
    }

    // Cookie trước; không có thì nhận token trong body cho Postman, test và client không phải trình duyệt.
    public static string? Read(HttpContext httpContext, string? bodyToken)
    {
        var options = Options(httpContext);
        var cookieToken = httpContext.Request.Cookies[options.Name];
        if (!string.IsNullOrWhiteSpace(cookieToken))
            return cookieToken;

        return string.IsNullOrWhiteSpace(bodyToken) ? null : bodyToken;
    }

    private static RefreshTokenCookieOptions Options(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOptions<RefreshTokenCookieOptions>>().Value;

    private static CookieOptions Build(HttpContext httpContext, RefreshTokenCookieOptions options, DateTimeOffset? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = options.Secure ?? !httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
        // Strict chặn CSRF; FE localhost:3000 và Gateway localhost:5000 cùng site nên cookie vẫn được gửi.
        SameSite = SameSiteMode.Strict,
        Path = options.Path,
        Expires = expiresAtUtc,
        IsEssential = true
    };
}
