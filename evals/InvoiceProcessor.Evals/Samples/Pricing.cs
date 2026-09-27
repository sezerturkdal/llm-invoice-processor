namespace InvoiceProcessor.Evals.Samples;

/// <summary>Computes consistent line totals, VAT rows and totals the way an invoicing system would.</summary>
public sealed record Pricing(IReadOnlyList<SampleLine> Lines, IReadOnlyList<VatRow> VatRows, decimal NetAmount, decimal VatAmount, decimal TotalAmount)
{
    /// <summary>Line totals before tax; VAT is computed per rate on the summed net.</summary>
    public static Pricing Net(params (string Description, decimal Qty, decimal UnitPrice, decimal VatRate, string? Sku)[] items)
    {
        var lines = items.Select(i => new SampleLine(i.Description, i.Qty, i.UnitPrice, Round(i.Qty * i.UnitPrice), i.VatRate, i.Sku)).ToList();

        var rows = lines
            .GroupBy(l => l.VatRate)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var @base = g.Sum(l => l.LineTotal);
                return new VatRow(g.Key, @base, Round(@base * g.Key));
            })
            .ToList();

        var net = lines.Sum(l => l.LineTotal);
        var vat = rows.Sum(r => r.Amount);
        return new Pricing(lines, rows, net, vat, net + vat);
    }

    /// <summary>
    /// Unit prices before tax, line totals including tax (as some marketplaces print them).
    /// The totals follow from the lines, so the gross lines add up to the total exactly.
    /// </summary>
    public static Pricing Gross(params (string Description, decimal Qty, decimal UnitPrice, decimal VatRate, string? Sku)[] items)
    {
        var priced = items.Select(i =>
        {
            var net = Round(i.Qty * i.UnitPrice);
            return (Line: new SampleLine(i.Description, i.Qty, i.UnitPrice, Round(net * (1 + i.VatRate)), i.VatRate, i.Sku), Net: net);
        }).ToList();

        var rows = priced
            .GroupBy(p => p.Line.VatRate)
            .OrderBy(g => g.Key)
            .Select(g => new VatRow(g.Key, g.Sum(p => p.Net), g.Sum(p => p.Line.LineTotal) - g.Sum(p => p.Net)))
            .ToList();

        var netTotal = priced.Sum(p => p.Net);
        var total = priced.Sum(p => p.Line.LineTotal);
        return new Pricing([.. priced.Select(p => p.Line)], rows, netTotal, total - netTotal, total);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
