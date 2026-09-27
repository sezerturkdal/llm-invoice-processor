using InvoiceProcessor.Core.Invoices;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Invoices;

public static class ExtractionLogQueries
{
    /// <summary>The invoice's most recent extraction attempt, successful or not.</summary>
    public static Task<ExtractionLog?> LatestForAsync(this DbSet<ExtractionLog> logs, Guid invoiceId, CancellationToken cancellationToken) =>
        logs.AsNoTracking()
            .Where(l => l.InvoiceId == invoiceId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
