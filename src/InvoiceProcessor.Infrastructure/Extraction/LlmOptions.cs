namespace InvoiceProcessor.Infrastructure.Extraction;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>
    /// Which <see cref="Core.Extraction.IInvoiceExtractor"/> to use: "Anthropic" or "Ollama". This is the default;
    /// an admin can pick another allowed provider and model in the app.
    /// </summary>
    public string Provider { get; set; } = "Anthropic";

    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// The providers admins may pick in the app; unset allows all. Set it to ["Ollama"] to guarantee that no
    /// document is ever sent to a cloud API, whatever is chosen in the UI.
    /// </summary>
    public string[]? AllowedProviders { get; set; }

    public bool IsAllowed(string provider) =>
        AllowedProviders is null || AllowedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>Set with user-secrets or the Llm__ApiKey environment variable, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    public int MaxTokens { get; set; } = 16000;

    /// <summary>Optional reasoning effort (low, medium, high, xhigh, max). Unset uses the model's default.</summary>
    public string? Effort { get; set; }

    /// <summary>USD per million tokens, keyed by model id, for the cost estimate in the extraction log.</summary>
    public Dictionary<string, ModelPricing> Pricing { get; set; } = [];

    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>These settings with another provider and model, for the model an admin picked.</summary>
    public LlmOptions With(string provider, string model)
    {
        var copy = (LlmOptions)MemberwiseClone();
        copy.Provider = provider;
        copy.Model = model;
        return copy;
    }
}

/// <summary>Settings for a local model served by Ollama. The model itself is <see cref="LlmOptions.Model"/>.</summary>
public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Context window in tokens. Ollama's default (4096) is too small for a page image or a long invoice.</summary>
    public int ContextLength { get; set; } = 16384;

    /// <summary>Local models on a CPU can take minutes per invoice.</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>How long Ollama keeps the model loaded after a request, e.g. "30m".</summary>
    public string KeepAlive { get; set; } = "30m";

    /// <summary>
    /// Thinking on or off for models that support it; unset leaves the model's default. Some models only think
    /// (qwen3-vl:8b) and ignore false; for extraction, pick an instruct variant (qwen3-vl:8b-instruct) instead.
    /// </summary>
    public bool? Think { get; set; }

    /// <summary>
    /// CPU threads when the model runs without a GPU; unset lets Ollama choose, which on CPUs with performance
    /// and efficiency cores may use only the performance ones.
    /// </summary>
    public int? Threads { get; set; }

    public DocumentPreparationOptions Documents { get; set; } = new();
}

/// <summary>How a PDF is turned into text or page images for a model without native PDF input.</summary>
public sealed class DocumentPreparationOptions
{
    /// <summary>A text layer shorter than this (non-whitespace characters) counts as a scan and the pages are rendered instead.</summary>
    public int MinTextLength { get; set; } = 50;

    /// <summary>Pages beyond this are left out of the request.</summary>
    public int MaxPages { get; set; } = 5;

    /// <summary>Resolution for rendering scanned pages. 150 keeps small print readable without a huge image.</summary>
    public int RenderDpi { get; set; } = 150;
}

public sealed class ModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }

    public decimal Estimate(long inputTokens, long outputTokens) =>
        (inputTokens * InputPerMillion + outputTokens * OutputPerMillion) / 1_000_000m;
}
