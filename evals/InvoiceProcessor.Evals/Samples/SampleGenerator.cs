using System.Text.Json;
using InvoiceProcessor.Evals.Evaluation;

namespace InvoiceProcessor.Evals.Samples;

/// <summary>Writes each catalog sample as a document in evals/samples and its ground truth in evals/expected.</summary>
public static class SampleGenerator
{
    public static async Task<int> RunAsync(EvalPaths paths, bool keepHtml)
    {
        var browser = BrowserRenderer.Find();
        var htmlDir = Path.Combine(Path.GetTempPath(), "invoiceprocessor-evals-html");
        Directory.CreateDirectory(htmlDir);
        Directory.CreateDirectory(paths.Samples);
        Directory.CreateDirectory(paths.Expected);

        foreach (var sample in SampleCatalog.All)
        {
            var htmlPath = Path.Combine(keepHtml ? paths.Samples : htmlDir, sample.Id + ".html");
            await File.WriteAllTextAsync(htmlPath, InvoiceHtmlRenderer.Render(sample));

            var documentPath = Path.Combine(paths.Samples, sample.FileName);
            File.Delete(documentPath);
            await browser.RenderAsync(htmlPath, documentPath, sample.Format);

            var expected = ExpectedInvoice.From(sample.ToExpected(), sample.ExpectedIssues, sample.Purpose);
            await File.WriteAllTextAsync(
                Path.Combine(paths.Expected, sample.Id + ".json"),
                JsonSerializer.Serialize(expected, ExpectedInvoice.JsonOptions) + Environment.NewLine);

            Console.WriteLine($"  {sample.FileName,-44} {new FileInfo(documentPath).Length / 1024,5} KB  {sample.Purpose}");
        }

        Console.WriteLine($"Wrote {SampleCatalog.All.Count} samples to {paths.Samples}");
        return 0;
    }
}
