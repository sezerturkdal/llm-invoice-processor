using InvoiceProcessor.Infrastructure.Extraction;

namespace InvoiceProcessor.Infrastructure.Tests.Extraction;

public class PrintedValuesTests
{
    [Theory]
    [InlineData("2026-06-30", "GBP", "2026-06-30")]
    [InlineData("2026/06/30", "GBP", "2026-06-30")]
    [InlineData("12.03.2026", "EUR", "2026-03-12")] // dots: always day first, even for USD
    [InlineData("3.4.2026", "USD", "2026-04-03")]
    [InlineData("14/05/2026", "EUR", "2026-05-14")] // 14 can only be the day
    [InlineData("08-15-2026", "USD", "2026-08-15")] // 15 can only be the day
    [InlineData("03-05-2026", "USD", "2026-03-05")] // either way round: month first for USD
    [InlineData("03/05/2026", "GBP", "2026-05-03")] // day first otherwise
    [InlineData("31/07/26", "GBP", "2026-07-31")]
    [InlineData("4 February 2026", "GBP", "2026-02-04")]
    [InlineData("02 Jun 2026", "EUR", "2026-06-02")]
    [InlineData("March 5, 2026", "USD", "2026-03-05")]
    [InlineData("29. Mai 2026", "EUR", "2026-05-29")]
    [InlineData("21 Nisan 2026", "TRY", "2026-04-21")]
    [InlineData("12 mars 2026", "EUR", "2026-03-12")]
    public void Reads_printed_dates(string printed, string currency, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), PrintedValues.ParseDate(printed, currency));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("31/02/2026")] // no such day
    [InlineData("13/13/2026")]
    [InlineData("1203-03-20")] // what a constrained small model makes of 12.03.2026
    public void Unreadable_dates_become_null(string? printed)
    {
        Assert.Null(PrintedValues.ParseDate(printed, "EUR"));
    }

    [Theory]
    [InlineData("EUR", "EUR")]
    [InlineData("eur", "EUR")]
    [InlineData("€", "EUR")]
    [InlineData("£", "GBP")]
    [InlineData("TL", "TRY")]
    [InlineData("₺", "TRY")]
    [InlineData("Euro", null)]
    [InlineData("$", null)] // dollar of which country?
    [InlineData(null, null)]
    public void Normalizes_currencies(string? printed, string? expected)
    {
        Assert.Equal(expected, PrintedValues.NormalizeCurrency(printed));
    }
}
