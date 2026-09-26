namespace InvoiceProcessor.Core.Extraction;

/// <summary>
/// Invoice data as the model read it from the document. Values are as printed, not corrected;
/// fields the model could not find are null.
/// </summary>
public sealed record ExtractedInvoice(
    string? Supplier,
    string? InvoiceNumber,
    DateOnly? Date,
    string? Currency,
    IReadOnlyList<ExtractedInvoiceLine> Lines,
    decimal? Net,
    decimal? Vat,
    decimal? Total);

public sealed record ExtractedInvoiceLine(string Description, decimal Qty, decimal UnitPrice, decimal LineTotal);
