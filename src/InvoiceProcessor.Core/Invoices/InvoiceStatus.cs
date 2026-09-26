namespace InvoiceProcessor.Core.Invoices;

public enum InvoiceStatus
{
    Processing,
    PendingReview,
    Approved,
    Rejected,
    Failed
}
