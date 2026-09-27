using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Evals.Evaluation;

namespace InvoiceProcessor.Evals.Tests;

public class FieldComparerTests
{
    private static readonly ExtractedInvoice Expected = new(
        "Harbourside Print Co. Ltd", "HPC-7731", new DateOnly(2026, 2, 4), "GBP",
        [new ExtractedInvoiceLine("Business cards, 400gsm matt", 3, 38.00m, 114.00m), new ExtractedInvoiceLine("Poster", 4, 22.50m, 90.00m)],
        204.00m, 40.80m, 244.80m);

    [Fact]
    public void Identical_invoice_is_all_correct()
    {
        var results = FieldComparer.Compare(Expected, Expected);

        Assert.All(results, r => Assert.True(r.Correct, r.Field));
        Assert.Equal(8 + 2 * 4, results.Count);
    }

    [Fact]
    public void Case_spacing_and_trailing_punctuation_do_not_matter_for_text()
    {
        var actual = Expected with { Supplier = "  harbourside  print co. ltd. ", InvoiceNumber = "hpc - 7731".Replace(" ", ""), Currency = "gbp" };

        var results = FieldComparer.Compare(Expected, actual);

        Assert.True(Field(results, "supplier").Correct);
        Assert.True(Field(results, "invoiceNumber").Correct);
        Assert.True(Field(results, "currency").Correct);
    }

    [Fact]
    public void Any_other_character_in_the_invoice_number_matters()
    {
        var results = FieldComparer.Compare(Expected, Expected with { InvoiceNumber = "HPC-7713" });

        Assert.False(Field(results, "invoiceNumber").Correct);
    }

    [Fact]
    public void Amounts_must_match_to_the_cent()
    {
        var results = FieldComparer.Compare(Expected, Expected with { Net = 204.004m, Total = 244.81m });

        Assert.True(Field(results, "net").Correct);
        Assert.False(Field(results, "total").Correct);
    }

    [Fact]
    public void Null_matches_only_null()
    {
        var expected = Expected with { Vat = null };

        Assert.True(Field(FieldComparer.Compare(expected, expected), "vat").Correct);
        Assert.False(Field(FieldComparer.Compare(expected, Expected), "vat").Correct);
        Assert.False(Field(FieldComparer.Compare(Expected, expected), "vat").Correct);
    }

    [Fact]
    public void Description_with_extra_code_appended_still_matches()
    {
        var actual = Expected with { Lines = [Expected.Lines[0] with { Description = "Business cards, 400gsm matt BC-400" }, Expected.Lines[1]] };

        var line = FieldComparer.Compare(Expected, actual).First(r => r.Field == "line.description");

        Assert.True(line.Correct);
    }

    [Fact]
    public void Missing_line_fails_count_and_every_field_of_that_line()
    {
        var actual = Expected with { Lines = [Expected.Lines[0]] };

        var results = FieldComparer.Compare(Expected, actual);

        Assert.False(Field(results, "lineCount").Correct);
        Assert.All(results.Where(r => r.Line == 1), r => Assert.False(r.Correct));
        Assert.All(results.Where(r => r.Line == 0), r => Assert.True(r.Correct));
    }

    private static FieldResult Field(IEnumerable<FieldResult> results, string field) => results.Single(r => r.Field == field);
}
