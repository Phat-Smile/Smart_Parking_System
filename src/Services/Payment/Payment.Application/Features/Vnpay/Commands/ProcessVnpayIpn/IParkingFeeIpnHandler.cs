namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

// Tiếp nhận kết quả thanh toán tiền gửi xe từ VNPay
public interface IParkingFeeIpnHandler
{
    // Xử lý thông báo thanh toán gửi xe
    Task<VnpayIpnResponseDto> HandleAsync(
        IReadOnlyDictionary<string, string> query,
        string txnRef,
        CancellationToken cancellationToken);
}
