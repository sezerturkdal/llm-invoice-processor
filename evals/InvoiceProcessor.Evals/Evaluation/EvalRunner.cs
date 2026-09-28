using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Core.Validation;
using InvoiceProcessor.Infrastructure.Extraction.Prompts;

namespace InvoiceProcessor.Evals.Evaluation;

/// <summary>Runs every sample that has an expected file through the extractor and scores the result.</summary>
public sealed class EvalRunner(IInvoiceExtractor extractor, EvalPaths paths, int parallelism)
{
    public async Task<EvalRun> RunAsync(IReadOnlyList<string> samplePaths, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new SampleResult[samplePaths.Count];
        using var gate = new SemaphoreSlim(parallelism);

        await Task.WhenAll(samplePaths.Select(async (path, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                results[index] = await RunSampleAsync(path, cancellationToken);
                Print(results[index]);
            }
            finally
            {
                gate.Release();
            }
        }));

        return new EvalRun(extractor.Provider, extractor.Model, PromptVersion(), startedAt, results);
    }

    private async Task<SampleResult> RunSampleAsync(string documentPath, CancellationToken cancellationToken)
    {
        var sample = Path.GetFileNameWithoutExtension(documentPath);
        var expected = await ExpectedInvoice.LoadAsync(Path.Combine(paths.Expected, sample + ".json"));
        var expectedIssues = expected.ExpectedIssues ?? [];

        var content = await File.ReadAllBytesAsync(documentPath, cancellationToken);
        var fileType = InvoiceFileType.Detect(content) ?? throw new InvalidDataException($"{documentPath} is not a PDF, PNG or JPEG.");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await extractor.ExtractAsync(new InvoiceDocument(content, fileType), cancellationToken);
            var fields = FieldComparer.Compare(expected.Invoice, result.Invoice);
            var usage = result.Usage;

            return new SampleResult(sample, fields, expectedIssues, RaisedIssues(result.Invoice), usage.InputTokens, usage.OutputTokens, usage.LatencyMs, usage.CostEstimate, Error: null);
        }
        // An HTTP timeout is an OperationCanceledException too, but a failed sample, not a cancelled run.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var usage = (ex as InvoiceExtractionException)?.Usage;
            return new SampleResult(sample, [], expectedIssues, [], usage?.InputTokens, usage?.OutputTokens, stopwatch.ElapsedMilliseconds, usage?.CostEstimate, ex.Message);
        }
    }

    // The same rules the API runs after extraction, minus the duplicate check (it needs the database).
    private static IReadOnlyList<string> RaisedIssues(ExtractedInvoice extracted)
    {
        var invoice = new Invoice { FileName = "eval", FilePath = "eval" };
        invoice.ApplyExtraction(extracted, "eval");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return [.. InvoiceValidator.Validate(invoice, new ValidationContext(today, [])).Select(i => i.Rule).Distinct()];
    }

    private static string PromptVersion()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            PromptLibrary.InvoiceExtractionSystem + PromptLibrary.InvoiceExtractionSchema + PromptLibrary.InvoiceTextInput));
        return Convert.ToHexStringLower(hash)[..8];
    }

    private static readonly Lock ConsoleLock = new();

    private static void Print(SampleResult result)
    {
        lock (ConsoleLock)
        {
            if (result.Error is not null)
            {
                Console.WriteLine($"  ✗ {result.Sample,-40} ERROR: {result.Error}");
                return;
            }

            var correct = result.Fields.Count(f => f.Correct);
            var mark = result.AllCorrect ? "✓" : "•";
            var issues = result.ExpectedIssues.Count > 0
                ? (result.CaughtExpectedIssues ? "  validation caught the error" : "  validation MISSED the error")
                : result.FalseAlarm ? $"  false alarm: {string.Join(", ", result.RaisedIssues)}"
                : result.MisreadFlagged ? $"  misread, flagged: {string.Join(", ", result.RaisedIssues)}"
                : result.Misread ? "  misread, NOT flagged" : "";
            Console.WriteLine($"  {mark} {result.Sample,-40} {correct,3}/{result.Fields.Count,-3} fields  {result.LatencyMs / 1000.0,5:0.0}s  ${result.Cost:0.0000}{issues}");

            foreach (var miss in result.Fields.Where(f => !f.Correct))
            {
                var where = miss.Line is { } line ? $"[{line}]" : "";
                Console.WriteLine($"        {miss.Field}{where}: expected {Show(miss.Expected)}, got {Show(miss.Actual)}");
            }
        }
    }

    private static string Show(string? value) => value is null ? "null" : $"\"{Truncate(value, 70)}\"";

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length] + "…";

    public static async Task<string> SaveAsync(EvalRun run, string directory)
    {
        Directory.CreateDirectory(directory);
        // Ollama model ids contain ':' (qwen3-vl:8b), which a Windows file name cannot.
        var model = string.Concat(run.Model.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
        var path = Path.Combine(directory, $"{run.StartedAt:yyyyMMdd-HHmmss}-{model}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(run, ExpectedInvoice.JsonOptions) + Environment.NewLine);
        return path;
    }
}
