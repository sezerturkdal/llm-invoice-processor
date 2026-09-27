using InvoiceProcessor.Core.Invoices;

namespace InvoiceProcessor.Api.Invoices;

public sealed record InvoiceResponse(
    Guid Id,
    string FileName,
    InvoiceStatus Status,
    string? Supplier,
    string? InvoiceNumber,
    DateOnly? Date,
    string? Currency,
    decimal? Net,
    decimal? Vat,
    decimal? Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ModelUsed,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<ValidationIssueResponse> ValidationIssues,
    string? ExtractionError,
    ExtractionUsageResponse? LastExtraction)
{
    /// <param name="lastExtraction">The most recent extraction attempt; its error is shown only while the invoice is Failed.</param>
    public static InvoiceResponse From(Invoice invoice, ExtractionLog? lastExtraction = null) => new(
        invoice.Id,
        invoice.FileName,
        invoice.Status,
        invoice.Supplier,
        invoice.InvoiceNumber,
        invoice.Date,
        invoice.Currency,
        invoice.Net,
        invoice.Vat,
        invoice.Total,
        invoice.CreatedAt,
        invoice.ReviewedAt,
        invoice.ModelUsed,
        [.. invoice.Lines.Select(l => new InvoiceLineResponse(l.Id, l.Description, l.Qty, l.UnitPrice, l.LineTotal))],
        [.. invoice.ValidationIssues.Select(v => new ValidationIssueResponse(v.Field, v.Rule, v.Message))],
        invoice.Status == InvoiceStatus.Failed ? lastExtraction?.Error : null,
        lastExtraction is null ? null : ExtractionUsageResponse.From(lastExtraction));
}

public sealed record InvoiceLineResponse(Guid Id, string Description, decimal Qty, decimal UnitPrice, decimal LineTotal);

public sealed record ValidationIssueResponse(string Field, string Rule, string Message);

/// <summary>What one extraction attempt used: tokens, estimated cost (USD) and latency.</summary>
public sealed record ExtractionUsageResponse(
    string Provider,
    string Model,
    int? InputTokens,
    int? OutputTokens,
    long LatencyMs,
    decimal? CostEstimate,
    bool Succeeded,
    DateTimeOffset CreatedAt)
{
    public static ExtractionUsageResponse From(ExtractionLog log) => new(
        log.Provider, log.Model, log.InputTokens, log.OutputTokens, log.LatencyMs, log.CostEstimate, log.Error is null, log.CreatedAt);
}
