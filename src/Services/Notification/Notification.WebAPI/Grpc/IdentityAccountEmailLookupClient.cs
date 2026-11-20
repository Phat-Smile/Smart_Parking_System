using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Notification.Application.Common.Identity;
using SmartParking.Contracts.Identity.Staff.V1;

namespace ParkingSystem.Notification.WebAPI.Grpc;

public sealed class IdentityAccountEmailLookupClient(
    IdentityStaffContract.IdentityStaffContractClient client,
    IOptions<IdentityServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<IdentityAccountEmailLookupClient> logger) : IUserAccountEmailLookup
{
    public async Task<UserAccountEmailInfo?> LookupAccountEmailAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return null;

        var request = new BatchLookupAccountsRequest();
        request.AccountIds.Add(accountId.ToString("D"));

        var headers = new Metadata();
        var serviceKey = serviceKeyOptions.Value.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        var timeout = options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 5;

        try
        {
            var response = await client.BatchLookupAccountsAsync(
                request,
                headers: headers,
                deadline: DateTime.UtcNow.AddSeconds(timeout),
                cancellationToken: cancellationToken);

            var account = response.Accounts.FirstOrDefault(a =>
                string.Equals(a.AccountId, accountId.ToString("D"), StringComparison.OrdinalIgnoreCase));

            if (account is null)
                return null;

            return new UserAccountEmailInfo(accountId, account.Email, account.IsEmailVerified);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to lookup account email in Identity for AccountId {AccountId}.", accountId);
            return null;
        }
    }
}
