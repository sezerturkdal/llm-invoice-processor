namespace InvoiceProcessor.Core.Invoices;

public class ValidationIssue
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    /// <summary>Name of the flagged field, e.g. "net" or "lines[2].lineTotal".</summary>
    public required string Field { get; set; }

    /// <summary>Identifier of the rule that failed, e.g. "LinesSumToNet".</summary>
    public required string Rule { get; set; }

    public required string Message { get; set; }
}
