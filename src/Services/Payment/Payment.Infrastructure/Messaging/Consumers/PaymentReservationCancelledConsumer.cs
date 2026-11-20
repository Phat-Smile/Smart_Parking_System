using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RecordPrepaymentForfeited;

namespace ParkingSystem.Payment.Infrastructure.Messaging.Consumers;

// Nhận sự kiện hủy đặt chỗ từ Booking để ghi mất cọc
public sealed class PaymentReservationCancelledConsumer(
    ISender sender,
    ILogger<PaymentReservationCancelledConsumer> logger)
    : IConsumer<ReservationCancelledIntegrationEvent>
{
    private static readonly HashSet<string> PermanentErrorCodes = new(StringComparer.Ordinal)
    {
        ReservationDepositErrors.ReservationIdInvalid.Code,
        ReservationDepositErrors.UserIdInvalid.Code,
        ReservationDepositErrors.LotIdInvalid.Code,
        ReservationDepositErrors.AmountInvalid.Code,
        ReservationDepositErrors.WalletNotFound.Code,
        ReservationDepositErrors.ReservationNotDebited.Code,
        ReservationDepositErrors.PrepaymentAlreadySettled.Code,
        ReservationDepositErrors.ForfeitAmountExceedsDeposit.Code
    };

    public async Task Consume(ConsumeContext<ReservationCancelledIntegrationEvent> context)
    {
        var message = context.Message;

        // Sự kiện không có tiền cọc bị mất thì bỏ qua
        if (message.ForfeitedAmount == 0m)
        {
            logger.LogInformation(
                "Reservation {ReservationId} cancelled with no forfeited deposit.",
                message.ReservationId);
            return;
        }

        // Chặn dữ liệu âm bất thường từ phía gửi sự kiện
        if (message.ForfeitedAmount < 0m)
        {
            logger.LogWarning(
                "Reservation {ReservationId} has negative ForfeitedAmount={Amount}. Ignoring.",
                message.ReservationId,
                message.ForfeitedAmount);
            return;
        }

        var command = new RecordPrepaymentForfeitedCommand(
            message.ReservationId,
            message.UserId,
            message.LotId,
            message.ForfeitedAmount,
            message.CancelReason,
            message.CancelledAtUtc);

        var result = await sender.Send(command, context.CancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Processed PrepaymentForfeited entry {EntryId} for reservation {ReservationId}.",
                result.Value,
                message.ReservationId);
            return;
        }

        var errorCode = result.Error?.Code ?? string.Empty;
        if (PermanentErrorCodes.Contains(errorCode))
        {
            logger.LogWarning(
                "Permanent rejection recording forfeit for reservation {ReservationId}: {Code} - {Message}.",
                message.ReservationId,
                errorCode,
                result.Error?.Message);
            return;
        }

        // Lỗi ngoài danh sách vĩnh viễn phải ném ra để thử lại
        throw new InvalidOperationException(
            $"Retryable failure recording forfeit for reservation {message.ReservationId}: {errorCode} - {result.Error?.Message}");
    }
}
