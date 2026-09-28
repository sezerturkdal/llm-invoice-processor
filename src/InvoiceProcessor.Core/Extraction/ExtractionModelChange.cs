namespace InvoiceProcessor.Core.Extraction;

/// <summary>
/// An admin picking the extraction model in the app. Rows are only added, never updated: the latest one is
/// the current choice, the rest are the history of who changed it and when. With no rows, the model
/// configured in Llm:Provider and Llm:Model is used.
/// </summary>
public class ExtractionModelChange
{
    public int Id { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
