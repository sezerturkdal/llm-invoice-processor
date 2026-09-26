using InvoiceProcessor.Core.Extraction;

namespace InvoiceProcessor.Core.Invoices;

public class Invoice
{
    public Guid Id { get; set; }

    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Processing;

    // Extracted fields are null until extraction has run.
    public string? Supplier { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? Date { get; set; }
    public string? Currency { get; set; }
    public decimal? Net { get; set; }
    public decimal? Vat { get; set; }
    public decimal? Total { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ModelUsed { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
    public List<ValidationIssue> ValidationIssues { get; set; } = [];
    public List<ExtractionLog> ExtractionLogs { get; set; } = [];

    /// <summary>Replaces the invoice data with an extraction result and sends it to review.</summary>
    public void ApplyExtraction(ExtractedInvoice extracted, string model)
    {
        Supplier = extracted.Supplier;
        InvoiceNumber = extracted.InvoiceNumber;
        Date = extracted.Date;
        Currency = extracted.Currency;
        Net = extracted.Net;
        Vat = extracted.Vat;
        Total = extracted.Total;
        ModelUsed = model;

        Lines.Clear();
        Lines.AddRange(extracted.Lines.Select(l => new InvoiceLine
        {
            InvoiceId = Id,
            Description = l.Description,
            Qty = l.Qty,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
        }));

        Status = InvoiceStatus.PendingReview;
    }
}
