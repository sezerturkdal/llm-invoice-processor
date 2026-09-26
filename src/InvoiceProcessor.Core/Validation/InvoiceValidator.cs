using InvoiceProcessor.Core.Invoices;
using static System.FormattableString;

namespace InvoiceProcessor.Core.Validation;

/// <summary>An earlier invoice with the same supplier and invoice number.</summary>
public sealed record DuplicateInvoice(Guid Id, InvoiceStatus Status, DateTimeOffset CreatedAt);

/// <summary>What the rules need besides the invoice itself.</summary>
public sealed record ValidationContext(DateOnly Today, IReadOnlyList<DuplicateInvoice> Duplicates);

/// <summary>
/// Deterministic checks on extracted (or user-corrected) invoice data. The model is told to report
/// figures as printed, so arithmetic that does not add up is caught here and shown to the reviewer.
/// Each failed rule produces one issue per field it flags. Messages use invariant formatting.
/// </summary>
public static class InvoiceValidator
{
    /// <summary>Allowed difference for sums, to absorb rounding on the printed amounts.</summary>
    public const decimal AmountTolerance = 0.02m;

    public static IReadOnlyList<ValidationIssue> Validate(Invoice invoice, ValidationContext context)
    {
        List<ValidationIssue> issues = [];

        CheckRequiredFields(invoice, issues);
        CheckLinesSumToNetOrTotal(invoice, issues);
        CheckNetPlusVatEqualsTotal(invoice, issues);
        CheckDateNotInFuture(invoice, context.Today, issues);
        CheckDuplicate(invoice, context.Duplicates, issues);

        foreach (var issue in issues)
        {
            issue.InvoiceId = invoice.Id;
        }

        return issues;
    }

    private static void CheckRequiredFields(Invoice invoice, List<ValidationIssue> issues)
    {
        const string rule = ValidationRules.RequiredFields;

        if (string.IsNullOrWhiteSpace(invoice.Supplier)) issues.Add(Issue("supplier", rule, "Supplier is missing."));
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)) issues.Add(Issue("invoiceNumber", rule, "Invoice number is missing."));
        if (invoice.Date is null) issues.Add(Issue("date", rule, "Invoice date is missing."));
        if (string.IsNullOrWhiteSpace(invoice.Currency)) issues.Add(Issue("currency", rule, "Currency is missing."));
        if (invoice.Net is null) issues.Add(Issue("net", rule, "Net amount is missing."));
        if (invoice.Vat is null) issues.Add(Issue("vat", rule, "VAT amount is missing."));
        if (invoice.Total is null) issues.Add(Issue("total", rule, "Total is missing."));
        if (invoice.Lines.Count == 0) issues.Add(Issue("lines", rule, "No line items were found."));
    }

    // Invoices print line totals either before tax (summing to net) or including tax (summing to
    // the total); both are consistent, so a match with either passes.
    private static void CheckLinesSumToNetOrTotal(Invoice invoice, List<ValidationIssue> issues)
    {
        if (invoice.Lines.Count == 0 || (invoice.Net is null && invoice.Total is null))
        {
            return;
        }

        var sum = invoice.Lines.Sum(l => l.LineTotal);
        if (Matches(sum, invoice.Net) || Matches(sum, invoice.Total))
        {
            return;
        }

        var message = Invariant($"Line totals add up to {sum:0.00}, which matches neither net ({Format(invoice.Net)}) nor total ({Format(invoice.Total)}).");
        issues.Add(Issue("lines", ValidationRules.LinesSumToNetOrTotal, message));
        if (invoice.Net is not null)
        {
            issues.Add(Issue("net", ValidationRules.LinesSumToNetOrTotal, message));
        }
    }

    private static void CheckNetPlusVatEqualsTotal(Invoice invoice, List<ValidationIssue> issues)
    {
        if (invoice.Net is not { } net || invoice.Vat is not { } vat || invoice.Total is not { } total)
        {
            return;
        }

        if (Matches(net + vat, total))
        {
            return;
        }

        // Any of the three may be the misread one, so all are flagged for the reviewer.
        var message = Invariant($"Net {net:0.00} + VAT {vat:0.00} = {net + vat:0.00}, but the total is {total:0.00}.");
        issues.Add(Issue("net", ValidationRules.NetPlusVatEqualsTotal, message));
        issues.Add(Issue("vat", ValidationRules.NetPlusVatEqualsTotal, message));
        issues.Add(Issue("total", ValidationRules.NetPlusVatEqualsTotal, message));
    }

    // One day of slack: an invoice issued today in a time zone ahead of UTC is dated "tomorrow" in UTC.
    private static void CheckDateNotInFuture(Invoice invoice, DateOnly today, List<ValidationIssue> issues)
    {
        if (invoice.Date is { } date && date > today.AddDays(1))
        {
            issues.Add(Issue("date", ValidationRules.DateNotInFuture, Invariant($"Invoice date {date:yyyy-MM-dd} is in the future.")));
        }
    }

    private static void CheckDuplicate(Invoice invoice, IReadOnlyList<DuplicateInvoice> duplicates, List<ValidationIssue> issues)
    {
        var others = duplicates.Where(d => d.Id != invoice.Id).OrderBy(d => d.CreatedAt).ToList();
        if (others.Count == 0)
        {
            return;
        }

        var first = others[0];
        var message = others.Count == 1
            ? Invariant($"Invoice {invoice.InvoiceNumber} from {invoice.Supplier} was already uploaded on {first.CreatedAt:yyyy-MM-dd} ({first.Status}).")
            : Invariant($"Invoice {invoice.InvoiceNumber} from {invoice.Supplier} was already uploaded {others.Count} times, first on {first.CreatedAt:yyyy-MM-dd}.");
        issues.Add(Issue("invoiceNumber", ValidationRules.DuplicateInvoiceNumber, message));
    }

    private static bool Matches(decimal actual, decimal? expected) =>
        expected is { } value && Math.Abs(actual - value) <= AmountTolerance;

    private static string Format(decimal? amount) => amount is { } value ? Invariant($"{value:0.00}") : "missing";

    private static ValidationIssue Issue(string field, string rule, string message) =>
        new() { Field = field, Rule = rule, Message = message };
}

public static class ValidationRules
{
    public const string RequiredFields = nameof(RequiredFields);
    public const string LinesSumToNetOrTotal = nameof(LinesSumToNetOrTotal);
    public const string NetPlusVatEqualsTotal = nameof(NetPlusVatEqualsTotal);
    public const string DateNotInFuture = nameof(DateNotInFuture);
    public const string DuplicateInvoiceNumber = nameof(DuplicateInvoiceNumber);
}
