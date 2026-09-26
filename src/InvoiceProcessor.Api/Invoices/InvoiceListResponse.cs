using InvoiceProcessor.Core.Invoices;

namespace InvoiceProcessor.Api.Invoices;

public sealed record InvoiceListResponse(IReadOnlyList<InvoiceSummary> Items, int TotalCount, int Page, int PageSize);

/// <summary>One dashboard row: enough to list and filter, without lines.</summary>
public sealed record InvoiceSummary(
    Guid Id,
    string FileName,
    InvoiceStatus Status,
    string? Supplier,
    string? InvoiceNumber,
    DateOnly? Date,
    string? Currency,
    decimal? Total,
    int IssueCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);
