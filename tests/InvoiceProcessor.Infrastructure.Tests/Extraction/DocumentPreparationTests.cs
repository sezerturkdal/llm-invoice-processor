using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Extraction;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace InvoiceProcessor.Infrastructure.Tests.Extraction;

public class DocumentPreparationTests
{
    private static readonly DocumentPreparationOptions Options = new();

    [Fact]
    public void Images_are_passed_through()
    {
        var png = Png();

        var prepared = DocumentPreparation.Prepare(new InvoiceDocument(png, InvoiceFileType.Png), Options);

        Assert.False(prepared.IsText);
        Assert.Same(png, Assert.Single(prepared.Images));
    }

    [Fact]
    public void Digital_pdf_is_sent_as_its_text_layer()
    {
        var pdf = TextPdf(["Acme GmbH   Invoice INV-2026-017", "Widget   2   50.00   100.00", "Total EUR 119.00"]);

        var prepared = DocumentPreparation.Prepare(new InvoiceDocument(pdf, InvoiceFileType.Pdf), Options);

        Assert.True(prepared.IsText);
        Assert.Empty(prepared.Images);
        Assert.Contains("INV-2026-017", prepared.Text);
        Assert.Contains("119.00", prepared.Text);
        Assert.DoesNotContain("--- Page", prepared.Text);
    }

    [Fact]
    public void Multi_page_text_is_marked_per_page_and_capped()
    {
        var pdf = TextPdf(["First page of the invoice with enough text"], ["Second page of the invoice with enough text"], ["Third page, terms and conditions only"]);

        var prepared = DocumentPreparation.Prepare(new InvoiceDocument(pdf, InvoiceFileType.Pdf), new DocumentPreparationOptions { MaxPages = 2 });

        Assert.Contains("--- Page 1 of 3 ---", prepared.Text);
        Assert.Contains("--- Page 2 of 3 ---", prepared.Text);
        Assert.DoesNotContain("Third page", prepared.Text);
    }

    [Fact]
    public void Scanned_pdf_without_text_is_rendered_to_page_images()
    {
        var pdf = ScannedPdf(pages: 2);

        var prepared = DocumentPreparation.Prepare(new InvoiceDocument(pdf, InvoiceFileType.Pdf), Options);

        Assert.False(prepared.IsText);
        Assert.Equal(2, prepared.Images.Count);
        Assert.All(prepared.Images, image => Assert.Equal(InvoiceFileType.Png, InvoiceFileType.Detect(image)));
    }

    [Fact]
    public void A_few_stray_characters_do_not_count_as_a_text_layer()
    {
        var pdf = TextPdf(["p. 1"]);

        var prepared = DocumentPreparation.Prepare(new InvoiceDocument(pdf, InvoiceFileType.Pdf), Options);

        Assert.False(prepared.IsText);
        Assert.Single(prepared.Images);
    }

    private static byte[] TextPdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            for (var i = 0; i < lines.Length; i++)
            {
                page.AddText(lines[i], 11, new PdfPoint(50, 780 - i * 20), font);
            }
        }

        return builder.Build();
    }

    private static byte[] ScannedPdf(int pages)
    {
        var builder = new PdfDocumentBuilder();
        var png = Png();

        for (var i = 0; i < pages; i++)
        {
            builder.AddPage(PageSize.A4).AddPng(png, new PdfRectangle(0, 0, 595, 842));
        }

        return builder.Build();
    }

    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(60, 80);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(10, 10, 40, 6, paint);
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
