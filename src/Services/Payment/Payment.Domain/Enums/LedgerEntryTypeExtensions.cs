namespace ParkingSystem.Payment.Domain.Enums;

public static class LedgerEntryTypeExtensions
{
    // Áp cọc và tịch thu cọc chỉ mang tính giải thích, không đổi số dư ví
    public static bool AffectsBalance(this LedgerEntryType entryType)
    {
        return entryType switch
        {
            LedgerEntryType.PrepaymentApplied => false,
            LedgerEntryType.PrepaymentForfeited => false,
            _ => true
        };
    }
}
