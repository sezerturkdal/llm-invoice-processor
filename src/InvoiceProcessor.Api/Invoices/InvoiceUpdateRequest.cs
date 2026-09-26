using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Invoices;

namespace InvoiceProcessor.Api.Invoices;

/// <summary>The reviewer's corrected version of the invoice data. Replaces all fields and lines.</summary>
public sealed record InvoiceUpdateRequest(
    string? Supplier,
    string? InvoiceNumber,
    DateOnly? Date,
    string? Currency,
    decimal? Net,
    decimal? Vat,
    decimal? Total,
    IReadOnlyList<InvoiceLineRequest>? Lines)
{
    /// <summary>Input errors keyed by field, in the same field naming the validation issues use.</summary>
    public Dictionary<string, string[]> Validate()
    {
        Dictionary<string, string[]> errors = [];

        CheckLength(errors, "supplier", Supplier, InvoiceFieldLimits.Supplier);
        CheckLength(errors, "invoiceNumber", InvoiceNumber, InvoiceFieldLimits.InvoiceNumber);
        CheckLength(errors, "currency", Currency, InvoiceFieldLimits.Currency);

        var lines = Lines ?? [];
        for (var i = 0; i < lines.Count; i++)
        {
            var description = lines[i].Description;
            if (string.IsNullOrWhiteSpace(description))
            {
                errors[$"lines[{i}].description"] = ["Description is required."];
            }
            else
            {
                CheckLength(errors, $"lines[{i}].description", description, InvoiceFieldLimits.LineDescription);
            }
        }

        return errors;
    }

    public ExtractedInvoice ToDetails() => new(
        Clean(Supplier),
        Clean(InvoiceNumber),
        Date,
        Clean(Currency)?.ToUpperInvariant(),
        [.. (Lines ?? []).Select(l => new ExtractedInvoiceLine(l.Description!.Trim(), l.Qty, l.UnitPrice, l.LineTotal))],
        Net,
        Vat,
        Total);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void CheckLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            errors[field] = [$"Must be at most {maxLength} characters."];
        }
    }
}

public sealed record InvoiceLineRequest(string? Description, decimal Qty, decimal UnitPrice, decimal LineTotal);
