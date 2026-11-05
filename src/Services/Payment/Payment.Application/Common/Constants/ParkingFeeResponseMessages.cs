namespace ParkingSystem.Payment.Application.Common.Constants;

// Thông điệp kết quả tính phí đỗ xe
public static class ParkingFeeResponseMessages
{
    // Thông điệp tính phí thành công
    public const string CalculateFeeSuccessfully = "Calculate parking fee successfully";

    // Thông điệp thanh toán ví thành công
    public const string PayFeeByWalletSuccessfully = "Pay parking fee by wallet successfully";

    // Thông điệp tạo thanh toán VNPay thành công
    public const string CreateVnPayPaymentSuccessfully = "Create VNPay payment successfully";

    // Thông điệp tái sử dụng mã thanh toán VNPay cũ
    public const string VnPayPaymentAlreadyExists = "VNPay payment already exists";

    // Thông điệp tra cứu trạng thái thanh toán thành công
    public const string GetParkingFeePaymentSuccessfully = "Get parking fee payment successfully";
}
