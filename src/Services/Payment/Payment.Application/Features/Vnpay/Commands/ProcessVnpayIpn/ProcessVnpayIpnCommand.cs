using MediatR;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

/// <summary>
/// gom toàn bộ tham số do VNPay gửi sang (mã giao dịch, số tiền, chữ ký...)
/// Đưa dữ liệu từ Controller vào tầng nghiệp vụ (Handler) để xử lý, giúp Controller không bị ôm đồm logic.
/// </summary>
public class ProcessVnpayIpnCommand : IRequest<VnpayIpnResponseDto>
{
    // 'init' -> chỉ được gán giá trị 1 lần duy nhất lúc khởi tạo, sau đó không ai được sửa
    public IReadOnlyDictionary<string, string> QueryParameters { get; init; }
    public ProcessVnpayIpnCommand(IReadOnlyDictionary<string, string> queryParameters)
    {
        QueryParameters = queryParameters;
    }
}
