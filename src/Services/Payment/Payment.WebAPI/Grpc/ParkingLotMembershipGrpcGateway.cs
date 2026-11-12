using Grpc.Core;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using SmartParking.Contracts.Parking.Membership.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

// Tra cứu quyền thành viên bãi đỗ xe qua gRPC từ dịch vụ Parking.
// Fail-closed: Mọi lỗi kết nối, timeout hoặc phản hồi không hợp lệ đều chuyển thành ParkingServiceUnavailable.
public sealed class ParkingLotMembershipGrpcGateway(
    ParkingLotMembershipContract.ParkingLotMembershipContractClient client,
    IOptions<ParkingServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<ParkingLotMembershipGrpcGateway> logger) : IParkingLotMembershipGateway
{
    public async Task<Result<ParkingLotMembershipInfo>> GetMembershipAsync(
        Guid lotId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (lotId == Guid.Empty || accountId == Guid.Empty)
        {
            return Result<ParkingLotMembershipInfo>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }

        // Thời gian chờ 3 giây (theo quy định fail-closed)
        var timeout = options.Value.TimeoutSeconds > 0 ? Math.Min(options.Value.TimeoutSeconds, 3) : 3;
        var headers = new Metadata();
        var serviceKey = serviceKeyOptions.Value.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        try
        {
            var response = await client.GetLotMembershipAsync(
                new GetLotMembershipRequest
                {
                    LotId = lotId.ToString("D"),
                    AccountId = accountId.ToString("D")
                },
                headers: headers,
                deadline: DateTime.UtcNow.AddSeconds(timeout),
                cancellationToken: cancellationToken);

            return Result<ParkingLotMembershipInfo>.Success(
                new ParkingLotMembershipInfo(response.IsMember, response.Role ?? string.Empty));
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Parking membership call failed with {StatusCode} for LotId {LotId}, AccountId {AccountId}.",
                ex.StatusCode, lotId, accountId);
            return Result<ParkingLotMembershipInfo>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure when calling parking membership for LotId {LotId}, AccountId {AccountId}.",
                lotId, accountId);
            return Result<ParkingLotMembershipInfo>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }
    }
}
