using Grpc.Core;
using MediatR;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.LookupStaff;
using ParkingSystem.Identity.Application.Profiles;
using SmartParking.Contracts.Identity.Staff.V1;

namespace ParkingSystem.Identity.WebAPI.Grpc;

public sealed class IdentityStaffGrpcService(
    ISender sender,
    IAccountRepository accounts,
    IProfileRepository profiles,
    ILogger<IdentityStaffGrpcService> logger) : IdentityStaffContract.IdentityStaffContractBase
{
    public override async Task<LookupStaffAccountResponse> LookupStaffAccount(
        LookupStaffAccountRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Email must not be empty."));
        }

        try
        {
            var result = await sender.Send(new LookupStaffAccountByEmailQuery(request.Email), context.CancellationToken);
            if (result.IsSuccess && result.Value!.IsFound)
            {
                return new LookupStaffAccountResponse
                {
                    IsFound = true,
                    AccountId = result.Value.AccountId.ToString("D")
                };
            }

            // Anti-enumeration: all non-staff / missing / suspended cases return IsFound = false with empty string.
            return new LookupStaffAccountResponse
            {
                IsFound = false,
                AccountId = string.Empty
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception)
        {
            // Email is personal data: log the failure, not the payload.
            logger.LogError(exception, "Staff account lookup failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Staff account lookup failed."));
        }
    }

    public override async Task<BatchLookupAccountsResponse> BatchLookupAccounts(
        BatchLookupAccountsRequest request,
        ServerCallContext context)
    {
        var validIds = request.AccountIds
            .Select(id => Guid.TryParseExact(id, "D", out var guid) ? guid : Guid.Empty)
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
        {
            return new BatchLookupAccountsResponse();
        }

        try
        {
            var accountsList = await accounts.FindByIdsAsync(validIds, context.CancellationToken);
            var fullNames = await profiles.FindFullNamesByAccountIdsAsync(validIds, context.CancellationToken);

            var response = new BatchLookupAccountsResponse();
            foreach (var acc in accountsList)
            {
                fullNames.TryGetValue(acc.Id, out var fullName);
                var displayName = !string.IsNullOrWhiteSpace(fullName)
                    ? fullName
                    : acc.Email.Split('@')[0];

                response.Accounts.Add(new AccountSummary
                {
                    AccountId = acc.Id.ToString("D"),
                    DisplayName = displayName,
                    Email = acc.Email
                });
            }

            return response;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Batch staff account lookup in Identity failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Batch staff account lookup failed."));
        }
    }
}
