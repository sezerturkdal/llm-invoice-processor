using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Extraction;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace InvoiceProcessor.Infrastructure.Tests.Extraction;

public class OllamaInvoiceExtractorTests
{
    private const string InvoiceJson = """
        {
          "supplier": "Acme GmbH",
          "invoiceNumber": "INV-001",
          "date": "2026-09-01",
          "currency": "EUR",
          "lines": [{ "description": "Widget", "qty": 2, "unitPrice": 50, "lineTotal": 100 }],
          "net": 100,
          "vat": 19,
          "total": 119
        }
        """;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly FakeHandler _handler = new();

    [Fact]
    public async Task Sends_schema_system_prompt_and_deterministic_options()
    {
        _handler.Respond(Chat(InvoiceJson));

        await CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        Assert.Equal("http://localhost:11434/api/chat", _handler.RequestUri);
        var body = _handler.RequestBody!;
        Assert.Equal("qwen3-vl:8b", body["model"]!.GetValue<string>());
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal("30m", body["keep_alive"]!.GetValue<string>());
        Assert.Null(body["think"]);

        Assert.NotNull(body["format"]!["properties"]!["invoiceNumber"]);

        var options = body["options"]!;
        Assert.Equal(0, options["temperature"]!.GetValue<double>());
        Assert.Equal(16384, options["num_ctx"]!.GetValue<int>());

        var system = body["messages"]![0]!;
        Assert.Equal("system", system["role"]!.GetValue<string>());
        Assert.Contains("do not calculate", system["content"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sends_images_as_base64()
    {
        _handler.Respond(Chat(InvoiceJson));

        await CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        var user = _handler.RequestBody!["messages"]![1]!;
        Assert.Equal("user", user["role"]!.GetValue<string>());
        Assert.Equal(Convert.ToBase64String(Png), user["images"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Sends_a_digital_pdf_as_text_without_images()
    {
        _handler.Respond(Chat(InvoiceJson));
        var pdf = TextPdf("Acme GmbH   Invoice INV-001   Widget 2 x 50.00 = 100.00   Total EUR 119.00");

        await CreateExtractor().ExtractAsync(new InvoiceDocument(pdf, InvoiceFileType.Pdf));

        var user = _handler.RequestBody!["messages"]![1]!;
        var content = user["content"]!.GetValue<string>();
        Assert.Contains("<document>", content);
        Assert.Contains("INV-001", content);
        Assert.DoesNotContain("{{text}}", content);
        Assert.Null(user["images"]);
    }

    [Fact]
    public async Task Parses_invoice_and_logs_tokens_at_no_cost()
    {
        _handler.Respond(Chat(InvoiceJson, model: "qwen3-vl:8b", promptTokens: 2100, outputTokens: 310));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        Assert.Equal("Acme GmbH", result.Invoice.Supplier);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Invoice.Date);
        Assert.Equal(119m, result.Invoice.Total);
        Assert.Single(result.Invoice.Lines);

        Assert.Equal("Ollama", result.Usage.Provider);
        Assert.Equal("qwen3-vl:8b", result.Usage.Model);
        Assert.Equal(2100, result.Usage.InputTokens);
        Assert.Equal(310, result.Usage.OutputTokens);
        Assert.Equal(0m, result.Usage.CostEstimate);
        Assert.Equal(InvoiceJson, result.Usage.RawResponse);
    }

    [Fact]
    public async Task Printed_date_and_currency_are_converted()
    {
        _handler.Respond(Chat(InvoiceJson.Replace("\"2026-09-01\"", "\"01.09.2026\"").Replace("\"EUR\"", "\"TL\"")));

        var result = await CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        Assert.Equal(new DateOnly(2026, 9, 1), result.Invoice.Date);
        Assert.Equal("TRY", result.Invoice.Currency);
        Assert.Contains("01.09.2026", result.Usage.RawResponse); // the log keeps what the model said
    }

    [Fact]
    public async Task Date_is_not_constrained_to_a_format_for_local_models()
    {
        _handler.Respond(Chat(InvoiceJson));

        await CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        var date = _handler.RequestBody!["format"]!["properties"]!["date"]!;
        Assert.Null(date["format"]);
        Assert.NotNull(date["type"]);
    }

    [Fact]
    public async Task Think_is_sent_when_configured()
    {
        _handler.Respond(Chat(InvoiceJson));
        var options = Options();
        options.Ollama.Think = false;

        await new OllamaInvoiceExtractor(Http(), options).ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png));

        Assert.False(_handler.RequestBody!["think"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Truncated_response_fails_with_usage()
    {
        _handler.Respond(Chat("{ \"supplier\": \"Ac", doneReason: "length", outputTokens: 16000));

        var ex = await Assert.ThrowsAsync<InvoiceExtractionException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png)));

        Assert.Contains("cut off", ex.Message);
        Assert.Equal(16000, ex.Usage?.OutputTokens);
    }

    [Fact]
    public async Task Invalid_json_fails_with_raw_response()
    {
        _handler.Respond(Chat("{ not json"));

        var ex = await Assert.ThrowsAsync<InvoiceExtractionException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png)));

        Assert.Equal("{ not json", ex.Usage?.RawResponse);
    }

    [Fact]
    public async Task Ollama_error_message_is_passed_on()
    {
        _handler.Respond("""{ "error": "model \"qwen3-vl:8b\" not found, try pulling it first" }""", HttpStatusCode.NotFound);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png)));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Contains("try pulling it first", ex.Message);
    }

    private OllamaInvoiceExtractor CreateExtractor() => new(Http(), Options());

    private HttpClient Http() => new(_handler) { BaseAddress = new Uri("http://localhost:11434/") };

    private static LlmOptions Options() => new() { Provider = "Ollama", Model = "qwen3-vl:8b" };

    private static byte[] TextPdf(string line)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(line, 11, new PdfPoint(50, 780), font);
        return builder.Build();
    }

    private static string Chat(
        string content,
        string model = "qwen3-vl:8b",
        string doneReason = "stop",
        int promptTokens = 100,
        int outputTokens = 50) =>
        JsonSerializer.Serialize(new
        {
            model,
            created_at = "2026-09-27T12:00:00Z",
            message = new { role = "assistant", content },
            done = true,
            done_reason = doneReason,
            prompt_eval_count = promptTokens,
            eval_count = outputTokens,
        });

    [Fact]
    public async Task Image_is_refused_up_front_by_a_text_only_model()
    {
        _handler.Respond(Chat(InvoiceJson));
        _handler.Capabilities = ["completion", "tools", "thinking"];

        var ex = await Assert.ThrowsAsync<InvoiceExtractionException>(
            () => CreateExtractor().ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png)));

        Assert.Contains("reads text only", ex.Message);
        Assert.Null(_handler.RequestUri); // no chat request was sent
    }

    [Fact]
    public async Task Text_only_model_still_reads_a_digital_pdf()
    {
        _handler.Respond(Chat(InvoiceJson));
        _handler.Capabilities = ["completion"];

        var result = await CreateExtractor().ExtractAsync(
            new InvoiceDocument(TextPdf("Acme GmbH   Invoice INV-001   Widget 2 x 50.00 = 100.00   Total EUR 119.00"), InvoiceFileType.Pdf));

        Assert.Equal("INV-001", result.Invoice.InvoiceNumber);
    }

    [Fact]
    public async Task Lists_installed_models_with_their_capabilities()
    {
        _handler.Tags = """
            { "models": [
              { "name": "qwen3-vl:8b", "size": 6100000000, "details": { "parameter_size": "8.8B" } },
              { "name": "gpt-oss:20b", "size": 13800000000, "details": { "parameter_size": "20.9B" } }
            ] }
            """;
        _handler.CapabilitiesByModel["gpt-oss:20b"] = ["completion", "tools", "thinking"];

        var models = await new OllamaModels(Http()).ListAsync(CancellationToken.None);

        Assert.Equal(["gpt-oss:20b", "qwen3-vl:8b"], models.Select(m => m.Name));
        Assert.False(models[0].SupportsImages);
        Assert.True(models[1].SupportsImages);
        Assert.Equal("8.8B", models[1].ParameterSize);
    }

    [Fact]
    public async Task Cloud_models_are_marked_as_such()
    {
        _handler.Tags = """
            { "models": [
              { "name": "qwen3-vl:8b", "size": 6100000000 },
              { "name": "qwen3-vl:235b-cloud", "size": 384, "remote_model": "qwen3-vl:235b", "remote_host": "https://ollama.com:443" },
              { "name": "glm-4.6:cloud", "size": 384 }
            ] }
            """;

        var models = await new OllamaModels(Http()).ListAsync(CancellationToken.None);

        Assert.Equal(["glm-4.6:cloud", "qwen3-vl:235b-cloud"], models.Where(m => m.IsCloud).Select(m => m.Name));
        Assert.False(models.Single(m => m.Name == "qwen3-vl:8b").IsCloud);
    }

    [Fact]
    public async Task Cloud_model_is_never_sent_a_document()
    {
        _handler.Respond(Chat(InvoiceJson));
        var options = Options();
        options.Model = "qwen3-vl:235b-cloud";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new OllamaInvoiceExtractor(Http(), options).ExtractAsync(new InvoiceDocument(Png, InvoiceFileType.Png)));

        Assert.Contains("Ollama Cloud", ex.Message);
        Assert.Null(_handler.RequestUri);
    }

    /// <summary>Answers /api/chat with the canned response, /api/show with capabilities, /api/tags with the model list.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private string _response = "";
        private HttpStatusCode _status = HttpStatusCode.OK;

        public JsonNode? RequestBody { get; private set; }
        public string? RequestUri { get; private set; }
        public string[] Capabilities { get; set; } = ["completion", "vision"];
        public Dictionary<string, string[]> CapabilitiesByModel { get; } = [];
        public string Tags { get; set; } = """{ "models": [] }""";

        public void Respond(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _response = json;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/tags":
                    return Json(Tags);

                case "/api/show":
                    var model = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!["model"]!.GetValue<string>();
                    var capabilities = CapabilitiesByModel.GetValueOrDefault(model, Capabilities);
                    return Json(JsonSerializer.Serialize(new { capabilities }));
            }

            RequestUri = request.RequestUri?.ToString();
            RequestBody = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return Json(_response, _status);
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
