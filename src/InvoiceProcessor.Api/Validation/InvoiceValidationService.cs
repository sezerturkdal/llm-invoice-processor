using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Core.Validation;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Validation;

/// <summary>Gathers what the validation rules need from the database and replaces the invoice's issues.</summary>
public sealed class InvoiceValidationService(InvoiceProcessorDbContext db, TimeProvider timeProvider)
{
    /// <summary>Validates the invoice as it is in memory; the caller saves the changes.</summary>
    public async Task ValidateAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        IReadOnlyList<DuplicateInvoice> duplicates = [];
        if (!string.IsNullOrWhiteSpace(invoice.Supplier) && !string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            // Rejected or failed uploads are not real duplicates. The column collation makes the match case-insensitive.
            duplicates = await db.Invoices
                .AsNoTracking()
                .Where(i => i.Id != invoice.Id
                    && i.Supplier == invoice.Supplier
                    && i.InvoiceNumber == invoice.InvoiceNumber
                    && i.Status != InvoiceStatus.Rejected
                    && i.Status != InvoiceStatus.Failed)
                .Select(i => new DuplicateInvoice(i.Id, i.Status, i.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var issues = InvoiceValidator.Validate(invoice, new ValidationContext(today, duplicates));

        invoice.ReplaceValidationIssues(issues);
    }
}
