using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.BuildingBlocks.Results;
using SmartParking.Contracts.Parking.Availability.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// gRPC adapter implementation of <see cref="IParkingAvailabilityClient"/> connecting to Parking service.
/// Transmits service key authentication metadata (X-Service-Key) when configured.
/// </summary>
public sealed class ParkingAvailabilityGrpcClient(
    ParkingAvailabilityContract.ParkingAvailabilityContractClient client,
    IOptions<ParkingServiceOptions> options,
    IOptions<ServiceKeyOptions>? serviceKeyOptions,
    ILogger<ParkingAvailabilityGrpcClient> logger) : IParkingAvailabilityClient
{
    public ParkingAvailabilityGrpcClient(
        ParkingAvailabilityContract.ParkingAvailabilityContractClient client,
        IOptions<ParkingServiceOptions> options,
        ILogger<ParkingAvailabilityGrpcClient> logger)
        : this(client, options, null, logger)
    {
    }

    public async Task<Result<int>> GetAvailableSlotCountAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        if (lotId == Guid.Empty)
        {
            return Result<int>.Failure(ParkingAvailabilityErrors.EmptyLotId);
        }

        var timeout = options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 5;
        var deadline = DateTime.UtcNow.AddSeconds(timeout);

        var headers = new Metadata();
        var serviceKey = serviceKeyOptions?.Value?.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        try
        {
            var request = new GetAvailableSlotCountRequest { LotId = lotId.ToString("D") };
            var response = await client.GetAvailableSlotCountAsync(
                request,
                headers: headers,
                deadline: deadline,
                cancellationToken: cancellationToken);

            if (response.AvailableSlotCount < 0)
            {
                logger.LogWarning("Parking service returned negative available slot count: {Count} for lot {LotId}.", response.AvailableSlotCount, lotId);
                return Result<int>.Failure(ParkingAvailabilityErrors.InvalidResponse);
            }

            return Result<int>.Success(response.AvailableSlotCount);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return Result<int>.Failure(ParkingAvailabilityErrors.LotNotFound);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            return Result<int>.Failure(ParkingAvailabilityErrors.EmptyLotId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            logger.LogError(ex, "Service key authentication failed when querying Parking service for lot {LotId}.", lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.ServiceUnavailable);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Call to Parking service timed out for lot {LotId}.", lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.DeadlineExceeded);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            logger.LogInformation("Call to Parking service was cancelled for lot {LotId}.", lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.RequestCancelled);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            logger.LogError(ex, "Parking service is unavailable when querying lot {LotId}.", lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.ServiceUnavailable);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC call to Parking service failed with StatusCode {StatusCode} for lot {LotId}.", ex.StatusCode, lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.CommunicationFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<int>.Failure(ParkingAvailabilityErrors.RequestCancelled);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred when calling Parking service for lot {LotId}.", lotId);
            return Result<int>.Failure(ParkingAvailabilityErrors.CommunicationFailed);
        }
    }
}
