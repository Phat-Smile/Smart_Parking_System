namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

/// <summary>
/// Gói tin phản hồi chuẩn theo Hợp đồng kỹ thuật của cổng thanh toán VNPay IPN.
/// Khi VNPay gửi Webhook báo kết quả, hệ thống của ta phải trả về JSON đúng định dạng này.
/// </summary>
public class VnpayIpnResponseDto
{
    public static class Codes
    {
        public const string Success = "00";
        public const string OrderNotFound = "01";
        public const string AlreadyConfirmed = "02";
        public const string InvalidAmount = "04";
        public const string InvalidSignature = "97";
        public const string UnknownError = "99";
    }

    public static class Messages
    {
        public const string Success = "Confirm Success";
        public const string OrderNotFound = "Order not found";
        public const string AlreadyConfirmed = "Order already confirmed";
        public const string InvalidAmount = "Invalid amount";
        public const string InvalidSignature = "Invalid signature";
        public const string UnknownError = "Unknown error";
    }

    // Dữ liệu JSON trả về cho VNPay phải có 2 trường: RspCode và Message
    public string RspCode { get; init; }
    public string Message { get; init; }

    public VnpayIpnResponseDto(string rspCode, string message)
    {
        RspCode = rspCode;
        Message = message;
    }

    public static VnpayIpnResponseDto Success() => new(Codes.Success, Messages.Success);

    public static VnpayIpnResponseDto OrderNotFound() => new(Codes.OrderNotFound, Messages.OrderNotFound);
    public static VnpayIpnResponseDto AlreadyConfirmed() => new(Codes.AlreadyConfirmed, Messages.AlreadyConfirmed);
    public static VnpayIpnResponseDto InvalidAmount() => new(Codes.InvalidAmount, Messages.InvalidAmount);
    public static VnpayIpnResponseDto InvalidSignature() => new(Codes.InvalidSignature, Messages.InvalidSignature);
    public static VnpayIpnResponseDto UnknownError() => new(Codes.UnknownError, Messages.UnknownError);
}
