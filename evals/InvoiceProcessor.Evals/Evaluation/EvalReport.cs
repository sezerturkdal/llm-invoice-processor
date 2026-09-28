using System.Globalization;
using System.Text;
using System.Text.Json;

namespace InvoiceProcessor.Evals.Evaluation;

/// <summary>Builds the markdown comparison table (latest run per model and prompt) for the README.</summary>
public static class EvalReport
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static async Task<IReadOnlyList<EvalRun>> LoadLatestRunsAsync(string resultsDirectory)
    {
        if (!Directory.Exists(resultsDirectory))
        {
            return [];
        }

        List<EvalRun> runs = [];
        foreach (var file in Directory.GetFiles(resultsDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            if (await JsonSerializer.DeserializeAsync<EvalRun>(stream, ExpectedInvoice.JsonOptions) is { } run)
            {
                runs.Add(run);
            }
        }

        return [.. runs
            .GroupBy(r => (r.Model, r.PromptVersion))
            .Select(g => g.MaxBy(r => r.StartedAt)!)
            .OrderBy(r => r.Model, StringComparer.Ordinal)];
    }

    public static string ToMarkdown(IReadOnlyList<EvalRun> runs)
    {
        var sb = new StringBuilder();
        var columns = runs.Select(r => runs.Count(o => o.Model == r.Model) > 1 ? $"{r.Model} ({r.PromptVersion})" : r.Model).ToList();

        sb.AppendLine($"| Field | {string.Join(" | ", columns)} |");
        sb.AppendLine($"|---|{string.Concat(runs.Select(_ => "---:|"))}");

        foreach (var field in FieldComparer.HeaderFields.Concat(FieldComparer.LineFields))
        {
            sb.AppendLine($"| {Label(field)} | {string.Join(" | ", runs.Select(r => Accuracy(r, field)))} |");
        }

        sb.AppendLine($"| **All fields correct** | {string.Join(" | ", runs.Select(r => Fraction(r.Samples.Count(s => s.AllCorrect), r.Samples.Count)))} |");
        sb.AppendLine($"| Deliberate errors flagged by validation | {string.Join(" | ", runs.Select(r => Fraction(r.Samples.Count(s => s.ExpectedIssues.Count > 0 && s.Error is null && s.CaughtExpectedIssues), r.Samples.Count(s => s.ExpectedIssues.Count > 0))))} |");
        sb.AppendLine($"| Clean invoices without false alarms | {string.Join(" | ", runs.Select(r => Fraction(r.Samples.Count(s => s.ExpectedIssues.Count == 0 && s.Error is null && !s.FalseAlarm), r.Samples.Count(s => s.ExpectedIssues.Count == 0))))} |");
        sb.AppendLine($"| Misread invoices flagged for review | {string.Join(" | ", runs.Select(r => Fraction(r.Samples.Count(s => s.MisreadFlagged), r.Samples.Count(s => s.Misread))))} |");
        sb.AppendLine($"| Avg latency | {string.Join(" | ", runs.Select(r => (Average(r, s => s.LatencyMs) / 1000).ToString("0.0", Invariant) + " s"))} |");
        sb.AppendLine($"| Avg tokens in / out | {string.Join(" | ", runs.Select(r => $"{Average(r, s => s.InputTokens ?? 0):0} / {Average(r, s => s.OutputTokens ?? 0):0}"))} |");
        sb.AppendLine($"| Avg cost per invoice | {string.Join(" | ", runs.Select(r => "$" + Average(r, s => (double)(s.Cost ?? 0)).ToString("0.0000", Invariant)))} |");
        sb.AppendLine();
        sb.AppendLine($"_{runs.Max(r => r.Samples.Count)} synthetic invoices; latest run per model: {string.Join(", ", runs.Select(r => $"{r.Model} {r.StartedAt:yyyy-MM-dd}"))}._");

        return sb.ToString();
    }

    private static string Accuracy(EvalRun run, string field)
    {
        var results = run.AllFields.Where(f => f.Field == field).ToList();
        // Samples that errored have no field results; count them as wrong.
        var failed = field.StartsWith("line.", StringComparison.Ordinal) ? 0 : run.Samples.Count(s => s.Error is not null);
        return Percent(results.Count(f => f.Correct), results.Count + failed);
    }

    private static double Average(EvalRun run, Func<SampleResult, double> selector) =>
        run.Samples.Count == 0 ? 0 : run.Samples.Average(selector);

    private static string Percent(int correct, int total) =>
        total == 0 ? "–" : (100.0 * correct / total).ToString("0.#", Invariant) + "%";

    private static string Fraction(int count, int total) => total == 0 ? "–" : $"{count}/{total}";

    private static string Label(string field) => field switch
    {
        "invoiceNumber" => "Invoice number",
        "lineCount" => "Line count",
        "line.description" => "Line description",
        "line.qty" => "Line quantity",
        "line.unitPrice" => "Line unit price",
        "line.lineTotal" => "Line total",
        "vat" => "VAT",
        _ => char.ToUpperInvariant(field[0]) + field[1..],
    };
}
