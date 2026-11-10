namespace ParkingSystem.Parking.Application.Common.Plate;

/// <summary>
/// Kết quả nhận diện biển số xe quang học từ hình ảnh.
/// </summary>
public sealed record PlateOcrResult(
    string RawPlate,
    string NormalizedPlate,
    double Confidence,
    string VehicleType,
    string? BoundingBox = null,
    string? ProcessingProvider = null);

/// <summary>
/// Giao diện dịch vụ AI trích xuất và nhận dạng biển số xe (ALPR / OCR Engine).
/// </summary>
public interface ILicensePlateOcrEngine
{
    Task<PlateOcrResult> RecognizePlateAsync(
        string imagePayload,
        string? lotId = null,
        CancellationToken cancellationToken = default);
}
