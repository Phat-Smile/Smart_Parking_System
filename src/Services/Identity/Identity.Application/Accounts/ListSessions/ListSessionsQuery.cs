using System.Text.RegularExpressions;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Identity.Application.Accounts.ListSessions;

public sealed record ListSessionsQuery(Guid AccountId, Guid? CurrentSessionId) : IRequest<Result<ListSessionsResult>>;

public sealed record SessionInfo(
    Guid Id,
    string Device,
    string IpAddress,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    bool IsCurrent);

public sealed record ListSessionsResult(IReadOnlyList<SessionInfo> Sessions);

public sealed class ListSessionsQueryHandler(
    IAuthSessionRepository authSessions,
    IClock clock) : IRequestHandler<ListSessionsQuery, Result<ListSessionsResult>>
{
    public async Task<Result<ListSessionsResult>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sessions = await authSessions.ListActiveForAccountAsync(request.AccountId, now, cancellationToken);

        var infos = sessions.Select(s => new SessionInfo(
            s.Id,
            ParseUserAgent(s.DeviceInfo),
            MaskIpAddress(s.IpAddress),
            s.CreatedAtUtc,
            s.LastActivityAtUtc,
            s.Id == request.CurrentSessionId)).ToList();

        return Result<ListSessionsResult>.Success(new ListSessionsResult(infos));
    }

    internal static string ParseUserAgent(string? ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "Thiết bị không xác định";

        var browser = ua switch
        {
            _ when ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) => "Edge",
            _ when ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) => "Chrome",
            _ when ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) => "Firefox",
            _ when ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) => "Safari",
            _ => "Trình duyệt"
        };

        var os = ua switch
        {
            _ when ua.Contains("Android", StringComparison.OrdinalIgnoreCase) => "Android",
            _ when ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) => "iOS",
            _ when ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) => "Windows",
            _ when ua.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) => "macOS",
            _ when ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) => "Linux",
            _ => "thiết bị không rõ"
        };

        return $"{browser} trên {os}";
    }

    internal static string MaskIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "N/A";

        // IPv4: mask last 2 octets → 192.168.x.x
        if (Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
        {
            var parts = ip.Split('.');
            return $"{parts[0]}.{parts[1]}.x.x";
        }

        // IPv6: mask last 4 groups → 2001:0db8:85a3:0000:x:x:x:x
        if (ip.Contains(':'))
        {
            var parts = ip.Split(':');
            if (parts.Length >= 4)
                return string.Join(":", parts.Take(4)) + ":x:x:x:x";
        }

        return ip;
    }
}
