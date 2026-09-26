namespace InvoiceProcessor.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Folder for uploaded files. Relative paths resolve against the app's content root.</summary>
    public string RootPath { get; set; } = "uploads";
}
