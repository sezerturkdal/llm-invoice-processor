namespace InvoiceProcessor.Api.Extractions;

/// <summary>Extraction usage over a period: overall and per model, so models can be compared on cost and speed.</summary>
/// <param name="Since">Start of the period; null means all time.</param>
public sealed record ExtractionStatsResponse(DateTimeOffset? Since, ExtractionStats Totals, IReadOnlyList<ModelExtractionStats> Models);

public sealed record ModelExtractionStats(string Provider, string Model, ExtractionStats Stats);

/// <param name="Extractions">All attempts, including failed ones and re-extractions.</param>
/// <param name="TotalCost">Estimated USD; failed attempts count when the provider reported usage.</param>
/// <param name="AverageCost">Per successful extraction, i.e. what one extracted invoice costs.</param>
/// <param name="AverageLatencyMs">Over successful attempts only; failures often end early and would skew it.</param>
public sealed record ExtractionStats(
    int Extractions,
    int Failures,
    long InputTokens,
    long OutputTokens,
    decimal TotalCost,
    decimal? AverageCost,
    double? AverageLatencyMs,
    long? MaxLatencyMs);
