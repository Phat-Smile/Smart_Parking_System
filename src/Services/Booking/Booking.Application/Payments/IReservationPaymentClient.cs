using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Payments;

/// <summary>
/// Application port for executing and querying reservation payment transactions (wallet debit, state query, refund).
/// </summary>
public interface IReservationPaymentClient
{
    Task<Result<ReservationDebitResult>> DebitAsync(
        Guid reservationId,
        Guid accountId,
        Guid lotId,
        long amountVnd,
        CancellationToken cancellationToken = default);

    Task<Result<ReservationDebitState>> GetDebitStateAsync(
        Guid reservationId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<Result<ReservationDebitState>> RefundAsync(
        Guid reservationId,
        Guid accountId,
        long amountVnd,
        string reason,
        CancellationToken cancellationToken = default);
}

