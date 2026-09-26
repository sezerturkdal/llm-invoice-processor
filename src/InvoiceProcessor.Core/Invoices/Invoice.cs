using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Validation;

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

    /// <summary>Only invoices waiting for review can be corrected or approved.</summary>
    public bool CanBeEdited => Status == InvoiceStatus.PendingReview;

    /// <summary>Failed uploads can be rejected too, e.g. a file that is not an invoice at all.</summary>
    public bool CanBeRejected => Status is InvoiceStatus.PendingReview or InvoiceStatus.Failed;

    /// <summary>
    /// Missing required data blocks approval. Other issues are warnings: the reviewer has compared
    /// the figures with the document and may approve an invoice that really is inconsistent.
    /// </summary>
    public bool HasBlockingIssues => ValidationIssues.Any(i => i.Rule == ValidationRules.RequiredFields);

    /// <summary>Replaces the invoice data with an extraction result and sends it to review.</summary>
    public void ApplyExtraction(ExtractedInvoice extracted, string model)
    {
        SetDetails(extracted);
        ModelUsed = model;
        Status = InvoiceStatus.PendingReview;
    }

    /// <summary>Replaces the invoice data with the reviewer's corrections.</summary>
    public void ApplyCorrections(ExtractedInvoice corrected)
    {
        EnsureState(CanBeEdited, "corrected");
        SetDetails(corrected);
    }

    public void Approve(DateTimeOffset reviewedAt)
    {
        EnsureState(CanBeEdited, "approved");
        if (HasBlockingIssues)
        {
            throw new InvalidOperationException($"Invoice {Id} is missing required fields and cannot be approved.");
        }

        Status = InvoiceStatus.Approved;
        ReviewedAt = reviewedAt;
    }

    public void Reject(DateTimeOffset reviewedAt)
    {
        EnsureState(CanBeRejected, "rejected");
        Status = InvoiceStatus.Rejected;
        ReviewedAt = reviewedAt;
    }

    public void ReplaceValidationIssues(IEnumerable<ValidationIssue> issues)
    {
        ValidationIssues.Clear();
        ValidationIssues.AddRange(issues);
    }

    // Review input is length-checked before it gets here; the cut only ever applies to model output,
    // which must not fail the save (the invoice would stay in Processing and be re-extracted on every restart).
    private void SetDetails(ExtractedInvoice details)
    {
        Supplier = Cut(details.Supplier, InvoiceFieldLimits.Supplier);
        InvoiceNumber = Cut(details.InvoiceNumber, InvoiceFieldLimits.InvoiceNumber);
        Date = details.Date;
        Currency = Cut(details.Currency, InvoiceFieldLimits.Currency);
        Net = details.Net;
        Vat = details.Vat;
        Total = details.Total;

        Lines.Clear();
        Lines.AddRange(details.Lines.Select(l => new InvoiceLine
        {
            InvoiceId = Id,
            Description = Cut(l.Description, InvoiceFieldLimits.LineDescription) ?? "",
            Qty = l.Qty,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
        }));
    }

    private static string? Cut(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;

    private void EnsureState(bool allowed, string action)
    {
        if (!allowed)
        {
            throw new InvalidOperationException($"Invoice {Id} is {Status} and cannot be {action}.");
        }
    }
}
