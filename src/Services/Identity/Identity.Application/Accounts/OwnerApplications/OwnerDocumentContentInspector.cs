namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public static class OwnerDocumentContentInspector
{
    // PDF magic bytes: %PDF- (0x25, 0x50, 0x44, 0x46, 0x2D)
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46, 0x2D];

    // PNG magic bytes: \x89PNG\r\n\x1a\n (0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // JPEG magic bytes: 0xFF, 0xD8, 0xFF
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    public static string? DetectContentType(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length >= PdfMagic.Length && headerBytes[..PdfMagic.Length].SequenceEqual(PdfMagic))
        {
            return "application/pdf";
        }

        if (headerBytes.Length >= PngMagic.Length && headerBytes[..PngMagic.Length].SequenceEqual(PngMagic))
        {
            return "image/png";
        }

        if (headerBytes.Length >= JpegMagic.Length && headerBytes[..JpegMagic.Length].SequenceEqual(JpegMagic))
        {
            return "image/jpeg";
        }

        return null;
    }
}
