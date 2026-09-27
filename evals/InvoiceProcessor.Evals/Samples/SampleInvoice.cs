using InvoiceProcessor.Core.Extraction;

namespace InvoiceProcessor.Evals.Samples;

public enum SampleLayout { Classic, Modern, Minimal }

public enum SampleFormat { Pdf, Png }

public sealed record Party(string Name, IReadOnlyList<string> Address, string? TaxId = null);

public sealed record SampleLine(string Description, decimal Qty, decimal UnitPrice, decimal LineTotal, decimal VatRate, string? Sku = null);

public sealed record VatRow(decimal Rate, decimal Base, decimal Amount);

/// <summary>
/// A synthetic invoice: everything needed to render it and the ground truth to score extraction
/// against. Amounts are the printed ones, which for the error samples are deliberately inconsistent.
/// </summary>
public sealed record SampleInvoice
{
    public required string Id { get; init; }

    /// <summary>What the sample exercises, for the report.</summary>
    public required string Purpose { get; init; }

    public required SampleLayout Layout { get; init; }
    public SampleFormat Format { get; init; } = SampleFormat.Pdf;

    public required Labels Labels { get; init; }
    public required string Culture { get; init; }
    public required string DateFormat { get; init; }
    public required Money Money { get; init; }

    public required Party Supplier { get; init; }
    public required Party Customer { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly Date { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public DateOnly? OrderDate { get; init; }

    /// <summary>Other reference numbers printed near the invoice number, as distractors.</summary>
    public IReadOnlyList<(string Label, string Value)> References { get; init; } = [];

    public required string Currency { get; init; }
    public required IReadOnlyList<SampleLine> Lines { get; init; }

    /// <summary>Line totals are printed including VAT (and so sum to the total, not the net).</summary>
    public bool LinesIncludeVat { get; init; }

    public bool ShowVatRateColumn { get; init; }
    public required IReadOnlyList<VatRow> VatRows { get; init; }
    public required decimal Net { get; init; }
    public required decimal Vat { get; init; }
    public required decimal Total { get; init; }
    public string? Note { get; init; }

    /// <summary>Validation rules that must fire for this sample (the deliberate errors).</summary>
    public IReadOnlyList<string> ExpectedIssues { get; init; } = [];

    public string FileName => Id + (Format == SampleFormat.Png ? ".png" : ".pdf");

    public ExtractedInvoice ToExpected() => new(
        Supplier.Name,
        InvoiceNumber,
        Date,
        Currency,
        [.. Lines.Select(l => new ExtractedInvoiceLine(l.Description, l.Qty, l.UnitPrice, l.LineTotal))],
        Net,
        Vat,
        Total);
}

/// <summary>How amounts are printed: symbol, its position, and the culture's separators.</summary>
public sealed record Money(string Symbol, bool SymbolAfter);
