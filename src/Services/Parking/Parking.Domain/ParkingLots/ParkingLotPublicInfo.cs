namespace ParkingSystem.Parking.Domain.ParkingLots;

// Owner-provided, public-facing descriptive information. Blank means 'not published', not false.
// Photo URLs are references to already-approved local public assets, NOT uploaded private media.
public sealed record ParkingLotPublicInfo(
    string? Description,
    string? EntryInstructions,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> PhotoUrls,
    bool PhotosAreIllustrative,
    IReadOnlyList<string> PaymentMethods,
    int? EvChargingPoints);
