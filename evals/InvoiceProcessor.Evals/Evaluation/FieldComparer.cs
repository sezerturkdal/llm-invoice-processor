using System.Globalization;
using System.Text.RegularExpressions;
using InvoiceProcessor.Core.Extraction;

namespace InvoiceProcessor.Evals.Evaluation;

/// <summary>
/// Decides whether an extracted value counts as correct. Deliberately strict on what matters to
/// accounting (amounts, dates, numbers) and lenient on formatting noise (case, spacing).
/// </summary>
public static partial class FieldComparer
{
    public const decimal AmountTolerance = 0.005m;

    public static readonly string[] HeaderFields = ["supplier", "invoiceNumber", "date", "currency", "net", "vat", "total"];
    public static readonly string[] LineFields = ["lineCount", "line.description", "line.qty", "line.unitPrice", "line.lineTotal"];

    public static IReadOnlyList<FieldResult> Compare(ExtractedInvoice expected, ExtractedInvoice actual)
    {
        List<FieldResult> results =
        [
            new("supplier", expected.Supplier, actual.Supplier, TextEquals(expected.Supplier, actual.Supplier)),
            new("invoiceNumber", expected.InvoiceNumber, actual.InvoiceNumber, CodeEquals(expected.InvoiceNumber, actual.InvoiceNumber)),
            new("date", Iso(expected.Date), Iso(actual.Date), expected.Date == actual.Date),
            new("currency", expected.Currency, actual.Currency, string.Equals(expected.Currency?.Trim(), actual.Currency?.Trim(), StringComparison.OrdinalIgnoreCase)),
            Amount("net", expected.Net, actual.Net),
            Amount("vat", expected.Vat, actual.Vat),
            Amount("total", expected.Total, actual.Total),
            new("lineCount", expected.Lines.Count.ToString(CultureInfo.InvariantCulture), actual.Lines.Count.ToString(CultureInfo.InvariantCulture), expected.Lines.Count == actual.Lines.Count),
        ];

        // Lines are matched by position; a missing or extra line makes every field of that position wrong.
        for (var i = 0; i < expected.Lines.Count; i++)
        {
            var e = expected.Lines[i];
            var a = i < actual.Lines.Count ? actual.Lines[i] : null;
            results.Add(new("line.description", e.Description, a?.Description, a is not null && DescriptionMatches(e.Description, a.Description), i));
            results.Add(Amount("line.qty", e.Qty, a?.Qty, i));
            results.Add(Amount("line.unitPrice", e.UnitPrice, a?.UnitPrice, i));
            results.Add(Amount("line.lineTotal", e.LineTotal, a?.LineTotal, i));
        }

        return results;
    }

    private static FieldResult Amount(string field, decimal? expected, decimal? actual, int? line = null) =>
        new(field, expected?.ToString("0.00##", CultureInfo.InvariantCulture), actual?.ToString("0.00##", CultureInfo.InvariantCulture),
            expected is null ? actual is null : actual is not null && Math.Abs(expected.Value - actual.Value) <= AmountTolerance,
            line);

    private static bool TextEquals(string? expected, string? actual) => Normalize(expected) == Normalize(actual);

    // Invoice numbers: spacing differences don't matter, every other character does.
    private static bool CodeEquals(string? expected, string? actual) =>
        string.Equals(expected is null ? null : Whitespace().Replace(expected, ""), actual is null ? null : Whitespace().Replace(actual, ""), StringComparison.OrdinalIgnoreCase);

    // The model may add the SKU or a code printed under the description; that still identifies the line.
    private static bool DescriptionMatches(string expected, string actual)
    {
        var e = Normalize(expected)!;
        var a = Normalize(actual)!;
        return e == a || a.Contains(e, StringComparison.Ordinal);
    }

    private static string? Normalize(string? value) =>
        value is null ? null : Whitespace().Replace(value, " ").Trim().TrimEnd('.', ',').ToLowerInvariant();

    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

public sealed record FieldResult(string Field, string? Expected, string? Actual, bool Correct, int? Line = null);
