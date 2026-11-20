using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.Common.Identity;
using SmartParking.Contracts.Identity.Staff.V1;

namespace ParkingSystem.Parking.WebAPI.Grpc;

public sealed class IdentityStaffGrpcClient(
    IdentityStaffContract.IdentityStaffContractClient client,
    IOptions<IdentityServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<IdentityStaffGrpcClient> logger) : IStaffAccountLookup, IStaffAccountEnricher
{
    public async Task<(bool IsFound, Guid AccountId)> LookupStaffByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, Guid.Empty);

        LookupStaffAccountResponse response;
        try
        {
            response = await client.LookupStaffAccountAsync(
                new LookupStaffAccountRequest { Email = email.Trim().ToLowerInvariant() },
                headers: BuildHeaders(),
                deadline: DateTime.UtcNow.AddSeconds(options.Value.TimeoutSeconds),
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException exception)
        {
            LogRpcFailure(exception.StatusCode);
            return (false, Guid.Empty);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Staff lookup in Identity failed unexpectedly.");
            return (false, Guid.Empty);
        }

        if (!response.IsFound || !Guid.TryParseExact(response.AccountId, "D", out var accountId) || accountId == Guid.Empty)
        {
            return (false, Guid.Empty);
        }

        return (true, accountId);
    }

    public async Task<IReadOnlyDictionary<Guid, (string DisplayName, string MaskedEmail)>> BatchLookupAccountsAsync(
        IReadOnlyList<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
            return new Dictionary<Guid, (string, string)>();

        var distinctIds = accountIds.Distinct().ToList();
        var request = new BatchLookupAccountsRequest();
        foreach (var id in distinctIds)
        {
            request.AccountIds.Add(id.ToString("D"));
        }

        try
        {
            var response = await client.BatchLookupAccountsAsync(
                request,
                headers: BuildHeaders(),
                deadline: DateTime.UtcNow.AddSeconds(options.Value.TimeoutSeconds),
                cancellationToken: cancellationToken);

            var result = new Dictionary<Guid, (string, string)>();
            foreach (var account in response.Accounts)
            {
                if (Guid.TryParseExact(account.AccountId, "D", out var accountId))
                {
                    result[accountId] = (account.DisplayName, MaskEmail(account.Email));
                }
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Batch staff account lookup in Identity failed.");
            return new Dictionary<Guid, (string, string)>();
        }
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "***@***";

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        return at <= 0 ? "***" : $"{trimmed[0]}***{trimmed[at..]}";
    }

    private Metadata BuildHeaders()
    {
        var headers = new Metadata();
        var serviceKey = serviceKeyOptions.Value.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);

        return headers;
    }

    private void LogRpcFailure(StatusCode status)
    {
        var level = status switch
        {
            StatusCode.InvalidArgument => LogLevel.Information,
            StatusCode.Unavailable or StatusCode.DeadlineExceeded => LogLevel.Warning,
            _ => LogLevel.Error
        };
        logger.Log(level, "Staff lookup in Identity failed with status {StatusCode}.", status);
    }
}
