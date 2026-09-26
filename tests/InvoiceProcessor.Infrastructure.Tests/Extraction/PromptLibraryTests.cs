using System.Text.Json;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Infrastructure.Extraction.Prompts;

namespace InvoiceProcessor.Infrastructure.Tests.Extraction;

public class PromptLibraryTests
{
    [Fact]
    public void System_prompt_is_embedded()
    {
        Assert.Contains("invoice", PromptLibrary.InvoiceExtractionSystem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schema_requires_every_field_of_ExtractedInvoice()
    {
        using var schema = JsonDocument.Parse(PromptLibrary.InvoiceExtractionSchema);

        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
        var recordFields = typeof(ExtractedInvoice).GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet();

        Assert.Equal(recordFields, required);
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }
}
