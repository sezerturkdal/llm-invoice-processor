namespace InvoiceProcessor.Infrastructure.Extraction;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Which <see cref="Core.Extraction.IInvoiceExtractor"/> to use, e.g. "Anthropic".</summary>
    public string Provider { get; set; } = "Anthropic";

    public string Model { get; set; } = "claude-opus-5";

    /// <summary>Set with user-secrets or the Llm__ApiKey environment variable, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    public int MaxTokens { get; set; } = 16000;

    /// <summary>Optional reasoning effort (low, medium, high, xhigh, max). Unset uses the model's default.</summary>
    public string? Effort { get; set; }

    /// <summary>USD per million tokens, keyed by model id, for the cost estimate in the extraction log.</summary>
    public Dictionary<string, ModelPricing> Pricing { get; set; } = [];
}

public sealed class ModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }

    public decimal Estimate(long inputTokens, long outputTokens) =>
        (inputTokens * InputPerMillion + outputTokens * OutputPerMillion) / 1_000_000m;
}
