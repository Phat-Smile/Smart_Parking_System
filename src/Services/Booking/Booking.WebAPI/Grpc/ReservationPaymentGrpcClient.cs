using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.BuildingBlocks.Results;
using SmartParking.Contracts.Payment.Reservation.V1;
using ProtoDebitOutcome = SmartParking.Contracts.Payment.Reservation.V1.DebitOutcome;
using ProtoDebitState = SmartParking.Contracts.Payment.Reservation.V1.DebitState;

namespace ParkingSystem.Booking.WebAPI.Grpc;

public sealed class ReservationPaymentGrpcClient(
    ReservationWalletContract.ReservationWalletContractClient client,
    IOptions<PaymentServiceOptions> options,
    IOptions<ServiceKeyOptions>? serviceKeyOptions,
    ILogger<ReservationPaymentGrpcClient> logger) : IReservationPaymentClient
{
    public async Task<Result<ReservationDebitResult>> DebitAsync(
        Guid reservationId,
        Guid accountId,
        Guid lotId,
        long amountVnd,
        CancellationToken cancellationToken = default)
    {
        var (deadline, headers) = BuildCallOptions();
        var request = new DebitReservationRequest
        {
            ReservationId = reservationId.ToString("D"),
            AccountId = accountId.ToString("D"),
            LotId = lotId.ToString("D"),
            AmountVnd = amountVnd,
            Purpose = PrepaymentPurpose.Deposit
        };

        try
        {
            var response = await client.DebitReservationAsync(
                request,
                headers: headers,
                deadline: deadline,
                cancellationToken: cancellationToken);

            var outcome = response.Outcome switch
            {
                ProtoDebitOutcome.Succeeded => ReservationDebitOutcome.Succeeded,
                ProtoDebitOutcome.InsufficientBalance => ReservationDebitOutcome.InsufficientBalance,
                ProtoDebitOutcome.WalletNotActive => ReservationDebitOutcome.WalletNotActive,
                ProtoDebitOutcome.AmountMismatch => ReservationDebitOutcome.AmountMismatch,
                _ => ReservationDebitOutcome.Unspecified
            };

            return Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                outcome,
                response.LedgerEntryId,
                response.BalanceAfterVnd));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Call to Payment service timed out.");
            return Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.DeadlineExceeded);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.Aborted)
        {
            logger.LogError(ex, "Payment service is unavailable (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
        {
            logger.LogError(ex, "Service authentication failed with Payment service (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.ServiceAuthFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Call to Payment service failed.");
            return Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.CommunicationFailed);
        }
    }

    public async Task<Result<ReservationDebitState>> GetDebitStateAsync(
        Guid reservationId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var (deadline, headers) = BuildCallOptions();
        var request = new GetReservationDebitRequest
        {
            ReservationId = reservationId.ToString("D"),
            AccountId = accountId.ToString("D")
        };

        try
        {
            var response = await client.GetReservationDebitAsync(
                request,
                headers: headers,
                deadline: deadline,
                cancellationToken: cancellationToken);

            var state = response.State switch
            {
                ProtoDebitState.NotDebited => ReservationDebitState.NotDebited,
                ProtoDebitState.Debited => ReservationDebitState.Debited,
                ProtoDebitState.Refunded => ReservationDebitState.Refunded,
                _ => ReservationDebitState.Unspecified
            };

            return Result<ReservationDebitState>.Success(state);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Call to Payment service timed out.");
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.DeadlineExceeded);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.Aborted)
        {
            logger.LogError(ex, "Payment service is unavailable (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
        {
            logger.LogError(ex, "Service authentication failed with Payment service (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.ServiceAuthFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Call to Payment service failed.");
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.CommunicationFailed);
        }
    }

    public async Task<Result<ReservationDebitState>> RefundAsync(
        Guid reservationId,
        Guid accountId,
        long amountVnd,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var (deadline, headers) = BuildCallOptions();
        var request = new RefundReservationRequest
        {
            ReservationId = reservationId.ToString("D"),
            AccountId = accountId.ToString("D"),
            AmountVnd = amountVnd,
            Reason = reason
        };

        try
        {
            var response = await client.RefundReservationAsync(
                request,
                headers: headers,
                deadline: deadline,
                cancellationToken: cancellationToken);

            var state = response.State switch
            {
                ProtoDebitState.Refunded => ReservationDebitState.Refunded,
                ProtoDebitState.Debited => ReservationDebitState.Debited,
                ProtoDebitState.NotDebited => ReservationDebitState.NotDebited,
                _ => ReservationDebitState.Unspecified
            };

            return Result<ReservationDebitState>.Success(state);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Call to Payment service timed out.");
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.DeadlineExceeded);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.Aborted)
        {
            logger.LogError(ex, "Payment service is unavailable (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
        {
            logger.LogError(ex, "Service authentication failed with Payment service (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.ServiceAuthFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Call to Payment service failed.");
            return Result<ReservationDebitState>.Failure(ReservationPaymentErrors.CommunicationFailed);
        }
    }

    private (DateTime Deadline, Metadata Headers) BuildCallOptions()
    {
        var timeout = options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 5;
        var deadline = DateTime.UtcNow.AddSeconds(timeout);

        var headers = new Metadata();
        var serviceKey = serviceKeyOptions?.Value?.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        return (deadline, headers);
    }
}
