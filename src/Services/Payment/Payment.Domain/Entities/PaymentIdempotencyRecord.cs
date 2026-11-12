namespace ParkingSystem.Payment.Domain.Entities;

// Lưu khóa chống trừ tiền hai lần
public class PaymentIdempotencyRecord
{
    public Guid Id { get; private set; }                     // Mã bản ghi chống trùng
    public Guid AccountId { get; private set; }              // Mã tài khoản người gửi
    public string Operation { get; private set; } = null!;   // Tên thao tác: pay_fee_wallet
    public string IdempotencyKey { get; private set; } = null!; // Khóa chống trùng từ client
    public string RequestHash { get; private set; } = null!; // Băm dữ liệu để kiểm tra sửa lén
    public string Status { get; private set; } = null!;      // Trạng thái: Pending, Success, Failed
    public string? ResponsePayload { get; private set; }     // Dữ liệu cũ để phát lại
    public int StatusCode { get; private set; }              // Mã HTTP cũ: 200, 400, 409
    public DateTimeOffset CreatedAtUtc { get; private set; } // Giờ bắt đầu nhận yêu cầu
    public DateTimeOffset LockedUntilUtc { get; private set; }// Hạn khóa tạm thời 30 giây

    private PaymentIdempotencyRecord() { }

    // Tạo khóa ở trạng thái đang xử lý
    public static PaymentIdempotencyRecord CreatePending(
        Guid accountId,
        string operation,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset nowUtc)
    {
        return new PaymentIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Operation = operation,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            Status = "Pending",
            CreatedAtUtc = nowUtc,
            LockedUntilUtc = nowUtc.AddSeconds(30),
            StatusCode = 0
        };
    }

    // Đánh dấu giao dịch đã trừ tiền xong
    public void MarkSuccess(string responsePayload, int statusCode)
    {
        Status = "Success";
        ResponsePayload = responsePayload;
        StatusCode = statusCode;
    }

    // Đánh dấu giao dịch bị từ chối
    public void MarkFailed(string responsePayload, int statusCode)
    {
        Status = "Failed";
        ResponsePayload = responsePayload;
        StatusCode = statusCode;
    }
}
