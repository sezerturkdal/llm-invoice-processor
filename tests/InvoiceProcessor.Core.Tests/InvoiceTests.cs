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
}
