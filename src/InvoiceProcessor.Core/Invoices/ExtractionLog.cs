namespace InvoiceProcessor.Core.Invoices;

public class ExtractionLog
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    public required string Provider { get; set; }
    public required string Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public long LatencyMs { get; set; }
    public decimal? CostEstimate { get; set; }
    public string? RawResponse { get; set; }

    /// <summary>Why the extraction failed; null when it succeeded.</summary>
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
