using System.Security.Claims;
using InvoiceProcessor.Api.Auth;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Infrastructure.Extraction;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Settings;

/// <summary>
/// Which model extracts invoices, picked by an admin among what the server configuration allows.
/// API keys and endpoints stay in configuration; only the choice is made here.
/// </summary>
public static class ExtractionSettingsEndpoints
{
    private const int HistoryLength = 10;

    public static IEndpointRouteBuilder MapExtractionSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/extraction").WithTags("Settings").RequireAuthorization(Policies.Admin);

        group.MapGet("/", Get);
        group.MapPut("/", Update);

        return app;
    }

    private static async Task<Ok<ExtractionSettingsResponse>> Get(
        InvoiceProcessorDbContext db,
        ExtractionModelSelector selector,
        ExtractionModelCatalog catalog,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await BuildResponseAsync(db, selector, catalog, cancellationToken));

    private static async Task<Results<Ok<ExtractionSettingsResponse>, ValidationProblem>> Update(
        UpdateExtractionSettingsRequest request,
        ClaimsPrincipal user,
        InvoiceProcessorDbContext db,
        ExtractionModelSelector selector,
        ExtractionModelCatalog catalog,
        TimeProvider timeProvider,
        ILogger<ExtractionModelChange> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Model))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["model"] = ["Choose a provider and a model."] });
        }

        // Checked against the live catalog: an allowed provider, reachable, and a model it really has.
        var (_, problem) = await catalog.FindAsync(request.Provider, request.Model, cancellationToken);
        if (problem is not null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["model"] = [problem] });
        }

        var current = await selector.GetSelectedAsync(cancellationToken);
        if (current.Provider != request.Provider || current.Model != request.Model)
        {
            var changedBy = user.Identity?.Name ?? throw new InvalidOperationException("Changing settings requires a signed-in user.");
            db.ExtractionModelChanges.Add(new ExtractionModelChange
            {
                Provider = request.Provider,
                Model = request.Model,
                ChangedBy = changedBy,
                ChangedAt = timeProvider.GetUtcNow(),
            });
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "{User} changed the extraction model from {OldProvider}/{OldModel} to {Provider}/{Model}",
                changedBy, current.Provider, current.Model, request.Provider, request.Model);
        }

        return TypedResults.Ok(await BuildResponseAsync(db, selector, catalog, cancellationToken));
    }

    private static async Task<ExtractionSettingsResponse> BuildResponseAsync(
        InvoiceProcessorDbContext db,
        ExtractionModelSelector selector,
        ExtractionModelCatalog catalog,
        CancellationToken cancellationToken)
    {
        var selected = await selector.GetSelectedAsync(cancellationToken);
        var providers = await catalog.GetAsync(cancellationToken);

        var history = await db.ExtractionModelChanges
            .AsNoTracking()
            .OrderByDescending(c => c.ChangedAt)
            .ThenByDescending(c => c.Id)
            .Take(HistoryLength)
            .Select(c => new ModelChangeResponse(c.Provider, c.Model, c.ChangedBy, c.ChangedAt))
            .ToListAsync(cancellationToken);

        return new ExtractionSettingsResponse(
            new SelectedModelResponse(selected.Provider, selected.Model, selected.IsDefault, selected.Change?.ChangedBy, selected.Change?.ChangedAt),
            [.. providers.Select(p => new ProviderResponse(p.Provider, p.Allowed, p.IsSelectable, p.Unavailable, p.Models))],
            history);
    }
}

public sealed record ExtractionSettingsResponse(SelectedModelResponse Selected, IReadOnlyList<ProviderResponse> Providers, IReadOnlyList<ModelChangeResponse> History);

/// <param name="IsDefault">True when the configured default (Llm:Provider, Llm:Model) applies.</param>
public sealed record SelectedModelResponse(string Provider, string Model, bool IsDefault, string? ChangedBy, DateTimeOffset? ChangedAt);

/// <param name="Unavailable">Why the provider cannot be picked right now, e.g. Ollama is not running.</param>
public sealed record ProviderResponse(string Provider, bool Allowed, bool IsSelectable, string? Unavailable, IReadOnlyList<ModelChoice> Models);

public sealed record ModelChangeResponse(string Provider, string Model, string ChangedBy, DateTimeOffset ChangedAt);

public sealed record UpdateExtractionSettingsRequest(string? Provider, string? Model);
