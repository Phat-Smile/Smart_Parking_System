using Microsoft.AspNetCore.Http;
using ParkingSystem.BuildingBlocks.Identity;

namespace ParkingSystem.AspNetCore.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? SubjectId => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
    public string? SessionId => httpContextAccessor.HttpContext?.User.FindFirst("sid")?.Value;

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
