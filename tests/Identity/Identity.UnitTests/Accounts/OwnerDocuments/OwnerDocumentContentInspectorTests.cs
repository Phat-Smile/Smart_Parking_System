using System.Text;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

public sealed class OwnerDocumentContentInspectorTests
{
    [Fact]
    public void DetectContentType_valid_pdf_bytes_returns_application_pdf()
    {
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ");
        var mime = OwnerDocumentContentInspector.DetectContentType(pdfBytes);

        Assert.Equal("application/pdf", mime);
    }

    [Fact]
    public void DetectContentType_valid_png_bytes_returns_image_png()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        var mime = OwnerDocumentContentInspector.DetectContentType(pngHeader);

        Assert.Equal("image/png", mime);
    }

    [Fact]
    public void DetectContentType_valid_jpeg_bytes_returns_image_jpeg()
    {
        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
        var mime = OwnerDocumentContentInspector.DetectContentType(jpegHeader);

        Assert.Equal("image/jpeg", mime);
    }

    [Fact]
    public void DetectContentType_spoofed_file_returns_null()
    {
        var fakeBytes = Encoding.UTF8.GetBytes("#!/bin/bash\necho 'fake document'");
        var mime = OwnerDocumentContentInspector.DetectContentType(fakeBytes);

        Assert.Null(mime);
    }

    [Fact]
    public void DetectContentType_empty_bytes_returns_null()
    {
        var mime = OwnerDocumentContentInspector.DetectContentType([]);

        Assert.Null(mime);
    }

    [Fact]
    public void DetectContentType_truncated_bytes_returns_null()
    {
        byte[] truncated = [0x89, 0x50]; // PNG requires 8 bytes
        var mime = OwnerDocumentContentInspector.DetectContentType(truncated);

        Assert.Null(mime);
    }
}
