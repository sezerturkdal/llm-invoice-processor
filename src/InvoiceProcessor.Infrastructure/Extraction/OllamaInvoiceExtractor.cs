using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Infrastructure.Extraction.Prompts;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>
/// Runs the extraction on a local model served by Ollama, so documents never leave the machine.
/// Local models have no native PDF input: a PDF goes in as its text layer, or as page images when it is
/// a scan (see <see cref="DocumentPreparation"/>). The answer is constrained to the same JSON schema
/// as the Anthropic extractor, through Ollama's <c>format</c> parameter.
/// </summary>
public sealed class OllamaInvoiceExtractor(HttpClient http, LlmOptions options) : IInvoiceExtractor
{
    public const string ProviderName = "Ollama";

    // The shared schema without "format": "date". Constrained to YYYY-MM-DD, a small model that copies "12.03.2026"
    // is forced to write it as "1203-03-20"; left free, it copies the printed date and PrintedValues converts it.
    private static readonly JsonElement Schema = LocalModelSchema();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Provider => ProviderName;

    public string Model => options.Model;

    public async Task<ExtractionResult> ExtractAsync(InvoiceDocument document, CancellationToken cancellationToken = default)
    {
        // Also covers a cloud model set directly in configuration, not only one picked in the app.
        if (OllamaModel.IsCloudName(options.Model))
        {
            throw new InvalidOperationException(
                $"{options.Model} is an Ollama Cloud model, which runs on Ollama's servers. Only models that run locally are used.");
        }

        var prepared = DocumentPreparation.Prepare(document, options.Ollama.Documents);

        // A text-only model given an image does not fail: it answers without seeing the page, with made-up values.
        if (!prepared.IsText
            && await new OllamaModels(http).GetCapabilitiesAsync(options.Model, cancellationToken) is { } capabilities
            && !capabilities.Contains("vision"))
        {
            throw new InvoiceExtractionException(
                $"{options.Model} reads text only, and this document is an image or a scanned PDF without a text layer. " +
                "Choose a model that supports images, such as qwen3-vl:8b.");
        }

        var request = new ChatRequest(
            Model: options.Model,
            Messages:
            [
                new ChatMessage("system", PromptLibrary.InvoiceExtractionSystem),
                prepared.Text is { } text
                    ? new ChatMessage("user", PromptLibrary.InvoiceTextInput.Replace("{{text}}", text))
                    : new ChatMessage("user", "Extract the data from this invoice.", prepared.Images.Select(Convert.ToBase64String).ToList()),
            ],
            Format: Schema,
            Options: new ModelOptions(Temperature: 0, NumCtx: options.Ollama.ContextLength, NumPredict: options.MaxTokens, NumThread: options.Ollama.Threads),
            KeepAlive: options.Ollama.KeepAlive,
            Think: options.Ollama.Think);

        var stopwatch = Stopwatch.StartNew();
        ChatResponse response;
        try
        {
            using var httpResponse = await http.PostAsJsonAsync("api/chat", request, RequestJsonOptions, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(httpResponse, cancellationToken);
                throw new HttpRequestException($"Ollama returned {(int)httpResponse.StatusCode}: {error}", null, httpResponse.StatusCode);
            }

            response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>(RequestJsonOptions, cancellationToken)
                ?? throw new InvoiceExtractionException("Ollama returned an empty response.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new HttpRequestException($"Could not reach Ollama at {http.BaseAddress}. Is it running? {ex.Message}", ex);
        }
        stopwatch.Stop();

        var rawResponse = response.Message?.Content ?? "";
        // Running locally costs nothing per call, so the estimate is zero rather than unknown.
        var usage = new ExtractionUsage(
            ProviderName, response.Model ?? options.Model, response.PromptEvalCount, response.EvalCount, stopwatch.ElapsedMilliseconds, 0m, rawResponse);

        if (response.DoneReason == "length")
        {
            throw new InvoiceExtractionException($"The response was cut off at {options.MaxTokens} tokens or the {options.Ollama.ContextLength}-token context.", usage);
        }

        ExtractedInvoice? invoice;
        try
        {
            invoice = JsonNode.Parse(rawResponse) is JsonObject json ? Normalize(json).Deserialize<ExtractedInvoice>(JsonOptions) : null;
        }
        catch (JsonException ex)
        {
            throw new InvoiceExtractionException($"The response is not valid invoice JSON: {ex.Message}", usage, ex);
        }

        if (invoice is null)
        {
            throw new InvoiceExtractionException("The response was empty.", usage);
        }

        return new ExtractionResult(invoice with { Lines = invoice.Lines ?? [] }, usage);
    }

    /// <summary>The date and currency as printed, converted to YYYY-MM-DD and an ISO code (null when unreadable).</summary>
    private static JsonObject Normalize(JsonObject json)
    {
        var currency = PrintedValues.NormalizeCurrency(StringValue(json["currency"]));
        json["currency"] = currency;
        json["date"] = PrintedValues.ParseDate(StringValue(json["date"]), currency)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return json;
    }

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static JsonElement LocalModelSchema()
    {
        var schema = JsonNode.Parse(PromptLibrary.InvoiceExtractionSchema)!;
        schema["properties"]!["date"]!.AsObject().Remove("format");
        return JsonSerializer.SerializeToElement(schema);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return body.Length > 500 ? body[..500] : body;
    }

    private sealed record ChatRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        JsonElement Format,
        ModelOptions Options,
        string KeepAlive,
        bool? Think,
        bool Stream = false);

    private sealed record ChatMessage(string Role, string Content, IReadOnlyList<string>? Images = null);

    private sealed record ModelOptions(double Temperature, int NumCtx, int NumPredict, int? NumThread);

    private sealed record ChatResponse(string? Model, ChatMessage? Message, string? DoneReason, int? PromptEvalCount, int? EvalCount);
}
