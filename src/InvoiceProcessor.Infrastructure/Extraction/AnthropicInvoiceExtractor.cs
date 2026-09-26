using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Extraction.Prompts;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>
/// Sends the document to Claude as-is (native PDF or image input, no OCR step) and constrains the
/// answer to the extraction JSON schema with structured outputs.
/// </summary>
public sealed class AnthropicInvoiceExtractor(AnthropicClient client, LlmOptions options) : IInvoiceExtractor
{
    public const string ProviderName = "Anthropic";

    // A policy refusal is re-served by a fallback model inside the same call instead of failing the extraction.
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    private static readonly Dictionary<string, JsonElement> Schema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(PromptLibrary.InvoiceExtractionSchema)!;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Provider => ProviderName;

    public string Model => options.Model;

    public async Task<ExtractionResult> ExtractAsync(InvoiceDocument document, CancellationToken cancellationToken = default)
    {
        var format = new BetaJsonOutputFormat { Schema = Schema };
        var outputConfig = string.IsNullOrWhiteSpace(options.Effort)
            ? new BetaOutputConfig { Format = format }
            : new BetaOutputConfig { Format = format, Effort = options.Effort };

        var parameters = new MessageCreateParams
        {
            Model = options.Model,
            MaxTokens = options.MaxTokens,
            Betas = [FallbackBeta],
            Fallbacks = new Default(),
            System = PromptLibrary.InvoiceExtractionSystem,
            OutputConfig = outputConfig,
            Messages =
            [
                new BetaMessageParam
                {
                    Role = Role.User,
                    Content = new List<BetaContentBlockParam>
                    {
                        ToContentBlock(document),
                        new BetaTextBlockParam { Text = "Extract the data from this invoice." },
                    },
                },
            ],
        };

        var stopwatch = Stopwatch.StartNew();
        BetaMessage response = await client.Beta.Messages.Create(parameters, cancellationToken);
        stopwatch.Stop();

        var text = new StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out BetaTextBlock? textBlock))
            {
                text.Append(textBlock.Text);
            }
        }

        var rawResponse = text.ToString();
        var usage = ToUsage(response, stopwatch.ElapsedMilliseconds, rawResponse);

        if (response.StopReason == "refusal")
        {
            var category = response.StopDetails?.Category?.Raw() ?? "unspecified";
            throw new InvoiceExtractionException($"The model declined to process this document (category: {category}).", usage);
        }

        if (response.StopReason == "max_tokens")
        {
            throw new InvoiceExtractionException($"The response was cut off at {options.MaxTokens} tokens.", usage);
        }

        ExtractedInvoice? invoice;
        try
        {
            invoice = JsonSerializer.Deserialize<ExtractedInvoice>(rawResponse, JsonOptions);
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

    private static BetaContentBlockParam ToContentBlock(InvoiceDocument document)
    {
        var data = Convert.ToBase64String(document.Content);

        if (document.FileType == InvoiceFileType.Pdf)
        {
            return new BetaRequestDocumentBlock { Source = new BetaBase64PdfSource { Data = data } };
        }

        return new BetaImageBlockParam
        {
            Source = new BetaBase64ImageSource { Data = data, MediaType = document.FileType.ContentType },
        };
    }

    private ExtractionUsage ToUsage(BetaMessage response, long latencyMs, string rawResponse)
    {
        // With a fallback the answer can come from a different model than requested; log and price the one that answered.
        var model = response.Model.Raw();
        var inputTokens = response.Usage.InputTokens;
        var outputTokens = response.Usage.OutputTokens;

        decimal? cost = options.Pricing.TryGetValue(model, out var pricing)
            ? pricing.Estimate(inputTokens, outputTokens)
            : null;

        return new ExtractionUsage(ProviderName, model, (int)inputTokens, (int)outputTokens, latencyMs, cost, rawResponse);
    }
}
