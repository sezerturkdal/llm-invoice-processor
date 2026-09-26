using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Invoices;

namespace InvoiceProcessor.Core.Tests;

public class InvoiceTests
{
    [Fact]
    public void New_invoice_starts_in_processing()
    {
        var invoice = new Invoice { FileName = "a.pdf", FilePath = "uploads/a.pdf" };

        Assert.Equal(InvoiceStatus.Processing, invoice.Status);
    }

    [Fact]
    public void Applying_extraction_copies_fields_and_moves_to_review()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), FileName = "a.pdf", FilePath = "a.pdf" };
        var extracted = new ExtractedInvoice(
            "Acme", "INV-1", new DateOnly(2026, 9, 1), "EUR",
            [new ExtractedInvoiceLine("Widget", 2, 50, 100)],
            100, 19, 119);

        invoice.ApplyExtraction(extracted, "claude-opus-5");

        Assert.Equal(InvoiceStatus.PendingReview, invoice.Status);
        Assert.Equal("Acme", invoice.Supplier);
        Assert.Equal(119m, invoice.Total);
        Assert.Equal("claude-opus-5", invoice.ModelUsed);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(invoice.Id, line.InvoiceId);
        Assert.Equal(100m, line.LineTotal);
    }

    [Fact]
    public void Re_applying_extraction_replaces_previous_lines()
    {
        var invoice = new Invoice { FileName = "a.pdf", FilePath = "a.pdf" };
        invoice.ApplyExtraction(new ExtractedInvoice(null, null, null, null, [new("Old", 1, 1, 1), new("Old", 1, 1, 1)], null, null, null), "m");

        invoice.ApplyExtraction(new ExtractedInvoice(null, null, null, null, [new("New", 1, 5, 5)], null, null, null), "m");

        Assert.Equal("New", Assert.Single(invoice.Lines).Description);
    }
}
