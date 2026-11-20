using System.Globalization;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public static class ReservationCancelledNoticeText
{
    private static readonly NumberFormatInfo VndFormat = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalDigits = 0
    };

    public static string Build(ReservationCancelledIntegrationEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var reason = evt.CancelReason?.Trim();

        if (string.Equals(reason, "NoShow", StringComparison.OrdinalIgnoreCase))
        {
            return evt.ForfeitedAmount > 0
                ? $"Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn. Khoản trả trước {FormatCurrency(evt.ForfeitedAmount)} không được hoàn."
                : "Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn.";
        }

        if (string.Equals(reason, "CustomerCancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (evt.ForfeitedAmount > 0)
            {
                return $"Bạn đã hủy đặt chỗ sau mốc được hoàn tiền. Khoản trả trước {FormatCurrency(evt.ForfeitedAmount)} không được hoàn.";
            }

            if (evt.RefundAmount > 0)
            {
                return $"Bạn đã hủy đặt chỗ. {FormatCurrency(evt.RefundAmount)} sẽ được hoàn về ví.";
            }

            return "Bạn đã hủy đặt chỗ.";
        }

        if (string.Equals(reason, "NoSuitableSlot", StringComparison.OrdinalIgnoreCase))
        {
            var baseText = "Đặt chỗ đã bị hủy vì bãi không còn chỗ phù hợp.";
            return evt.RefundAmount > 0
                ? $"{baseText} {FormatCurrency(evt.RefundAmount)} sẽ được hoàn về ví."
                : baseText;
        }

        if (string.Equals(reason, "PaymentFailed", StringComparison.OrdinalIgnoreCase))
        {
            return "Đặt chỗ đã bị hủy vì thanh toán không thành công.";
        }

        return "Đặt chỗ của bạn đã được hủy.";
    }

    private static string FormatCurrency(decimal amount) => $"{amount.ToString("N0", VndFormat)} đ";
}
