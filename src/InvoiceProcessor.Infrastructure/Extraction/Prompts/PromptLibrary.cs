using System.Reflection;

namespace InvoiceProcessor.Infrastructure.Extraction.Prompts;

/// <summary>Prompts and schemas are embedded resources, so they are versioned and reviewed like code but kept out of it.</summary>
public static class PromptLibrary
{
    public static string InvoiceExtractionSystem { get; } = Load("invoice-extraction.system.md");

    public static string InvoiceExtractionSchema { get; } = Load("invoice-extraction.schema.json");

    private static string Load(string fileName)
    {
        var assembly = typeof(PromptLibrary).Assembly;
        var resourceName = $"{typeof(PromptLibrary).Namespace}.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded prompt '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
