using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>A model installed in Ollama, and what it can do ("completion", "vision", "thinking", ...).</summary>
public sealed record OllamaModel(string Name, long SizeBytes, string? ParameterSize, IReadOnlyList<string> Capabilities, bool IsCloud)
{
    public bool SupportsImages => Capabilities.Contains("vision");

    /// <summary>Embedding-only models cannot answer a chat request.</summary>
    public bool CanChat => Capabilities.Count == 0 || Capabilities.Contains("completion");

    /// <summary>
    /// Ollama Cloud models (qwen3-vl:235b-cloud, glm-4.6:cloud) show up among the installed models once pulled,
    /// but run on Ollama's servers: the document would leave the machine, which is what a local model is for.
    /// </summary>
    public static bool IsCloudName(string name) =>
        name.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase) || name.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Reads which models the local Ollama has installed (<c>/api/tags</c>) and their capabilities (<c>/api/show</c>).</summary>
public sealed class OllamaModels(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public Uri? Endpoint => http.BaseAddress;

    public async Task<IReadOnlyList<OllamaModel>> ListAsync(CancellationToken cancellationToken)
    {
        var tags = await http.GetFromJsonAsync<TagsResponse>("api/tags", JsonOptions, cancellationToken);
        var models = new List<OllamaModel>();

        foreach (var tag in tags?.Models ?? [])
        {
            var capabilities = await GetCapabilitiesAsync(tag.Name, cancellationToken) ?? [];
            var isCloud = tag.RemoteHost is not null || OllamaModel.IsCloudName(tag.Name);
            models.Add(new OllamaModel(tag.Name, tag.Size, tag.Details?.ParameterSize, capabilities, isCloud));
        }

        return [.. models.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Null when the model is not installed or Ollama is too old to report capabilities.</summary>
    public async Task<IReadOnlyList<string>?> GetCapabilitiesAsync(string model, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/show", new { model }, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var show = await response.Content.ReadFromJsonAsync<ShowResponse>(JsonOptions, cancellationToken);
        return show?.Capabilities;
    }

    private sealed record TagsResponse(List<Tag>? Models);

    /// <param name="RemoteHost">Set for Ollama Cloud models, which run remotely.</param>
    private sealed record Tag(string Name, long Size, TagDetails? Details, string? RemoteHost);

    private sealed record TagDetails(string? ParameterSize);

    private sealed record ShowResponse(List<string>? Capabilities);
}
