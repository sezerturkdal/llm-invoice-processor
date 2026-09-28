using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Core.Validation;

namespace InvoiceProcessor.Core.Tests.Validation;

public class InvoiceValidatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 27);

    [Fact]
    public void Consistent_invoice_has_no_issues()
    {
        var issues = Validate(ValidInvoice());

        Assert.Empty(issues);
    }

    [Fact]
    public void Issues_belong_to_the_invoice()
    {
        var invoice = ValidInvoice();
        invoice.Supplier = null;

        var issue = Assert.Single(Validate(invoice));

        Assert.Equal(invoice.Id, issue.InvoiceId);
    }

    // Required fields

    [Fact]
    public void Every_missing_field_is_flagged()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), FileName = "a.pdf", FilePath = "a.pdf" };

        var issues = Validate(invoice);

        Assert.All(issues, i => Assert.Equal(ValidationRules.RequiredFields, i.Rule));
        Assert.Equal(
            ["supplier", "invoiceNumber", "date", "currency", "net", "vat", "total", "lines"],
            issues.Select(i => i.Field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_text_counts_as_missing(string value)
    {
        var invoice = ValidInvoice();
        invoice.InvoiceNumber = value;

        var issue = Assert.Single(Validate(invoice));

        Assert.Equal("invoiceNumber", issue.Field);
    }

    // Lines sum to net or total

    [Fact]
    public void Lines_summing_to_net_pass()
    {
        var invoice = ValidInvoice(lines: [100m, 50m], net: 150m, vat: 30m, total: 180m);

        Assert.Empty(Validate(invoice));
    }

    [Fact]
    public void Lines_printed_including_vat_pass_when_they_sum_to_total()
    {
        // invoice1.pdf: the Amazon invoice prints its item subtotal including VAT.
        var invoice = ValidInvoice(lines: [113.99m, 0m], net: 94.99m, vat: 19.00m, total: 113.99m);

        Assert.Empty(Validate(invoice));
    }

    [Fact]
    public void Lines_matching_neither_are_flagged_on_lines_and_net()
    {
        var invoice = ValidInvoice(lines: [100m, 40m], net: 150m, vat: 30m, total: 180m);

        var issues = Validate(invoice);

        Assert.Equal(["lines", "net"], issues.Select(i => i.Field));
        Assert.All(issues, i => Assert.Equal(ValidationRules.LinesSumToNetOrTotal, i.Rule));
        Assert.Equal("Line totals add up to 140.00, which matches neither net (150.00) nor total (180.00).", issues[0].Message);
    }

    [Fact]
    public void Rounding_within_tolerance_passes()
    {
        var invoice = ValidInvoice(lines: [33.33m, 33.33m, 33.33m], net: 100m, vat: 20m, total: 120m);

        Assert.Empty(Validate(invoice));
    }

    [Fact]
    public void Difference_just_over_tolerance_is_flagged()
    {
        var invoice = ValidInvoice(lines: [99.97m], net: 100m, vat: 20m, total: 120m);

        Assert.Contains(Validate(invoice), i => i.Rule == ValidationRules.LinesSumToNetOrTotal);
    }

    [Fact]
    public void Discount_lines_count_towards_the_sum()
    {
        var invoice = ValidInvoice(lines: [100m, -10m], net: 90m, vat: 18m, total: 108m);

        Assert.Empty(Validate(invoice));
    }

    [Fact]
    public void Sum_is_not_checked_without_net_and_total()
    {
        var invoice = ValidInvoice(lines: [100m], net: 150m, vat: 30m, total: 180m);
        invoice.Net = null;
        invoice.Total = null;

        Assert.DoesNotContain(Validate(invoice), i => i.Rule == ValidationRules.LinesSumToNetOrTotal);
    }

    [Fact]
    public void Missing_net_is_not_flagged_twice()
    {
        var invoice = ValidInvoice(lines: [100m], net: 150m, vat: 30m, total: 180m);
        invoice.Net = null;

        var issues = Validate(invoice).Where(i => i.Rule == ValidationRules.LinesSumToNetOrTotal);

        Assert.Equal(["lines"], issues.Select(i => i.Field));
    }

    // Qty × unit price = line total

    [Fact]
    public void Misread_quantity_is_flagged_even_when_the_sums_add_up()
    {
        // What a small model read from "10 × 4,95 = 49,50": the totals are right, the quantity is not.
        var invoice = ValidInvoice(lines: [49.50m], net: 49.50m, vat: 9.41m, total: 58.91m);
        invoice.Lines[0].Description = "Copy paper A4";
        invoice.Lines[0].Qty = 1;
        invoice.Lines[0].UnitPrice = 4.95m;

        var issue = Assert.Single(Validate(invoice));

        Assert.Equal("lines", issue.Field);
        Assert.Equal(ValidationRules.LineAmountsMatch, issue.Rule);
        Assert.Equal("Line 1 (Copy paper A4): 1 × 4.95 = 4.95, but the line total is 49.50.", issue.Message);
    }

    [Fact]
    public void Each_inconsistent_line_is_reported()
    {
        var invoice = ValidInvoice(lines: [10m, 20m, 30m], net: 60m, vat: 12m, total: 72m);
        invoice.Lines[0].Qty = 2;
        invoice.Lines[2].UnitPrice = 3m;

        var issues = Validate(invoice).Where(i => i.Rule == ValidationRules.LineAmountsMatch).ToList();

        Assert.Equal(2, issues.Count);
        Assert.StartsWith("Line 1 ", issues[0].Message);
        Assert.StartsWith("Line 3 ", issues[1].Message);
    }

    [Theory]
    [InlineData(3, 0.33, 1.00)]     // unit price 0.333… printed rounded
    [InlineData(12, 1.67, 20.00)]   // 1.666… × 12
    [InlineData(1.5, 9.99, 14.99)]  // fractional quantity, rounded total
    [InlineData(1, -10.00, -10.00)] // discount line
    public void Rounded_unit_prices_are_not_flagged(decimal qty, decimal unitPrice, decimal lineTotal)
    {
        var invoice = ValidInvoice(lines: [lineTotal], net: lineTotal, vat: 0m, total: lineTotal);
        invoice.Lines[0].Qty = qty;
        invoice.Lines[0].UnitPrice = unitPrice;

        Assert.DoesNotContain(Validate(invoice), i => i.Rule == ValidationRules.LineAmountsMatch);
    }

    [Fact]
    public void Gross_line_totals_are_not_checked_line_by_line()
    {
        // Unit price before tax, line total after 23 % VAT, as synthetic-05 prints them.
        var invoice = ValidInvoice(lines: [366.54m], net: 298.00m, vat: 68.54m, total: 366.54m);
        invoice.Lines[0].Qty = 2;
        invoice.Lines[0].UnitPrice = 149.00m;

        Assert.Empty(Validate(invoice));
    }

    // Net + VAT = total

    [Fact]
    public void Net_plus_vat_not_matching_total_flags_all_three()
    {
        var invoice = ValidInvoice(lines: [100m], net: 100m, vat: 20m, total: 125m);

        var issues = Validate(invoice).Where(i => i.Rule == ValidationRules.NetPlusVatEqualsTotal).ToList();

        Assert.Equal(["net", "vat", "total"], issues.Select(i => i.Field));
        Assert.Equal("Net 100.00 + VAT 20.00 = 120.00, but the total is 125.00.", issues[0].Message);
    }

    [Fact]
    public void Net_plus_vat_is_skipped_when_a_value_is_missing()
    {
        var invoice = ValidInvoice(lines: [100m], net: 100m, vat: 20m, total: 125m);
        invoice.Vat = null;

        Assert.DoesNotContain(Validate(invoice), i => i.Rule == ValidationRules.NetPlusVatEqualsTotal);
    }

    [Fact]
    public void Zero_vat_invoices_pass()
    {
        var invoice = ValidInvoice(lines: [100m], net: 100m, vat: 0m, total: 100m);

        Assert.Empty(Validate(invoice));
    }

    // Date not in future

    [Fact]
    public void Date_today_or_tomorrow_passes()
    {
        var today = ValidInvoice();
        today.Date = Today;
        var tomorrow = ValidInvoice();
        tomorrow.Date = Today.AddDays(1);

        Assert.Empty(Validate(today));
        Assert.Empty(Validate(tomorrow));
    }

    [Fact]
    public void Date_further_ahead_is_flagged()
    {
        var invoice = ValidInvoice();
        invoice.Date = Today.AddDays(2);

        var issue = Assert.Single(Validate(invoice));

        Assert.Equal("date", issue.Field);
        Assert.Equal(ValidationRules.DateNotInFuture, issue.Rule);
        Assert.Equal("Invoice date 2026-09-29 is in the future.", issue.Message);
    }

    // Duplicates

    [Fact]
    public void Earlier_invoice_with_same_number_is_flagged()
    {
        var invoice = ValidInvoice();
        DuplicateInvoice earlier = new(Guid.NewGuid(), InvoiceStatus.Approved, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

        var issue = Assert.Single(Validate(invoice, [earlier]));

        Assert.Equal("invoiceNumber", issue.Field);
        Assert.Equal(ValidationRules.DuplicateInvoiceNumber, issue.Rule);
        Assert.Equal("Invoice INV-1 from Acme was already uploaded on 2026-09-01 (Approved).", issue.Message);
    }

    [Fact]
    public void Several_duplicates_are_summarised_from_the_first()
    {
        var invoice = ValidInvoice();
        DuplicateInvoice[] duplicates =
        [
            new(Guid.NewGuid(), InvoiceStatus.PendingReview, new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero)),
            new(Guid.NewGuid(), InvoiceStatus.Approved, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)),
        ];

        var issue = Assert.Single(Validate(invoice, duplicates));

        Assert.Equal("Invoice INV-1 from Acme was already uploaded 2 times, first on 2026-09-02.", issue.Message);
    }

    [Fact]
    public void Invoice_is_not_a_duplicate_of_itself()
    {
        var invoice = ValidInvoice();

        Assert.Empty(Validate(invoice, [new DuplicateInvoice(invoice.Id, InvoiceStatus.PendingReview, DateTimeOffset.UtcNow)]));
    }

    private static IReadOnlyList<ValidationIssue> Validate(Invoice invoice, IReadOnlyList<DuplicateInvoice>? duplicates = null) =>
        InvoiceValidator.Validate(invoice, new ValidationContext(Today, duplicates ?? []));

    private static Invoice ValidInvoice(decimal[]? lines = null, decimal net = 100m, decimal vat = 20m, decimal total = 120m) =>
        new()
        {
            Id = Guid.NewGuid(),
            FileName = "a.pdf",
            FilePath = "a.pdf",
            Supplier = "Acme",
            InvoiceNumber = "INV-1",
            Date = new DateOnly(2026, 9, 1),
            Currency = "EUR",
            Net = net,
            Vat = vat,
            Total = total,
            Lines = [.. (lines ?? [100m]).Select(t => new InvoiceLine { Description = "Item", Qty = 1, UnitPrice = t, LineTotal = t })],
        };
}
