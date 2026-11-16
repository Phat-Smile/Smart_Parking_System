namespace ParkingSystem.Payment.Application.Common.Options;

public class VnpayOptions
{
    public const string SectionName = "Vnpay";

    public string Mode { get; set; } = "Sandbox";
    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ReturnUrl { get; set; } = "http://localhost:3000/payment/vnpay-return";
    public string ParkingReturnUrl { get; set; } = "http://localhost:3000/payment/parking-return";
    public int PaymentExpiryMinutes { get; set; } = 15;
}
