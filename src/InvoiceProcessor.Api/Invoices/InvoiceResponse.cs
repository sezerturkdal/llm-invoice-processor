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
    IReadOnlyList<ValidationIssueResponse> ValidationIssues)
{
    public static InvoiceResponse From(Invoice invoice) => new(
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
        [.. invoice.ValidationIssues.Select(v => new ValidationIssueResponse(v.Field, v.Rule, v.Message))]);
}

public sealed record InvoiceLineResponse(Guid Id, string Description, decimal Qty, decimal UnitPrice, decimal LineTotal);

public sealed record ValidationIssueResponse(string Field, string Rule, string Message);
