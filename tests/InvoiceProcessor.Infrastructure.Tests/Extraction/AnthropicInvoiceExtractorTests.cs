using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Extraction;

namespace InvoiceProcessor.Infrastructure.Tests.Extraction;

public class AnthropicInvoiceExtractorTests
{
    private const string InvoiceJson = """
        {
          "supplier": "Acme GmbH",
          "invoiceNumber": "INV-001",
          "date": "2026-09-01",
          "currency": "EUR",
          "lines": [
            { "description": "Widget", "qty": 2, "unitPrice": 50, "lineTotal": 100 },
            { "description": "Discount", "qty": 1, "unitPrice": -10, "lineTotal": -10 }
          ],
          "net": 90,
          "vat": 17.1,
          "total": 107.1
        }
        """;

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake");

    private readonly FakeHandler _handler = new();

    [Fact]
    public async Task Sends_pdf_as_native_document_with_schema_and_fallbacks()
    {
        _handler.Respond(Message(InvoiceJson));

        await CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf));

        var body = _handler.RequestBody!;
        Assert.Equal("claude-opus-5", body["model"]!.GetValue<string>());
        Assert.Equal("default", body["fallbacks"]!.GetValue<string>());
        Assert.Contains("server-side-fallback-2026-07-01", _handler.BetaHeader);

        var format = body["output_config"]!["format"]!;
        Assert.Equal("json_schema", format["type"]!.GetValue<string>());
        Assert.NotNull(format["schema"]!["properties"]!["invoiceNumber"]);

        Assert.Contains("do not calculate", body["system"]!.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        var document = body["messages"]![0]!["content"]![0]!;
        Assert.Equal("document", document["type"]!.GetValue<string>());
        Assert.Equal("application/pdf", document["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal(Convert.ToBase64String(PdfBytes), document["source"]!["data"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sends_images_as_image_blocks()
    {
        _handler.Respond(Message(InvoiceJson));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        await CreateExtractor().ExtractAsync(new InvoiceDocument(png, InvoiceFileType.Png));

        var image = _handler.RequestBody!["messages"]![0]!["content"]![0]!;
        Assert.Equal("image", image["type"]!.GetValue<string>());
        Assert.Equal("image/png", image["source"]!["media_type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Parses_invoice_and_costs_the_call()
    {
        _handler.Respond(Message(InvoiceJson, inputTokens: 1500, outputTokens: 200));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf));

        Assert.Equal("Acme GmbH", result.Invoice.Supplier);
        Assert.Equal("INV-001", result.Invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Invoice.Date);
        Assert.Equal(107.1m, result.Invoice.Total);
        Assert.Equal(2, result.Invoice.Lines.Count);
        Assert.Equal(-10m, result.Invoice.Lines[1].LineTotal);

        Assert.Equal("Anthropic", result.Usage.Provider);
        Assert.Equal("claude-opus-5", result.Usage.Model);
        Assert.Equal(1500, result.Usage.InputTokens);
        Assert.Equal(200, result.Usage.OutputTokens);
        Assert.Equal(0.0125m, result.Usage.CostEstimate); // 1500 * $5/M + 200 * $25/M
        Assert.Equal(InvoiceJson, result.Usage.RawResponse);
    }

    [Fact]
    public async Task Nulls_for_missing_fields_are_kept()
    {
        _handler.Respond(Message("""
            { "supplier": null, "invoiceNumber": "7", "date": null, "currency": null, "lines": [], "net": null, "vat": null, "total": 12.5 }
            """));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf));

        Assert.Null(result.Invoice.Supplier);
        Assert.Null(result.Invoice.Date);
        Assert.Empty(result.Invoice.Lines);
        Assert.Equal(12.5m, result.Invoice.Total);
    }

    [Fact]
    public async Task Prices_the_model_that_answered_after_a_fallback()
    {
        _handler.Respond(Message(InvoiceJson, model: "claude-opus-4-8", inputTokens: 1000, outputTokens: 0));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf));

        Assert.Equal("claude-opus-4-8", result.Usage.Model);
        Assert.Equal(0.004m, result.Usage.CostEstimate);
    }

    [Fact]
    public async Task Unknown_model_has_no_cost_estimate()
    {
        _handler.Respond(Message(InvoiceJson, model: "claude-unlisted"));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf));

        Assert.Null(result.Usage.CostEstimate);
    }

    [Theory]
    [InlineData("refusal", "declined")]
    [InlineData("max_tokens", "cut off")]
    public async Task Unusable_stop_reasons_fail_with_usage(string stopReason, string expectedMessage)
    {
        _handler.Respond(Message("", stopReason: stopReason, outputTokens: 42));

        var ex = await Assert.ThrowsAsync<InvoiceExtractionException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf)));

        Assert.Contains(expectedMessage, ex.Message);
        Assert.Equal(42, ex.Usage?.OutputTokens);
    }

    [Fact]
    public async Task Invalid_json_fails_with_raw_response()
    {
        _handler.Respond(Message("{ not json"));

        var ex = await Assert.ThrowsAsync<InvoiceExtractionException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(PdfBytes, InvoiceFileType.Pdf)));

        Assert.Equal("{ not json", ex.Usage?.RawResponse);
    }

    private AnthropicInvoiceExtractor CreateExtractor()
    {
        var client = new AnthropicClient
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(_handler),
            MaxRetries = 0,
        };

        var options = new LlmOptions
        {
            Model = "claude-opus-5",
            Pricing =
            {
                ["claude-opus-5"] = new ModelPricing { InputPerMillion = 5m, OutputPerMillion = 25m },
                ["claude-opus-4-8"] = new ModelPricing { InputPerMillion = 4m, OutputPerMillion = 20m },
            },
        };

        return new AnthropicInvoiceExtractor(client, options);
    }

    private static string Message(
        string text,
        string model = "claude-opus-5",
        string stopReason = "end_turn",
        int inputTokens = 100,
        int outputTokens = 50) =>
        JsonSerializer.Serialize(new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model,
            content = new[] { new { type = "text", text } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = inputTokens, output_tokens = outputTokens },
        });

    private sealed class FakeHandler : HttpMessageHandler
    {
        private string _response = "";

        public JsonNode? RequestBody { get; private set; }
        public string BetaHeader { get; private set; } = "";

        public void Respond(string json) => _response = json;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            BetaHeader = request.Headers.TryGetValues("anthropic-beta", out var values) ? string.Join(",", values) : "";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
