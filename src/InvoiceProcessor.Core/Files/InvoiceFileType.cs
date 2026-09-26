namespace InvoiceProcessor.Core.Files;

/// <summary>A file format accepted for invoice upload.</summary>
public sealed record InvoiceFileType(string Name, string Extension, string ContentType)
{
    public static readonly InvoiceFileType Pdf = new("PDF", ".pdf", "application/pdf");
    public static readonly InvoiceFileType Png = new("PNG", ".png", "image/png");
    public static readonly InvoiceFileType Jpeg = new("JPEG", ".jpg", "image/jpeg");

    public static IReadOnlyList<InvoiceFileType> All { get; } = [Pdf, Png, Jpeg];

    /// <summary>Number of leading bytes <see cref="Detect"/> needs (the PNG signature is the longest).</summary>
    public const int HeaderLength = 8;

    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    /// <summary>
    /// Detects the format from the file's leading bytes. The client-supplied file name and
    /// content type are not trusted.
    /// </summary>
    public static InvoiceFileType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature)) return Pdf;
        if (header.StartsWith(PngSignature)) return Png;
        if (header.StartsWith(JpegSignature)) return Jpeg;
        return null;
    }

    /// <summary>Finds the type of a stored file by its extension.</summary>
    public static InvoiceFileType? FromPath(string path)
    {
        var extension = Path.GetExtension(path);
        return All.FirstOrDefault(t => string.Equals(t.Extension, extension, StringComparison.OrdinalIgnoreCase));
    }
}
