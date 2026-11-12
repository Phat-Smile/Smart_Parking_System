namespace ParkingSystem.Payment.Application.Common.Constants;

public static class TopupStatusNames
{
    // Giao dich dang cho ket qua tu cong thanh toan
    public const string Pending = "Pending";

    // Giao dich nap tien thanh cong va da cong tien vao vi
    public const string Succeeded = "Succeeded";

    // Giao dich nap tien that bai
    public const string Failed = "Failed";

    // Link thanh toan da qua han, ket qua VNPay den muon van co the cap nhat giao dich.
    public const string Expired = "Expired";
}
