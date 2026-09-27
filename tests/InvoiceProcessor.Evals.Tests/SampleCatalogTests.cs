using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Core.Validation;
using InvoiceProcessor.Evals.Samples;

namespace InvoiceProcessor.Evals.Tests;

/// <summary>The ground truth has to be right before it can score anything.</summary>
public class SampleCatalogTests
{
    public static TheoryData<string> SampleIds => [.. SampleCatalog.All.Select(s => s.Id)];

    [Fact]
    public void Sample_ids_are_unique_and_synthetic()
    {
        var ids = SampleCatalog.All.Select(s => s.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.StartsWith("synthetic-", id));
    }

    // Clean samples must pass every rule, and each error sample must trip exactly the rules it was
    // built to trip; otherwise the "validation caught it" score would measure the samples, not the code.
    [Theory]
    [MemberData(nameof(SampleIds))]
    public void Expected_data_trips_exactly_the_intended_validation_rules(string id)
    {
        var sample = SampleCatalog.All.Single(s => s.Id == id);
        var invoice = new Invoice { FileName = "x", FilePath = "x" };
        invoice.ApplyExtraction(sample.ToExpected(), "test");

        var raised = InvoiceValidator.Validate(invoice, new ValidationContext(new DateOnly(2026, 9, 27), []))
            .Select(i => i.Rule).Distinct().Order();

        Assert.Equal(sample.ExpectedIssues.Order(), raised);
    }

    [Theory]
    [MemberData(nameof(SampleIds))]
    public void Printed_vat_rows_add_up_to_the_vat_amount(string id)
    {
        var sample = SampleCatalog.All.Single(s => s.Id == id);

        Assert.Equal(sample.Vat, sample.VatRows.Sum(r => r.Amount));
    }

    [Fact]
    public void Gross_pricing_makes_lines_sum_to_the_total()
    {
        var p = Pricing.Gross(("A", 2, 149.00m, 0.23m, null), ("B", 1, 279.00m, 0.23m, null));

        Assert.Equal(p.TotalAmount, p.Lines.Sum(l => l.LineTotal));
        Assert.Equal(577.00m, p.NetAmount);
        Assert.Equal(p.TotalAmount - p.NetAmount, p.VatAmount);
    }

    [Fact]
    public void Net_pricing_rounds_vat_per_rate()
    {
        var p = Pricing.Net(("Food", 24, 14.50m, 0.07m, null), ("Drinks", 24, 3.80m, 0.19m, null), ("Juice", 12, 4.20m, 0.19m, null));

        Assert.Equal([24.36m, 26.90m], p.VatRows.Select(r => r.Amount));
        Assert.Equal(p.NetAmount + p.VatAmount, p.TotalAmount);
    }
}
