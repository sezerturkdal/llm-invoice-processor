namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>One provider in the model picker: whether configuration allows it, whether it can be used now, and its models.</summary>
public sealed record ProviderChoice(string Provider, bool Allowed, string? Unavailable, IReadOnlyList<ModelChoice> Models)
{
    public bool IsSelectable => Allowed && Unavailable is null;
}

/// <param name="SupportsImages">False for text-only models, which can read only PDFs with a text layer.</param>
/// <param name="InputPerMillion">USD per million tokens; null for local models, which cost nothing per call.</param>
public sealed record ModelChoice(
    string Id,
    bool SupportsImages,
    string? Size,
    decimal? InputPerMillion,
    decimal? OutputPerMillion);

/// <summary>
/// The models an admin can pick from. Claude models are the ones with a price in <c>Llm:Pricing</c>, so every
/// extraction can be costed; Ollama models are whatever is installed locally, read live from Ollama.
/// </summary>
public sealed class ExtractionModelCatalog(InvoiceExtractorFactory factory)
{
    private static readonly TimeSpan OllamaTimeout = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<ProviderChoice>> GetAsync(CancellationToken cancellationToken) =>
    [
        Anthropic(),
        await OllamaAsync(cancellationToken),
    ];

    /// <summary>The model if it can be selected now, or why not.</summary>
    public async Task<(ModelChoice? Model, string? Problem)> FindAsync(string provider, string model, CancellationToken cancellationToken)
    {
        var choice = (await GetAsync(cancellationToken)).FirstOrDefault(p => p.Provider == provider);
        if (choice is null)
        {
            return (null, $"Unknown provider '{provider}'. Supported: {string.Join(", ", InvoiceExtractorFactory.Providers)}.");
        }

        if (!choice.Allowed)
        {
            return (null, $"{provider} is not allowed by the server configuration (Llm:AllowedProviders).");
        }

        if (choice.Unavailable is not null)
        {
            return (null, choice.Unavailable);
        }

        var found = choice.Models.FirstOrDefault(m => m.Id == model);
        return found is null ? (null, $"'{model}' is not one of the available {provider} models.") : (found, null);
    }

    private ProviderChoice Anthropic()
    {
        const string provider = AnthropicInvoiceExtractor.ProviderName;
        var options = factory.Options;

        if (!options.IsAllowed(provider))
        {
            return new ProviderChoice(provider, Allowed: false, Unavailable: null, []);
        }

        var models = options.Pricing
            .Where(p => p.Key.StartsWith("claude-", StringComparison.Ordinal))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new ModelChoice(p.Key, SupportsImages: true, Size: null, p.Value.InputPerMillion, p.Value.OutputPerMillion))
            .ToList();

        var unavailable = factory.HasAnthropicApiKey ? null : "No Anthropic API key is configured on the server (Llm:ApiKey or ANTHROPIC_API_KEY).";
        return new ProviderChoice(provider, Allowed: true, unavailable, models);
    }

    private async Task<ProviderChoice> OllamaAsync(CancellationToken cancellationToken)
    {
        const string provider = OllamaInvoiceExtractor.ProviderName;

        if (!factory.Options.IsAllowed(provider))
        {
            return new ProviderChoice(provider, Allowed: false, Unavailable: null, []);
        }

        IReadOnlyList<OllamaModel> installed;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(OllamaTimeout);
            try
            {
                installed = await factory.OllamaModels.ListAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return new ProviderChoice(provider, Allowed: true, $"Ollama is not reachable at {factory.OllamaModels.Endpoint}. Is it running?", []);
            }
        }

        var models = installed
            .Where(m => m.CanChat && !m.IsCloud)
            .Select(m => new ModelChoice(m.Name, m.SupportsImages, m.ParameterSize, InputPerMillion: null, OutputPerMillion: null))
            .ToList();

        var unavailable = models.Count == 0
            ? "No local models are installed in Ollama (cloud models are not offered). Install one with: ollama pull qwen3-vl:8b"
            : null;
        return new ProviderChoice(provider, Allowed: true, unavailable, models);
    }
}
