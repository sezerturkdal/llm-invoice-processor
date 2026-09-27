namespace InvoiceProcessor.Evals.Evaluation;

/// <summary>One eval run, saved as evals/results/&lt;timestamp&gt;-&lt;model&gt;.json and compared by the report.</summary>
public sealed record EvalRun(
    string Provider,
    string Model,
    /// <summary>Hash of the system prompt and schema, so runs with different prompts are told apart.</summary>
    string PromptVersion,
    DateTimeOffset StartedAt,
    IReadOnlyList<SampleResult> Samples)
{
    public int Succeeded => Samples.Count(s => s.Error is null);

    public IEnumerable<FieldResult> AllFields => Samples.SelectMany(s => s.Fields);
}

public sealed record SampleResult(
    string Sample,
    IReadOnlyList<FieldResult> Fields,
    IReadOnlyList<string> ExpectedIssues,
    IReadOnlyList<string> RaisedIssues,
    int? InputTokens,
    int? OutputTokens,
    long LatencyMs,
    decimal? Cost,
    string? Error)
{
    public bool AllCorrect => Error is null && Fields.All(f => f.Correct);

    /// <summary>Every deliberate error was flagged by validation.</summary>
    public bool CaughtExpectedIssues => ExpectedIssues.All(RaisedIssues.Contains);

    /// <summary>Validation flagged something on a sample that has no deliberate error.</summary>
    public bool FalseAlarm => ExpectedIssues.Count == 0 && RaisedIssues.Count > 0;
}
