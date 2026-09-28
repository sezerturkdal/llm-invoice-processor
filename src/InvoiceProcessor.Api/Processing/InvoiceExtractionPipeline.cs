using InvoiceProcessor.Api.Validation;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Infrastructure.Extraction;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Processing;

/// <summary>Runs extraction for one invoice and records the outcome, success or failure, in the extraction log.</summary>
public sealed class InvoiceExtractionPipeline(
    InvoiceProcessorDbContext db,
    IFileStorage storage,
    ExtractionModelSelector modelSelector,
    InvoiceValidationService validation,
    TimeProvider timeProvider,
    ILogger<InvoiceExtractionPipeline> logger)
{
    private const int MaxErrorLength = 2000;

    public async Task ProcessAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.ValidationIssues)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        // Already handled, e.g. queued both by the upload and by the startup recovery.
        if (invoice is null || invoice.Status != InvoiceStatus.Processing)
        {
            return;
        }

        var fileType = InvoiceFileType.FromPath(invoice.FilePath)
            ?? throw new InvalidOperationException($"Invoice {invoice.Id} has an unsupported stored file '{invoice.FilePath}'.");

        byte[] content;
        await using (var stream = await storage.OpenReadAsync(invoice.FilePath, cancellationToken))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        // Chosen per invoice, so a model picked in Settings applies from the next extraction on.
        var extractor = await modelSelector.GetExtractorAsync(cancellationToken);

        try
        {
            var result = await extractor.ExtractAsync(new InvoiceDocument(content, fileType), cancellationToken);

            invoice.ApplyExtraction(result.Invoice, result.Usage.Model);
            await validation.ValidateAsync(invoice, cancellationToken);
            AddLog(invoice, extractor, result.Usage, error: null);

            logger.LogInformation(
                "Extracted invoice {InvoiceId} with {Model}: {InputTokens} in / {OutputTokens} out tokens, {LatencyMs} ms, ${Cost}, {IssueCount} validation issues",
                invoice.Id, result.Usage.Model, result.Usage.InputTokens, result.Usage.OutputTokens, result.Usage.LatencyMs, result.Usage.CostEstimate,
                invoice.ValidationIssues.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Leave the invoice in Failed rather than retrying forever; it can be re-queued from the API.
            logger.LogWarning(ex, "Extraction failed for invoice {InvoiceId}", invoice.Id);

            invoice.Status = InvoiceStatus.Failed;
            var usage = (ex as InvoiceExtractionException)?.Usage;
            AddLog(invoice, extractor, usage, Truncate(ex.Message, MaxErrorLength));
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private void AddLog(Invoice invoice, IInvoiceExtractor extractor, ExtractionUsage? usage, string? error)
    {
        db.ExtractionLogs.Add(new ExtractionLog
        {
            InvoiceId = invoice.Id,
            Provider = usage?.Provider ?? extractor.Provider,
            Model = usage?.Model ?? extractor.Model,
            InputTokens = usage?.InputTokens,
            OutputTokens = usage?.OutputTokens,
            LatencyMs = usage?.LatencyMs ?? 0,
            CostEstimate = usage?.CostEstimate,
            RawResponse = usage?.RawResponse,
            Error = error,
            CreatedAt = timeProvider.GetUtcNow(),
        });
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
