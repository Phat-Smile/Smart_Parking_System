using Grpc.Core;
using MediatR;
using ParkingSystem.Identity.Application.Accounts.LookupStaff;
using SmartParking.Contracts.Identity.Staff.V1;

namespace ParkingSystem.Identity.WebAPI.Grpc;

public sealed class IdentityStaffGrpcService(
    ISender sender,
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
}
