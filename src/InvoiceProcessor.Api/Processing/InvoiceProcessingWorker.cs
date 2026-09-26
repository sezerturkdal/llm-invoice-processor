using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Processing;

/// <summary>Takes invoices off the queue one at a time and runs extraction in its own DI scope.</summary>
public sealed class InvoiceProcessingWorker(
    InvoiceProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<InvoiceProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        await foreach (var invoiceId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<InvoiceExtractionPipeline>();
                await pipeline.ProcessAsync(invoiceId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Storage or database trouble; the invoice stays in Processing and is retried on the next start.
                logger.LogError(ex, "Could not process invoice {InvoiceId}", invoiceId);
            }
        }
    }

    // Invoices uploaded before a restart, or whose processing was interrupted by one.
    private async Task RequeueUnfinishedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceProcessorDbContext>();

        var pending = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Processing)
            .OrderBy(i => i.CreatedAt)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pending)
        {
            queue.Enqueue(id);
        }

        if (pending.Count > 0)
        {
            logger.LogInformation("Re-queued {Count} invoices still in Processing", pending.Count);
        }
    }
}
