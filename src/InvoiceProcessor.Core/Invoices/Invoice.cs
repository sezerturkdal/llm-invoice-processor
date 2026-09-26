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
}
