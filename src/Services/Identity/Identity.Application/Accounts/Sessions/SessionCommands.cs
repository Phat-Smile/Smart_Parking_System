using System.Net;
using System.Net.Sockets;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Identity.Application.Accounts.Sessions;

// CurrentSessionId lấy từ claim "sid" của access token, để đánh dấu thiết bị đang dùng.
public sealed record ListSessionsQuery(Guid AccountId, Guid? CurrentSessionId) : IRequest<Result<IReadOnlyList<SessionInfo>>>;

public sealed record SessionInfo(Guid Id, string Device, string? IpAddress, DateTimeOffset CreatedAtUtc, DateTimeOffset LastActivityAtUtc, bool IsCurrent);

public sealed record RevokeSessionCommand(Guid AccountId, Guid SessionId) : IRequest<Result<RevokedSession>>;

public sealed record RevokedSession(Guid SessionId, string Message);

public static class SessionErrors
{
    public static readonly Error SessionNotFound =
        new("identity.session_not_found", "Không tìm thấy phiên đăng nhập này.");
}

public sealed class ListSessionsQueryHandler(IAuthSessionRepository sessions, IClock clock)
    : IRequestHandler<ListSessionsQuery, Result<IReadOnlyList<SessionInfo>>>
{
    public async Task<Result<IReadOnlyList<SessionInfo>>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var active = await sessions.ListActiveForAccountAsync(request.AccountId, clock.UtcNow, cancellationToken);
        IReadOnlyList<SessionInfo> items = active
            .Select(s => new SessionInfo(
                s.Id,
                DeviceDescription.FromUserAgent(s.DeviceInfo),
                DeviceDescription.MaskIp(s.IpAddress),
                s.CreatedAtUtc,
                s.LastActivityAtUtc,
                s.Id == request.CurrentSessionId))
            .ToList();
        return Result<IReadOnlyList<SessionInfo>>.Success(items);
    }
}

public sealed class RevokeSessionCommandHandler(IAuthSessionRepository sessions, IAuthSessionManager sessionManager)
    : IRequestHandler<RevokeSessionCommand, Result<RevokedSession>>
{
    public const string RevokedReason = "UserRevoked";

    public async Task<Result<RevokedSession>> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        // Phiên của người khác trả cùng lỗi với phiên không tồn tại để không lộ id phiên của ai.
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null || session.AccountId != request.AccountId)
            return Result<RevokedSession>.Failure(SessionErrors.SessionNotFound);

        await sessionManager.RevokeSessionAsync(session.Id, RevokedReason, cancellationToken);
        return Result<RevokedSession>.Success(new RevokedSession(session.Id, "Đã đăng xuất thiết bị này."));
    }
}

// Hiển thị cho người dùng nhận ra thiết bị, không cần chính xác tuyệt đối như thư viện phân tích User-Agent.
public static class DeviceDescription
{
    public static string FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Thiết bị không xác định";

        // Thứ tự quan trọng: Edge và Opera chứa chữ "Chrome", Chrome chứa chữ "Safari".
        var browser = userAgent switch
        {
            _ when userAgent.Contains("Edg/", StringComparison.Ordinal) => "Edge",
            _ when userAgent.Contains("OPR/", StringComparison.Ordinal) => "Opera",
            _ when userAgent.Contains("Firefox/", StringComparison.Ordinal) => "Firefox",
            _ when userAgent.Contains("Chrome/", StringComparison.Ordinal) => "Chrome",
            _ when userAgent.Contains("Safari/", StringComparison.Ordinal) => "Safari",
            _ when userAgent.Contains("PostmanRuntime", StringComparison.Ordinal) => "Postman",
            _ when userAgent.StartsWith("curl/", StringComparison.Ordinal) => "curl",
            _ => "Trình duyệt khác"
        };

        // Android chứa chữ "Linux", iPhone chứa chữ "Mac OS X".
        var os = userAgent switch
        {
            _ when userAgent.Contains("Windows", StringComparison.Ordinal) => "Windows",
            _ when userAgent.Contains("Android", StringComparison.Ordinal) => "Android",
            _ when userAgent.Contains("iPhone", StringComparison.Ordinal) || userAgent.Contains("iPad", StringComparison.Ordinal) => "iOS",
            _ when userAgent.Contains("Mac OS X", StringComparison.Ordinal) => "macOS",
            _ when userAgent.Contains("Linux", StringComparison.Ordinal) => "Linux",
            _ => null
        };

        return os is null ? browser : $"{browser} trên {os}";
    }

    // IP là dữ liệu cá nhân: chỉ giữ phần đủ để người dùng nhận ra mạng của mình.
    public static string? MaskIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var ip))
            return null;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.x.x";
        }

        // IPv6: giữ 4 nhóm đầu (phần mạng), che 4 nhóm sau (phần thiết bị).
        var bytes6 = ip.GetAddressBytes();
        var network = Enumerable.Range(0, 4).Select(i => ((bytes6[i * 2] << 8) | bytes6[i * 2 + 1]).ToString("x"));
        return string.Join(':', network) + ":x:x:x:x";
    }
}
