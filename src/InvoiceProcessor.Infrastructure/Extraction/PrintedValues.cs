using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceProcessor.Infrastructure.Extraction;

/// <summary>
/// Turns dates and currencies as printed on an invoice into the schema's formats. Small local models copy
/// "12.03.2026" or "4 February 2026" instead of writing 2026-03-12, so they transcribe and this converts,
/// deterministically. Anything it cannot read unambiguously becomes null, which the reviewer is asked to fill.
/// </summary>
public static partial class PrintedValues
{
    private static readonly CultureInfo[] MonthNameCultures =
        [.. new[] { "en-GB", "en-US", "de-DE", "fr-FR", "tr-TR", "es-ES", "it-IT", "nl-NL" }.Select(CultureInfo.GetCultureInfo)];

    private static readonly string[] MonthNameFormats =
        ["d MMMM yyyy", "d MMM yyyy", "d. MMMM yyyy", "d. MMM yyyy", "MMMM d, yyyy", "MMM d, yyyy", "MMMM d yyyy", "MMM d yyyy", "d MMM. yyyy"];

    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["€"] = "EUR",
        ["£"] = "GBP",
        ["US$"] = "USD",
        ["TL"] = "TRY",
        ["₺"] = "TRY",
        ["CHF"] = "CHF",
        ["Fr."] = "CHF",
        ["¥"] = "JPY",
        ["zł"] = "PLN",
        ["kr"] = "SEK",
    };

    /// <param name="currency">
    /// The invoice's currency, for numeric dates where either part could be the month (03/05/2026): month
    /// first for USD, day first otherwise, as the prompt asks the model to follow the supplier's country.
    /// </param>
    public static DateOnly? ParseDate(string? printed, string? currency)
    {
        if (string.IsNullOrWhiteSpace(printed))
        {
            return null;
        }

        var text = printed.Trim();

        var isoLike = IsoDate().Match(text);
        if (isoLike.Success)
        {
            return Create(int.Parse(isoLike.Groups[1].Value), int.Parse(isoLike.Groups[2].Value), int.Parse(isoLike.Groups[3].Value));
        }

        var numeric = NumericDate().Match(text);
        if (numeric.Success)
        {
            var first = int.Parse(numeric.Groups[1].Value);
            var separator = numeric.Groups[2].Value;
            var second = int.Parse(numeric.Groups[3].Value);
            var year = int.Parse(numeric.Groups[4].Value);
            if (year < 100)
            {
                year += 2000;
            }

            var monthFirst = separator != "." && (first > 12 ? false : second > 12 || string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase));
            return monthFirst ? Create(year, first, second) : Create(year, second, first);
        }

        foreach (var culture in MonthNameCultures)
        {
            if (DateTime.TryParseExact(text, MonthNameFormats, culture, DateTimeStyles.AllowWhiteSpaces, out var date))
            {
                return DateOnly.FromDateTime(date);
            }
        }

        return null;
    }

    /// <summary>ISO 4217 code for a code or symbol ("EUR", "€", "TL"); null when it is not recognisable as one.</summary>
    public static string? NormalizeCurrency(string? printed)
    {
        if (string.IsNullOrWhiteSpace(printed))
        {
            return null;
        }

        var text = printed.Trim();
        if (CurrencySymbols.TryGetValue(text, out var code))
        {
            return code;
        }

        return IsoCurrency().IsMatch(text) ? text.ToUpperInvariant() : null;
    }

    private static DateOnly? Create(int year, int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year is >= 1 and <= 9999 ? year : 2000, month) && year is >= 1900 and <= 2999
            ? new DateOnly(year, month, day)
            : null;

    [GeneratedRegex(@"^(\d{4})[-./](\d{1,2})[-./](\d{1,2})$")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^(\d{1,2})([-./])(\d{1,2})\2(\d{4}|\d{2})$")]
    private static partial Regex NumericDate();

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex IsoCurrency();
}
