namespace InvoiceProcessor.Evals;

/// <summary>Repository locations, found from the solution file so the tool works from any directory.</summary>
public sealed record EvalPaths(string Root)
{
    public string Samples => Path.Combine(Root, "evals", "samples");
    public string Expected => Path.Combine(Root, "evals", "expected");
    public string Results => Path.Combine(Root, "evals", "results");
    public string ApiSettings => Path.Combine(Root, "src", "InvoiceProcessor.Api", "appsettings.json");

    public static EvalPaths Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InvoiceProcessor.slnx")))
            {
                return new EvalPaths(dir.FullName);
            }
        }

        throw new InvalidOperationException("Could not find the repository root (InvoiceProcessor.slnx).");
    }
}
