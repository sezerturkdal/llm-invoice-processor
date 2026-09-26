using InvoiceProcessor.Core.Files;

namespace InvoiceProcessor.Core.Extraction;

/// <summary>Reads structured invoice data from a document. One implementation per LLM provider.</summary>
public interface IInvoiceExtractor
{
    string Provider { get; }

    /// <summary>The configured model; the one that actually answered is in <see cref="ExtractionUsage.Model"/>.</summary>
    string Model { get; }

    /// <exception cref="InvoiceExtractionException">The model did not return usable invoice data.</exception>
    Task<ExtractionResult> ExtractAsync(InvoiceDocument document, CancellationToken cancellationToken = default);
}

public sealed record InvoiceDocument(byte[] Content, InvoiceFileType FileType);

public sealed record ExtractionResult(ExtractedInvoice Invoice, ExtractionUsage Usage);

/// <summary>What one extraction call cost, for the extraction log.</summary>
public sealed record ExtractionUsage(
    string Provider,
    string Model,
    int? InputTokens,
    int? OutputTokens,
    long LatencyMs,
    decimal? CostEstimate,
    string? RawResponse);

/// <summary>
/// The provider answered but the answer is unusable (refusal, truncated or invalid JSON).
/// Carries the usage so failed calls are still logged and costed.
/// </summary>
public sealed class InvoiceExtractionException(string message, ExtractionUsage? usage = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ExtractionUsage? Usage { get; } = usage;
}
