using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Evals;
using InvoiceProcessor.Evals.Evaluation;
using InvoiceProcessor.Evals.Samples;
using InvoiceProcessor.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Eval runner for the extraction step.
//
//   generate                 render the synthetic samples (needs Edge or Chrome) and their expected JSON
//   run [--model <id>]       extract every sample, score it against evals/expected, save the run
//       [--samples <prefix>] only samples whose name starts with the prefix (default: synthetic-)
//       [--parallel <n>]     concurrent extractions (default 3)
//   report                   print the markdown comparison of the latest run per model

var paths = EvalPaths.Find();
var command = args.FirstOrDefault() ?? "help";

return command switch
{
    "generate" => await SampleGenerator.RunAsync(paths, keepHtml: args.Contains("--html")),
    "run" => await RunAsync(),
    "report" => await ReportAsync(),
    _ => Help(),
};

async Task<int> RunAsync()
{
    var prefix = Option("--samples") ?? "synthetic-";
    var samples = Directory.GetFiles(paths.Samples)
        .Where(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        .Where(p => InvoiceFileExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
        .Where(p => File.Exists(Path.Combine(paths.Expected, Path.GetFileNameWithoutExtension(p) + ".json")))
        .Order(StringComparer.Ordinal)
        .ToList();

    if (samples.Count == 0)
    {
        Console.Error.WriteLine($"No samples starting with '{prefix}' that have an expected file. Run 'generate' first.");
        return 1;
    }

    // Same configuration the API uses: its appsettings (model, pricing), then user-secrets and
    // environment variables (API key). --model overrides the configured model.
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(paths.ApiSettings, optional: false)
        .AddUserSecrets(typeof(EvalPaths).Assembly)
        .AddEnvironmentVariables()
        .AddInMemoryCollection(Option("--model") is { } model ? [new("Llm:Model", model)] : [])
        .Build();

    var extractor = new ServiceCollection().AddInvoiceExtractor(configuration).BuildServiceProvider().GetRequiredService<IInvoiceExtractor>();
    var parallelism = int.TryParse(Option("--parallel"), out var n) && n > 0 ? n : 3;

    Console.WriteLine($"Running {samples.Count} samples with {extractor.Provider} {extractor.Model}…");
    var run = await new EvalRunner(extractor, paths, parallelism).RunAsync(samples, CancellationToken.None);

    // Runs that include real (local-only) invoices are kept out of the committed results.
    var onlySynthetic = samples.All(p => Path.GetFileName(p).StartsWith("synthetic-", StringComparison.Ordinal));
    var saved = await EvalRunner.SaveAsync(run, onlySynthetic ? paths.Results : Path.Combine(paths.Results, "local"));

    Console.WriteLine();
    Console.WriteLine(EvalReport.ToMarkdown([run]));
    Console.WriteLine($"Saved {saved}");
    return run.Succeeded == run.Samples.Count ? 0 : 1;
}

async Task<int> ReportAsync()
{
    var runs = await EvalReport.LoadLatestRunsAsync(paths.Results);
    if (runs.Count == 0)
    {
        Console.Error.WriteLine("No results yet. Run 'run' first.");
        return 1;
    }

    Console.WriteLine(EvalReport.ToMarkdown(runs));
    return 0;
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

int Help()
{
    Console.WriteLine("Usage: dotnet run --project evals/InvoiceProcessor.Evals -- <generate [--html] | run [--model <id>] [--samples <prefix>] [--parallel <n>] | report>");
    return command == "help" ? 0 : 1;
}

internal static partial class Program
{
    private static readonly HashSet<string> InvoiceFileExtensions = [".pdf", ".png", ".jpg", ".jpeg"];
}
