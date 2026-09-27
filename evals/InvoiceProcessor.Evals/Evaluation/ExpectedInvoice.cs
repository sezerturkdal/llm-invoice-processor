using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceProcessor.Core.Extraction;

namespace InvoiceProcessor.Evals.Evaluation;

/// <summary>
/// Ground truth for one sample, stored as evals/expected/&lt;sample&gt;.json. The invoice part uses
/// the extraction schema, so real invoices can have expected files written by hand in the same shape.
/// </summary>
public sealed record ExpectedInvoice(
    string? Supplier,
    string? InvoiceNumber,
    DateOnly? Date,
    string? Currency,
    IReadOnlyList<ExtractedInvoiceLine> Lines,
    decimal? Net,
    decimal? Vat,
    decimal? Total,
    IReadOnlyList<string>? ExpectedIssues = null,
    string? Purpose = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [JsonIgnore]
    public ExtractedInvoice Invoice => new(Supplier, InvoiceNumber, Date, Currency, Lines, Net, Vat, Total);

    public static ExpectedInvoice From(ExtractedInvoice invoice, IReadOnlyList<string> expectedIssues, string? purpose) => new(
        invoice.Supplier, invoice.InvoiceNumber, invoice.Date, invoice.Currency, invoice.Lines,
        invoice.Net, invoice.Vat, invoice.Total, expectedIssues, purpose);

    public static async Task<ExpectedInvoice> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ExpectedInvoice>(stream, JsonOptions)
            ?? throw new InvalidDataException($"{path} is empty.");
    }
}
