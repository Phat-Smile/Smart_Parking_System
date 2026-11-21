using Microsoft.Extensions.Logging;
using ParkingSystem.Notification.Application.Common.Identity;

namespace ParkingSystem.Notification.Infrastructure.Identity;

public sealed class IdentityAccountGrpcClient(
    ILogger<IdentityAccountGrpcClient> logger) : IIdentityAccountClient
{
    public Task<AccountContactInfo?> GetAccountContactInfoAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // Safe fallback when Identity gRPC is not configured or in tests.
        return Task.FromResult<AccountContactInfo?>(null);
    }
}
