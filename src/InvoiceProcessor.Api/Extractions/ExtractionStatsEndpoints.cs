using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Extractions;

public static class ExtractionStatsEndpoints
{
    private const int MaxDays = 365;

    public static IEndpointRouteBuilder MapExtractionStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/extractions/stats", GetStats).WithTags("Extractions");
        return app;
    }

    // Token, cost and latency totals from the extraction log. Omit days for all time.
    private static async Task<Ok<ExtractionStatsResponse>> GetStats(
        InvoiceProcessorDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        int? days = null)
    {
        DateTimeOffset? since = days is null ? null : timeProvider.GetUtcNow().AddDays(-Math.Clamp(days.Value, 1, MaxDays));

        var logs = db.ExtractionLogs.AsNoTracking();
        if (since is not null)
        {
            logs = logs.Where(l => l.CreatedAt >= since);
        }

        // Sums rather than averages come back from SQL, so the overall totals can be combined exactly in memory.
        var rows = await logs
            .GroupBy(l => new { l.Provider, l.Model })
            .Select(g => new Sums(
                g.Key.Provider,
                g.Key.Model,
                g.Count(),
                g.Count(l => l.Error != null),
                g.Sum(l => (long)(l.InputTokens ?? 0)),
                g.Sum(l => (long)(l.OutputTokens ?? 0)),
                g.Sum(l => l.CostEstimate ?? 0),
                g.Count(l => l.Error == null && l.CostEstimate != null),
                g.Sum(l => l.Error == null ? l.CostEstimate ?? 0 : 0),
                g.Sum(l => l.Error == null ? l.LatencyMs : 0),
                g.Max(l => l.Error == null ? (long?)l.LatencyMs : null)))
            .ToListAsync(cancellationToken);

        var models = rows
            .OrderByDescending(r => r.Extractions)
            .ThenBy(r => r.Model)
            .Select(r => new ModelExtractionStats(r.Provider, r.Model, r.ToStats()))
            .ToList();

        var totals = rows.Aggregate(Sums.Empty, (total, r) => total + r).ToStats();

        return TypedResults.Ok(new ExtractionStatsResponse(since, totals, models));
    }

    private sealed record Sums(
        string Provider,
        string Model,
        int Extractions,
        int Failures,
        long InputTokens,
        long OutputTokens,
        decimal TotalCost,
        int PricedSuccesses,
        decimal SuccessCost,
        long SuccessLatencyMs,
        long? MaxLatencyMs)
    {
        public static readonly Sums Empty = new("", "", 0, 0, 0, 0, 0, 0, 0, 0, null);

        private int Successes => Extractions - Failures;

        public static Sums operator +(Sums a, Sums b) => new(
            "",
            "",
            a.Extractions + b.Extractions,
            a.Failures + b.Failures,
            a.InputTokens + b.InputTokens,
            a.OutputTokens + b.OutputTokens,
            a.TotalCost + b.TotalCost,
            a.PricedSuccesses + b.PricedSuccesses,
            a.SuccessCost + b.SuccessCost,
            a.SuccessLatencyMs + b.SuccessLatencyMs,
            a.MaxLatencyMs is null ? b.MaxLatencyMs : b.MaxLatencyMs is null ? a.MaxLatencyMs : Math.Max(a.MaxLatencyMs.Value, b.MaxLatencyMs.Value));

        public ExtractionStats ToStats() => new(
            Extractions,
            Failures,
            InputTokens,
            OutputTokens,
            TotalCost,
            PricedSuccesses == 0 ? null : Math.Round(SuccessCost / PricedSuccesses, 6),
            Successes == 0 ? null : Math.Round((double)SuccessLatencyMs / Successes),
            MaxLatencyMs);
    }
}
