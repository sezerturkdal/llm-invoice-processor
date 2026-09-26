namespace InvoiceProcessor.Api.Invoices;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    public int MaxFileSizeMb { get; set; } = 10;

    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;
}
