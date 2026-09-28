using Anthropic;
using InvoiceProcessor.Core.Extraction;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>
/// Builds the extractor for a provider and model. The clients behind them are created once and shared;
/// extractors are cheap, so one is built per extraction for whatever model is selected at that moment.
/// </summary>
public sealed class InvoiceExtractorFactory
{
    public static readonly IReadOnlyList<string> Providers = [AnthropicInvoiceExtractor.ProviderName, OllamaInvoiceExtractor.ProviderName];

    private readonly Lazy<AnthropicClient> _anthropic;
    private readonly HttpClient _ollama;

    public InvoiceExtractorFactory(LlmOptions options)
    {
        Options = options;

        // Falls back to the ANTHROPIC_API_KEY environment variable when Llm:ApiKey is not set.
        _anthropic = new(() => string.IsNullOrWhiteSpace(options.ApiKey) ? new AnthropicClient() : new AnthropicClient { ApiKey = options.ApiKey });
        _ollama = new HttpClient
        {
            BaseAddress = new Uri(options.Ollama.Endpoint.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(options.Ollama.TimeoutSeconds),
        };
        OllamaModels = new OllamaModels(_ollama);
    }

    /// <summary>The configured settings: the default provider and model, pricing, what is allowed.</summary>
    public LlmOptions Options { get; }

    public OllamaModels OllamaModels { get; }

    public bool HasAnthropicApiKey =>
        !string.IsNullOrWhiteSpace(Options.ApiKey) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    public IInvoiceExtractor Create(string provider, string model)
    {
        var options = Options.With(provider, model);

        return provider switch
        {
            AnthropicInvoiceExtractor.ProviderName => new AnthropicInvoiceExtractor(_anthropic.Value, options),
            OllamaInvoiceExtractor.ProviderName => new OllamaInvoiceExtractor(_ollama, options),
            _ => throw new InvalidOperationException($"Unknown LLM provider '{provider}'. Supported: {string.Join(", ", Providers)}."),
        };
    }
}
