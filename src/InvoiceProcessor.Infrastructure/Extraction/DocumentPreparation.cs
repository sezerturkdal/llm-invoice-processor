using System.Runtime.Versioning;
using System.Text;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using PDFtoImage;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>
/// What a model without native PDF input gets instead of the file: the PDF's text layer when it has one,
/// otherwise images of its pages. Images are passed through unchanged.
/// </summary>
public sealed record PreparedDocument(string? Text, IReadOnlyList<byte[]> Images)
{
    public bool IsText => Text is not null;
}

public static class DocumentPreparation
{
    /// <summary>
    /// A digital PDF's text layer holds the exact characters, so it is preferred over rendering: no misread
    /// digits, and far fewer tokens than an image. Scans have no (or almost no) text layer and are rendered.
    /// </summary>
    public static PreparedDocument Prepare(InvoiceDocument document, DocumentPreparationOptions options)
    {
        if (document.FileType != InvoiceFileType.Pdf)
        {
            return new PreparedDocument(null, [document.Content]);
        }

        var text = ReadTextLayer(document.Content, options.MaxPages);
        if (text.Count(c => !char.IsWhiteSpace(c)) >= options.MinTextLength)
        {
            return new PreparedDocument(text, []);
        }

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new PreparedDocument(null, RenderPages(document.Content, options));
        }

        throw new PlatformNotSupportedException("Rendering PDF pages needs Windows, Linux or macOS.");
    }

    private static string ReadTextLayer(byte[] pdf, int maxPages)
    {
        using var document = PdfDocument.Open(pdf);
        var pageCount = Math.Min(document.NumberOfPages, maxPages);
        var text = new StringBuilder();

        for (var number = 1; number <= pageCount; number++)
        {
            if (pageCount > 1)
            {
                text.AppendLine($"--- Page {number} of {document.NumberOfPages} ---");
            }

            // Content order keeps a table row together on one line, which plain letter order does not.
            text.AppendLine(ContentOrderTextExtractor.GetText(document.GetPage(number)).Trim());
        }

        return text.ToString().Trim();
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static List<byte[]> RenderPages(byte[] pdf, DocumentPreparationOptions options)
    {
        var renderOptions = new RenderOptions(Dpi: options.RenderDpi, WithAnnotations: true, BackgroundColor: SkiaSharp.SKColors.White);
        var pageCount = Math.Min(Conversion.GetPageCount(pdf), options.MaxPages);
        var pages = new List<byte[]>(pageCount);

        for (var index = 0; index < pageCount; index++)
        {
            using var png = new MemoryStream();
            Conversion.SavePng(png, pdf, index, options: renderOptions);
            pages.Add(png.ToArray());
        }

        return pages;
    }
}
