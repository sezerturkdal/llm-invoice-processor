using System.Text;
using InvoiceProcessor.Core.Files;

namespace InvoiceProcessor.Core.Tests;

public class InvoiceFileTypeTests
{
    [Fact]
    public void Detects_pdf()
    {
        Assert.Equal(InvoiceFileType.Pdf, InvoiceFileType.Detect("%PDF-1.7\n"u8));
    }

    [Fact]
    public void Detects_png()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.Equal(InvoiceFileType.Png, InvoiceFileType.Detect(header));
    }

    [Fact]
    public void Detects_jpeg()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

        Assert.Equal(InvoiceFileType.Jpeg, InvoiceFileType.Detect(header));
    }

    [Theory]
    [InlineData("")]
    [InlineData("%PD")]
    [InlineData("hello world")]
    [InlineData("<html><body>")]
    public void Rejects_unknown_or_truncated_content(string content)
    {
        Assert.Null(InvoiceFileType.Detect(Encoding.ASCII.GetBytes(content)));
    }

    [Fact]
    public void Truncated_png_signature_is_rejected()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47];

        Assert.Null(InvoiceFileType.Detect(header));
    }

    [Theory]
    [InlineData("2026/09/abc.pdf", "application/pdf")]
    [InlineData("2026/09/abc.PNG", "image/png")]
    [InlineData("2026/09/abc.jpg", "image/jpeg")]
    public void Finds_type_from_stored_path(string path, string contentType)
    {
        Assert.Equal(contentType, InvoiceFileType.FromPath(path)?.ContentType);
    }

    [Fact]
    public void Unknown_extension_has_no_type()
    {
        Assert.Null(InvoiceFileType.FromPath("2026/09/abc.exe"));
    }
}
