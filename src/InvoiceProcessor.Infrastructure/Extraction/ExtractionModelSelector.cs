using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>The model extraction uses right now: the latest admin choice, or the configured default.</summary>
public sealed record SelectedModel(string Provider, string Model, ExtractionModelChange? Change)
{
    /// <summary>True when no admin choice applies and <c>Llm:Provider</c> / <c>Llm:Model</c> are used.</summary>
    public bool IsDefault => Change is null;
}

/// <summary>
/// Picks the extractor for each extraction from the model selected in the app, so a change applies to the
/// next upload without a restart. Configuration always wins: a choice whose provider is no longer in
/// <c>Llm:AllowedProviders</c> is ignored in favour of the configured default.
/// </summary>
public sealed class ExtractionModelSelector(
    InvoiceProcessorDbContext db,
    InvoiceExtractorFactory factory,
    ILogger<ExtractionModelSelector> logger)
{
    public async Task<SelectedModel> GetSelectedAsync(CancellationToken cancellationToken)
    {
        var latest = await db.ExtractionModelChanges
            .AsNoTracking()
            .OrderByDescending(c => c.ChangedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var options = factory.Options;
        if (latest is null)
        {
            return new SelectedModel(options.Provider, options.Model, null);
        }

        if (!options.IsAllowed(latest.Provider) || !InvoiceExtractorFactory.Providers.Contains(latest.Provider))
        {
            logger.LogWarning(
                "The selected model {Provider}/{Model} is not allowed by Llm:AllowedProviders; using the configured {DefaultProvider}/{DefaultModel}",
                latest.Provider, latest.Model, options.Provider, options.Model);
            return new SelectedModel(options.Provider, options.Model, null);
        }

        return new SelectedModel(latest.Provider, latest.Model, latest);
    }

    public async Task<IInvoiceExtractor> GetExtractorAsync(CancellationToken cancellationToken)
    {
        var selected = await GetSelectedAsync(cancellationToken);
        return factory.Create(selected.Provider, selected.Model);
    }
}
