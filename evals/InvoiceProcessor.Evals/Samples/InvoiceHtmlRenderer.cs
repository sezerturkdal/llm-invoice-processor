using System.Globalization;
using System.Net;
using System.Text;

namespace InvoiceProcessor.Evals.Samples;

/// <summary>Renders a sample as a print-ready A4 HTML page in one of three layouts.</summary>
public static class InvoiceHtmlRenderer
{
    public static string Render(SampleInvoice sample)
    {
        var f = new Formatter(sample);
        var body = sample.Layout switch
        {
            SampleLayout.Classic => Classic(sample, f),
            SampleLayout.Modern => Modern(sample, f),
            SampleLayout.Minimal => Minimal(sample, f),
            _ => throw new ArgumentOutOfRangeException(nameof(sample)),
        };

        // Images get a slight tilt and grey cast, like a scan or phone photo. A screenshot ignores
        // @page margins, so the page margin is padding here, wide enough that the tilt clips nothing.
        var scanStyle = sample.Format == SampleFormat.Png
            ? """
              html { background: #e9e7e1; }
              body { margin: 40px; padding: 70px 80px; background: #fbfaf7; font-size: 12pt; transform: rotate(-0.6deg); transform-origin: center top; filter: grayscale(0.35) contrast(1.08); box-shadow: 0 2px 10px rgba(0,0,0,.15); }
              """
            : "";

        return $$"""
            <!doctype html>
            <html lang="{{sample.Culture[..2]}}">
            <head>
            <meta charset="utf-8">
            <title>{{E(sample.Labels.Invoice)}} {{E(sample.InvoiceNumber)}}</title>
            <style>
              @page { size: A4; margin: 16mm 15mm 18mm; @bottom-right { content: "{{E(sample.Labels.Page)}} " counter(page) " / " counter(pages); font: 8pt sans-serif; color: #777; } }
              * { box-sizing: border-box; }
              body { margin: 0; color: #1d1d1f; font: 9.5pt/1.45 {{FontFor(sample.Layout)}}; }
              table { width: 100%; border-collapse: collapse; }
              thead { display: table-header-group; }
              tr { break-inside: avoid; }
              .num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
              .muted { color: #6b6b70; }
              .small { font-size: 8pt; }
              {{LayoutCss(sample.Layout)}}
              {{scanStyle}}
            </style>
            </head>
            <body>
            {{body}}
            </body>
            </html>
            """;
    }

    private static string Classic(SampleInvoice s, Formatter f) => $$"""
        <div class="top">
          <div>
            <div class="supplier">{{E(s.Supplier.Name)}}</div>
            <div class="muted">{{Lines(s.Supplier.Address)}}</div>
            {{TaxId(s, f)}}
          </div>
          <div class="title">{{E(s.Labels.Invoice).ToUpper(f.Culture)}}</div>
        </div>
        <div class="split">
          <div>
            <div class="caption">{{E(s.Labels.BillTo)}}</div>
            <strong>{{E(s.Customer.Name)}}</strong><br>{{Lines(s.Customer.Address)}}
          </div>
          <table class="meta">{{MetaRows(s, f)}}</table>
        </div>
        {{LineTable(s, f)}}
        {{Totals(s, f)}}
        {{Note(s)}}
        """;

    private static string Modern(SampleInvoice s, Formatter f) => $$"""
        <div class="band">
          <div>
            <div class="supplier">{{E(s.Supplier.Name)}}</div>
            <div class="small">{{string.Join(" · ", s.Supplier.Address.Select(E))}}</div>
          </div>
          <div class="title">{{E(s.Labels.Invoice)}}</div>
        </div>
        <div class="cards">
          <div class="card"><div class="caption">{{E(s.Labels.BillTo)}}</div><strong>{{E(s.Customer.Name)}}</strong><br>{{Lines(s.Customer.Address)}}</div>
          <div class="card"><table class="meta">{{MetaRows(s, f)}}</table></div>
        </div>
        {{LineTable(s, f)}}
        {{Totals(s, f)}}
        {{Note(s)}}
        <div class="footer small muted">{{E(s.Supplier.Name)}} · {{string.Join(", ", s.Supplier.Address.Select(E))}}{{(s.Supplier.TaxId is null ? "" : $" · {E(s.Labels.TaxId)} {E(s.Supplier.TaxId)}")}}</div>
        """;

    private static string Minimal(SampleInvoice s, Formatter f) => $$"""
        <div class="head">
          <div class="title">{{E(s.Labels.Invoice)}} {{E(s.InvoiceNumber)}}</div>
          <div class="muted">{{E(s.Labels.InvoiceDate)}}: {{f.Date(s.Date)}}</div>
        </div>
        <div class="split">
          <div><strong>{{E(s.Supplier.Name)}}</strong><br>{{Lines(s.Supplier.Address)}}{{TaxId(s, f)}}</div>
          <div><span class="caption">{{E(s.Labels.BillTo)}}</span><br><strong>{{E(s.Customer.Name)}}</strong><br>{{Lines(s.Customer.Address)}}</div>
          <table class="meta">{{MetaRows(s, f, includeNumberAndDate: false)}}</table>
        </div>
        {{LineTable(s, f)}}
        {{Totals(s, f)}}
        {{Note(s)}}
        """;

    private static string MetaRows(SampleInvoice s, Formatter f, bool includeNumberAndDate = true)
    {
        var rows = new List<(string Label, string Value)>();
        if (includeNumberAndDate)
        {
            rows.Add((s.Labels.InvoiceNumber, s.InvoiceNumber));
            rows.Add((s.Labels.InvoiceDate, f.Date(s.Date)));
        }

        if (s.OrderDate is { } orderDate) rows.Add((s.Labels.OrderDate, f.Date(orderDate)));
        if (s.DeliveryDate is { } deliveryDate) rows.Add((s.Labels.DeliveryDate, f.Date(deliveryDate)));
        rows.AddRange(s.References);
        if (s.DueDate is { } dueDate) rows.Add((s.Labels.DueDate, f.Date(dueDate)));

        return string.Concat(rows.Select(r => $"<tr><th>{E(r.Label)}</th><td>{E(r.Value)}</td></tr>"));
    }

    private static string LineTable(SampleInvoice s, Formatter f)
    {
        var rate = s.ShowVatRateColumn ? $"<th class=\"num\">{E(s.Labels.VatRate)}</th>" : "";
        var totalHeader = s.LinesIncludeVat ? s.Labels.LineTotalInclVat : s.Labels.LineTotal;

        var rows = new StringBuilder();
        foreach (var line in s.Lines)
        {
            var sku = line.Sku is null ? "" : $"<div class=\"small muted\">{E(line.Sku)}</div>";
            var rateCell = s.ShowVatRateColumn ? $"<td class=\"num\">{f.Rate(line.VatRate)}</td>" : "";
            rows.Append($"<tr><td>{E(line.Description)}{sku}</td><td class=\"num\">{f.Quantity(line.Qty)}</td><td class=\"num\">{f.Amount(line.UnitPrice)}</td>{rateCell}<td class=\"num\">{f.Amount(line.LineTotal)}</td></tr>");
        }

        return $$"""
            <table class="lines">
              <thead><tr><th>{{E(s.Labels.Description)}}</th><th class="num">{{E(s.Labels.Qty)}}</th><th class="num">{{E(s.Labels.UnitPrice)}}</th>{{rate}}<th class="num">{{E(totalHeader)}}</th></tr></thead>
              <tbody>{{rows}}</tbody>
            </table>
            """;
    }

    private static string Totals(SampleInvoice s, Formatter f)
    {
        var vatRows = string.Concat(s.VatRows.Select(r =>
            $"<tr><th>{E(string.Format(f.Culture, s.Labels.VatOn, f.Rate(r.Rate), f.Amount(r.Base)))}</th><td class=\"num\">{f.Amount(r.Amount)}</td></tr>"));

        return $$"""
            <table class="totals">
              <tr><th>{{E(s.Labels.Net)}}</th><td class="num">{{f.Amount(s.Net)}}</td></tr>
              {{vatRows}}
              <tr class="grand"><th>{{E(s.Labels.Total)}}</th><td class="num">{{f.Amount(s.Total)}}</td></tr>
            </table>
            """;
    }

    private static string TaxId(SampleInvoice s, Formatter f) =>
        s.Supplier.TaxId is null ? "" : $"<div class=\"small muted\">{E(s.Labels.TaxId)} {E(s.Supplier.TaxId)}</div>";

    private static string Note(SampleInvoice s) => s.Note is null ? "" : $"<p class=\"note small muted\">{E(s.Note)}</p>";

    private static string Lines(IEnumerable<string> lines) => string.Join("<br>", lines.Select(E));

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string FontFor(SampleLayout layout) => layout switch
    {
        SampleLayout.Classic => "Georgia, 'Times New Roman', serif",
        SampleLayout.Modern => "'Segoe UI', Arial, sans-serif",
        _ => "'Consolas', 'Courier New', monospace",
    };

    private static string LayoutCss(SampleLayout layout) => layout switch
    {
        SampleLayout.Classic => """
            .top { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #1d1d1f; padding-bottom: 10px; }
            .supplier { font-size: 15pt; font-weight: bold; }
            .title { font-size: 22pt; letter-spacing: 3px; }
            .split { display: flex; justify-content: space-between; gap: 24px; margin: 18px 0; }
            .caption { font-size: 8pt; text-transform: uppercase; letter-spacing: 1px; color: #6b6b70; }
            .meta { width: auto; } .meta th { text-align: left; font-weight: normal; color: #6b6b70; padding: 1px 14px 1px 0; } .meta td { text-align: right; }
            .lines th { border-bottom: 1px solid #1d1d1f; text-align: left; padding: 5px 6px; font-size: 8.5pt; }
            .lines td { border-bottom: 1px solid #ddd; padding: 5px 6px; vertical-align: top; }
            .totals { width: 55%; margin: 14px 0 0 auto; } .totals th { text-align: left; font-weight: normal; padding: 3px 6px; } .totals td { padding: 3px 6px; }
            .totals .grand th, .totals .grand td { border-top: 2px solid #1d1d1f; font-weight: bold; font-size: 11pt; padding-top: 6px; }
            .note { margin-top: 28px; }
            """,
        SampleLayout.Modern => """
            .band { display: flex; justify-content: space-between; align-items: center; background: #0f4c5c; color: #fff; padding: 16px 20px; border-radius: 6px; }
            .supplier { font-size: 14pt; font-weight: 600; } .title { font-size: 20pt; font-weight: 300; }
            .cards { display: flex; gap: 12px; margin: 14px 0 18px; } .card { flex: 1; background: #f1f5f6; border-radius: 6px; padding: 10px 14px; }
            .caption { font-size: 8pt; font-weight: 600; color: #0f4c5c; text-transform: uppercase; }
            .meta th { text-align: left; font-weight: normal; color: #55606a; padding: 1px 10px 1px 0; } .meta td { text-align: right; font-weight: 600; }
            .lines th { background: #0f4c5c; color: #fff; text-align: left; padding: 6px 8px; font-weight: 600; font-size: 8.5pt; }
            .lines td { padding: 6px 8px; border-bottom: 1px solid #e3e8ea; vertical-align: top; } .lines tbody tr:nth-child(even) td { background: #f8fafb; }
            .totals { width: 50%; margin: 14px 0 0 auto; } .totals th { text-align: left; font-weight: normal; padding: 3px 8px; } .totals td { padding: 3px 8px; }
            .totals .grand th, .totals .grand td { background: #0f4c5c; color: #fff; font-weight: 600; font-size: 11pt; padding: 7px 8px; }
            .note { margin-top: 20px; } .footer { margin-top: 36px; border-top: 1px solid #e3e8ea; padding-top: 8px; }
            """,
        _ => """
            .head { display: flex; justify-content: space-between; align-items: baseline; border-bottom: 1px dashed #888; padding-bottom: 8px; }
            .title { font-size: 13pt; font-weight: bold; }
            .split { display: flex; justify-content: space-between; gap: 18px; margin: 14px 0; }
            .caption { color: #6b6b70; }
            .meta { width: auto; } .meta th { text-align: left; font-weight: normal; color: #6b6b70; padding: 0 10px 0 0; }
            .lines th { text-align: left; border-top: 1px dashed #888; border-bottom: 1px dashed #888; padding: 4px; font-weight: normal; }
            .lines td { padding: 4px; vertical-align: top; }
            .totals { width: 60%; margin: 10px 0 0 auto; border-top: 1px dashed #888; } .totals th { text-align: left; font-weight: normal; padding: 2px 4px; } .totals td { padding: 2px 4px; }
            .totals .grand th, .totals .grand td { font-weight: bold; border-top: 1px dashed #888; padding-top: 4px; }
            .note { margin-top: 20px; }
            """,
    };

    /// <summary>Culture-specific printing of amounts, quantities, rates and dates.</summary>
    private sealed class Formatter(SampleInvoice sample)
    {
        public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(sample.Culture);

        public string Amount(decimal value)
        {
            var number = Math.Abs(value).ToString("N2", Culture);
            var sign = value < 0 ? "-" : "";
            return sample.Money.SymbolAfter ? $"{sign}{number} {sample.Money.Symbol}" : $"{sign}{sample.Money.Symbol}{number}";
        }

        public string Quantity(decimal value) => value.ToString("0.##", Culture);

        public string Rate(decimal rate) => (rate * 100).ToString("0.##", Culture) + (sample.Culture.StartsWith("en", StringComparison.Ordinal) ? "%" : " %");

        public string Date(DateOnly date) => date.ToString(sample.DateFormat, Culture);
    }
}
