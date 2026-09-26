using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Core.Validation;

namespace InvoiceProcessor.Core.Tests;

public class InvoiceReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pending_invoice_can_be_approved()
    {
        var invoice = PendingInvoice();

        invoice.Approve(Now);

        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(Now, invoice.ReviewedAt);
    }

    [Fact]
    public void Warnings_do_not_block_approval()
    {
        var invoice = PendingInvoice();
        invoice.ReplaceValidationIssues([new ValidationIssue { Field = "total", Rule = ValidationRules.NetPlusVatEqualsTotal, Message = "m" }]);

        invoice.Approve(Now);

        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
    }

    [Fact]
    public void Missing_required_fields_block_approval()
    {
        var invoice = PendingInvoice();
        invoice.ReplaceValidationIssues([new ValidationIssue { Field = "supplier", Rule = ValidationRules.RequiredFields, Message = "m" }]);

        Assert.True(invoice.HasBlockingIssues);
        Assert.Throws<InvalidOperationException>(() => invoice.Approve(Now));
        Assert.Equal(InvoiceStatus.PendingReview, invoice.Status);
    }

    [Theory]
    [InlineData(InvoiceStatus.Processing)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    [InlineData(InvoiceStatus.Failed)]
    public void Only_pending_invoices_can_be_corrected_or_approved(InvoiceStatus status)
    {
        var invoice = PendingInvoice();
        invoice.Status = status;

        Assert.False(invoice.CanBeEdited);
        Assert.Throws<InvalidOperationException>(() => invoice.ApplyCorrections(Details("X")));
        Assert.Throws<InvalidOperationException>(() => invoice.Approve(Now));
    }

    [Theory]
    [InlineData(InvoiceStatus.PendingReview, true)]
    [InlineData(InvoiceStatus.Failed, true)]
    [InlineData(InvoiceStatus.Processing, false)]
    [InlineData(InvoiceStatus.Approved, false)]
    [InlineData(InvoiceStatus.Rejected, false)]
    public void Rejecting_is_allowed_for_pending_and_failed(InvoiceStatus status, bool allowed)
    {
        var invoice = PendingInvoice();
        invoice.Status = status;

        if (allowed)
        {
            invoice.Reject(Now);
            Assert.Equal(InvoiceStatus.Rejected, invoice.Status);
            Assert.Equal(Now, invoice.ReviewedAt);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => invoice.Reject(Now));
        }
    }

    [Fact]
    public void Corrections_replace_fields_and_lines_but_keep_the_model()
    {
        var invoice = PendingInvoice();

        invoice.ApplyCorrections(Details("Corrected Ltd"));

        Assert.Equal("Corrected Ltd", invoice.Supplier);
        Assert.Equal("Fixed", Assert.Single(invoice.Lines).Description);
        Assert.Equal("claude-opus-5", invoice.ModelUsed);
        Assert.Equal(InvoiceStatus.PendingReview, invoice.Status);
    }

    [Fact]
    public void Overlong_model_output_is_cut_to_the_column_limits()
    {
        var invoice = new Invoice { FileName = "a.pdf", FilePath = "a.pdf" };
        var longText = new string('x', 5000);

        invoice.ApplyExtraction(
            new ExtractedInvoice(longText, longText, null, longText, [new ExtractedInvoiceLine(longText, 1, 1, 1)], null, null, null),
            "m");

        Assert.Equal(InvoiceFieldLimits.Supplier, invoice.Supplier!.Length);
        Assert.Equal(InvoiceFieldLimits.InvoiceNumber, invoice.InvoiceNumber!.Length);
        Assert.Equal(InvoiceFieldLimits.Currency, invoice.Currency!.Length);
        Assert.Equal(InvoiceFieldLimits.LineDescription, invoice.Lines[0].Description.Length);
    }

    private static Invoice PendingInvoice()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), FileName = "a.pdf", FilePath = "a.pdf" };
        invoice.ApplyExtraction(Details("Acme"), "claude-opus-5");
        return invoice;
    }

    private static ExtractedInvoice Details(string supplier) =>
        new(supplier, "INV-1", new DateOnly(2026, 9, 1), "EUR", [new ExtractedInvoiceLine("Fixed", 1, 100, 100)], 100, 20, 120);
}
