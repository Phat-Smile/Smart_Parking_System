using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.WebAPI.Security;

/// <summary>
/// Trích xuất danh tính người dùng hiện tại từ JWT và HTTP Context.
/// </summary>
internal sealed class CurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public Guid? ActorId =>
        Guid.TryParse(HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Email =>
        HttpContext?.User.FindFirst("email")?.Value;

    public string Role =>
        HttpContext?.User.FindFirst("role")?.Value ?? "System";

    public string? IpAddress
    {
        get
        {
            var context = HttpContext;
            if (context is null) return null;

            // Ưu tiên đọc IP chuyển tiếp từ YARP Gateway
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var ip = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(ip)) return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
