using InvoiceProcessor.Core.Validation;

namespace InvoiceProcessor.Evals.Samples;

/// <summary>
/// The synthetic eval set. All companies, people and numbers are made up. Each sample targets
/// something real invoices do that trips extraction up; see <see cref="SampleInvoice.Purpose"/>.
/// </summary>
public static class SampleCatalog
{
    // Declared before All: static initializers run in textual order.
    private static readonly Money Euro = new("€", SymbolAfter: true);
    private static readonly Money EuroBefore = new("€", SymbolAfter: false);
    private static readonly Money Pound = new("£", SymbolAfter: false);
    private static readonly Money Dollar = new("$", SymbolAfter: false);
    private static readonly Money Lira = new("TL", SymbolAfter: true);

    public static IReadOnlyList<SampleInvoice> All { get; } =
    [
        GermanOfficeSupplies(),
        UkPrintShop(),
        UsSoftwareNoTax(),
        TurkishOfficeSupplies(),
        GrossLineTotals(),
        DiscountLine(),
        TwoVatRates(),
        MultiPageParts(),
        TotalMismatch(),
        LinesDoNotAddUp(),
        FrenchPngScan(),
        ManyReferenceNumbers(),
    ];

    private static SampleInvoice GermanOfficeSupplies()
    {
        var p = Pricing.Net(
            ("Kopierpapier A4, 80 g/m², 500 Blatt", 10, 4.95m, 0.19m, "KP-A4-80"),
            ("Toner HP 26A schwarz", 2, 89.90m, 0.19m, "TN-HP26A"),
            ("Ordner breit, blau", 25, 2.49m, 0.19m, "OR-80-BL"),
            ("Lieferpauschale", 1, 6.90m, 0.19m, null));

        return new SampleInvoice
        {
            Id = "synthetic-01-de-office-supplies",
            Purpose = "German labels, decimal comma, dd.MM.yyyy, symbol after the amount",
            Layout = SampleLayout.Classic,
            Labels = Labels.German,
            Culture = "de-DE",
            DateFormat = "dd.MM.yyyy",
            Money = Euro,
            Supplier = new Party("Nordlicht Bürobedarf GmbH", ["Speicherstraße 14", "20457 Hamburg", "Deutschland"], "DE318774501"),
            Customer = new Party("Keller & Söhne Architekten", ["Lindenallee 3", "22301 Hamburg"]),
            InvoiceNumber = "RE-2026-00418",
            Date = new DateOnly(2026, 3, 12),
            DueDate = new DateOnly(2026, 4, 11),
            References = [("Kundennummer", "K-10293")],
            Currency = "EUR",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
            Note = "Zahlbar innerhalb von 30 Tagen ohne Abzug.",
        };
    }

    private static SampleInvoice UkPrintShop()
    {
        var p = Pricing.Net(
            ("Business cards, 400gsm matt, 500 pcs", 3, 38.00m, 0.20m, null),
            ("A1 foam board poster", 4, 22.50m, 0.20m, null),
            ("Design proof revision (hours)", 1.5m, 45.00m, 0.20m, null));

        return new SampleInvoice
        {
            Id = "synthetic-02-uk-print-shop",
            Purpose = "Long-form date (4 February 2026), fractional quantity, purchase order number",
            Layout = SampleLayout.Modern,
            Labels = Labels.English,
            Culture = "en-GB",
            DateFormat = "d MMMM yyyy",
            Money = Pound,
            Supplier = new Party("Harbourside Print Co. Ltd", ["Unit 4, Wapping Wharf", "Bristol BS1 6WP", "United Kingdom"], "GB 402 7718 26"),
            Customer = new Party("Juniper Events Ltd", ["18 Queen Square", "Bath BA1 2HN"]),
            InvoiceNumber = "HPC-7731",
            Date = new DateOnly(2026, 2, 4),
            DueDate = new DateOnly(2026, 3, 6),
            References = [("Your PO", "PO-55120")],
            Currency = "GBP",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice UsSoftwareNoTax()
    {
        var p = Pricing.Net(
            ("Annual license: Analytics Pro (per seat)", 5, 480.00m, 0m, "AP-ANNUAL"),
            ("Onboarding workshop (remote, half day)", 1, 750.00m, 0m, "SVC-ONB"));

        return new SampleInvoice
        {
            Id = "synthetic-03-us-software-no-tax",
            Purpose = "US MM/dd/yyyy date that is ambiguous (03/05), zero tax",
            Layout = SampleLayout.Minimal,
            Labels = Labels.UsEnglish,
            Culture = "en-US",
            DateFormat = "MM/dd/yyyy",
            Money = Dollar,
            Supplier = new Party("Pinecrest Software LLC", ["2210 NW Raleigh St", "Portland, OR 97210", "USA"], "93-4417205"),
            Customer = new Party("Blue Mesa Analytics Inc.", ["500 Grand Ave, Suite 12", "Denver, CO 80203"]),
            InvoiceNumber = "INV-10077",
            Date = new DateOnly(2026, 3, 5),
            DueDate = new DateOnly(2026, 4, 4),
            Currency = "USD",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
            Note = "No sales tax charged: software delivered electronically to an out-of-state customer.",
        };
    }

    private static SampleInvoice TurkishOfficeSupplies()
    {
        var p = Pricing.Net(
            ("A4 Fotokopi Kağıdı (5'li koli)", 12, 1149.00m, 0.20m, null),
            ("Lazer Yazıcı Toneri", 3, 2450.00m, 0.20m, null),
            ("Masa Düzenleyici Set", 5, 389.90m, 0.20m, null));

        return new SampleInvoice
        {
            Id = "synthetic-04-tr-office-supplies",
            Purpose = "Turkish labels, thousands dot and decimal comma, currency written as TL (ISO code TRY)",
            Layout = SampleLayout.Classic,
            Labels = Labels.Turkish,
            Culture = "tr-TR",
            DateFormat = "dd.MM.yyyy",
            Money = Lira,
            Supplier = new Party("Anadolu Ofis Malzemeleri A.Ş.", ["Merkez Mah. Atatürk Cad. No: 112", "34384 Şişli / İstanbul"], "0680412297"),
            Customer = new Party("Ege Lojistik Ltd. Şti.", ["Kazımdirik Mah. 372. Sok. No: 8", "35100 Bornova / İzmir"]),
            InvoiceNumber = "ANO2026000153",
            Date = new DateOnly(2026, 4, 21),
            DueDate = new DateOnly(2026, 5, 21),
            References = [("Sipariş No", "SIP-8841")],
            Currency = "TRY",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice GrossLineTotals()
    {
        var p = Pricing.Gross(
            ("USB-C docking station, 11-in-1", 2, 149.00m, 0.23m, "CE-DK11"),
            ("27\" QHD monitor, height adjustable", 1, 279.00m, 0.23m, "CE-MN27"),
            ("HDMI 2.1 cable, 2 m", 3, 12.00m, 0.23m, "CE-HD2"));

        return new SampleInvoice
        {
            Id = "synthetic-05-gross-line-totals",
            Purpose = "Unit prices excl. VAT but line totals incl. VAT, so lines sum to the total, not the net",
            Layout = SampleLayout.Modern,
            Labels = Labels.English,
            Culture = "en-IE",
            DateFormat = "dd/MM/yyyy",
            Money = EuroBefore,
            Supplier = new Party("Cloverfield Electronics Ltd", ["Block C, Sandyford Business Park", "Dublin 18, D18 X2C4", "Ireland"], "IE 3947261PH"),
            Customer = new Party("Tidewater Consulting", ["41 Harcourt Street", "Dublin 2"]),
            InvoiceNumber = "CFE-26-01188",
            Date = new DateOnly(2026, 5, 14),
            Currency = "EUR",
            LinesIncludeVat = true,
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice DiscountLine()
    {
        var p = Pricing.Net(
            ("Website maintenance, Q2 2026", 1, 1200.00m, 0.21m, null),
            ("Hosting and SSL, 12 months", 1, 240.00m, 0.21m, null),
            ("Loyalty discount 10%", 1, -144.00m, 0.21m, null));

        return new SampleInvoice
        {
            Id = "synthetic-06-discount-line",
            Purpose = "Discount printed as a negative line",
            Layout = SampleLayout.Classic,
            Labels = Labels.English,
            Culture = "en-GB",
            DateFormat = "dd MMM yyyy",
            Money = EuroBefore,
            Supplier = new Party("Brightwave Studio B.V.", ["Keizersgracht 221", "1016 DV Amsterdam", "The Netherlands"], "NL862117503B01"),
            Customer = new Party("Hollow Oak Bakery", ["Overtoom 88", "1054 HN Amsterdam"]),
            InvoiceNumber = "2026-0147",
            Date = new DateOnly(2026, 6, 2),
            DueDate = new DateOnly(2026, 6, 16),
            Currency = "EUR",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice TwoVatRates()
    {
        var p = Pricing.Net(
            ("Frühstücksbuffet, pro Person", 24, 14.50m, 0.07m, null),
            ("Obstplatte, groß", 2, 28.00m, 0.07m, null),
            ("Kaffee & Tee Flatrate, pro Person", 24, 3.80m, 0.19m, null),
            ("Orangensaft, 1 l", 12, 4.20m, 0.19m, null));

        return new SampleInvoice
        {
            Id = "synthetic-07-two-vat-rates",
            Purpose = "Two VAT rates (7% food, 19% drinks) that must be summed into one VAT amount",
            Layout = SampleLayout.Minimal,
            Labels = Labels.German,
            Culture = "de-DE",
            DateFormat = "dd.MM.yyyy",
            Money = Euro,
            ShowVatRateColumn = true,
            Supplier = new Party("Café Morgenrot KG", ["Gärtnerplatz 5", "80469 München"], "DE294410887"),
            Customer = new Party("Lindgrün Marketing GmbH", ["Sendlinger Str. 42", "80331 München"]),
            InvoiceNumber = "MR-5519",
            Date = new DateOnly(2026, 5, 29),
            DeliveryDate = new DateOnly(2026, 5, 27),
            Currency = "EUR",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice MultiPageParts()
    {
        string[] parts =
        [
            "Hex bolt M8 x 40, zinc plated (box of 100)", "Hex nut M8, zinc plated (box of 200)", "Flat washer M8 (box of 500)",
            "Socket cap screw M6 x 20 (box of 100)", "Threaded rod M10 x 1 m", "Hose clamp 20-32 mm (pack of 10)",
            "Cable ties 300 mm, black (pack of 100)", "Heat shrink tubing kit, 560 pcs", "Safety gloves, nitrile, size L (pair)",
            "Safety glasses, clear, anti-fog", "Ear plugs, foam (box of 200)", "Work light LED 50 W, IP65",
            "Extension cord 25 ft, 12 AWG", "Duct tape 2 in x 60 yd", "Shop towels, blue (roll of 55)",
            "Anti-seize compound, 8 oz", "Thread locker, medium strength, 10 ml", "Penetrating oil, 11 oz spray",
            "Drill bit set, cobalt, 29 pcs", "Step drill bit 1/4 - 1-3/8 in", "Grinding disc 4-1/2 in (pack of 10)",
            "Cut-off wheel 4-1/2 in (pack of 25)", "Flap disc 60 grit, 4-1/2 in", "Wire brush, stainless, 4 in",
            "Measuring tape 25 ft", "Utility knife, retractable", "Utility blades (pack of 100)",
            "Zip-top parts bags 4 x 6 in (pack of 500)", "Label tape 3/4 in, black on white", "Marking paint, fluorescent orange",
            "Pipe thread sealant tape (pack of 10)", "Spray lubricant, silicone, 11 oz",
        ];
        decimal[] prices = [14.20m, 11.85m, 9.40m, 18.75m, 6.30m, 12.90m, 8.45m, 21.99m, 3.15m, 4.60m, 17.80m, 44.95m, 38.50m, 7.25m, 19.99m, 12.35m, 9.95m, 6.80m, 64.00m, 27.40m, 23.50m, 31.25m, 5.90m, 8.75m, 11.20m, 9.85m, 18.40m, 22.60m, 13.95m, 7.40m, 10.50m, 8.10m];
        int[] quantities = [4, 2, 3, 2, 10, 5, 6, 1, 24, 12, 2, 3, 2, 8, 6, 2, 4, 6, 1, 2, 3, 2, 10, 4, 5, 6, 2, 1, 3, 12, 5, 6];

        var p = Pricing.Net([.. parts.Select((name, i) => (name, (decimal)quantities[i], prices[i], 0.0825m, (string?)$"IIS-{3100 + i * 7}"))]);

        return new SampleInvoice
        {
            Id = "synthetic-08-multipage-parts",
            Purpose = "32 line items across two pages with SKUs and US sales tax",
            Layout = SampleLayout.Classic,
            Labels = Labels.UsEnglish,
            Culture = "en-US",
            DateFormat = "MMMM d, yyyy",
            Money = Dollar,
            Supplier = new Party("Ironbridge Industrial Supply Inc.", ["7400 Navigation Blvd", "Houston, TX 77011", "USA"], "76-2290418"),
            Customer = new Party("Gulf Coast Fabrication LLC", ["1180 Industrial Park Dr", "Pasadena, TX 77506"]),
            InvoiceNumber = "IIS-240918",
            Date = new DateOnly(2026, 7, 8),
            DueDate = new DateOnly(2026, 8, 7),
            References = [("Customer PO", "GCF-PO-3378")],
            Currency = "USD",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice TotalMismatch()
    {
        var p = Pricing.Net(
            ("Monthly garden maintenance, July", 1, 320.00m, 0.20m, null),
            ("Hedge trimming (hours)", 3, 45.00m, 0.20m, null));

        return new SampleInvoice
        {
            Id = "synthetic-09-total-mismatch",
            Purpose = "Printed total has transposed digits (564.00 instead of 546.00); validation must flag it",
            Layout = SampleLayout.Modern,
            Labels = Labels.English,
            Culture = "en-GB",
            DateFormat = "dd/MM/yyyy",
            Money = Pound,
            Supplier = new Party("Fernhill Garden Services Ltd", ["The Old Barn, Fernhill Lane", "Guildford GU3 1BQ"], "GB 318 4471 09"),
            Customer = new Party("Mrs A. Whitfield", ["12 Chestnut Close", "Godalming GU7 2LR"]),
            InvoiceNumber = "FGS-0712",
            Date = new DateOnly(2026, 7, 31),
            Currency = "GBP",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount,
            Total = 564.00m,
            ExpectedIssues = [ValidationRules.NetPlusVatEqualsTotal],
        };
    }

    private static SampleInvoice LinesDoNotAddUp()
    {
        var p = Pricing.Net(
            ("Ethiopia Yirgacheffe, whole bean, 5 lb", 5, 72.50m, 0.07m, null),
            ("Colombia Supremo, whole bean, 5 lb", 3, 65.00m, 0.07m, null),
            ("Freight", 1, 30.00m, 0.07m, null));

        // The printed subtotal includes a line that is missing from the table.
        const decimal printedNet = 612.50m;
        var vat = Math.Round(printedNet * 0.07m, 2, MidpointRounding.AwayFromZero);

        return new SampleInvoice
        {
            Id = "synthetic-10-lines-dont-add-up",
            Purpose = "Line items sum to 587.50 but the printed subtotal is 612.50; validation must flag it",
            Layout = SampleLayout.Classic,
            Labels = Labels.UsEnglish,
            Culture = "en-US",
            DateFormat = "MM/dd/yyyy",
            Money = Dollar,
            Supplier = new Party("Summit Coffee Roasters", ["88 Canyon Rd", "Boulder, CO 80302"], "84-3310952"),
            Customer = new Party("The Daily Grind Cafe", ["1450 Pearl St", "Boulder, CO 80302"]),
            InvoiceNumber = "SCR-20260815",
            Date = new DateOnly(2026, 8, 15),
            DueDate = new DateOnly(2026, 9, 14),
            Currency = "USD",
            Lines = p.Lines,
            VatRows = [new VatRow(0.07m, printedNet, vat)],
            Net = printedNet,
            Vat = vat,
            Total = printedNet + vat,
            ExpectedIssues = [ValidationRules.LinesSumToNetOrTotal],
        };
    }

    private static SampleInvoice FrenchPngScan()
    {
        var p = Pricing.Net(
            ("Lampe de bureau en laiton", 2, 129.00m, 0.20m, null),
            ("Abat-jour en lin naturel", 4, 34.50m, 0.20m, null),
            ("Frais de livraison", 1, 15.00m, 0.20m, null));

        return new SampleInvoice
        {
            Id = "synthetic-11-fr-image",
            Purpose = "Image (PNG) instead of PDF, French labels, month name date (7 mai 2026), space thousands separator",
            Layout = SampleLayout.Minimal,
            Format = SampleFormat.Png,
            Labels = Labels.French,
            Culture = "fr-FR",
            DateFormat = "d MMMM yyyy",
            Money = Euro,
            Supplier = new Party("Atelier Lumière SARL", ["27 rue Mercière", "69002 Lyon", "France"], "FR 41 824 117 390"),
            Customer = new Party("Maison Duval", ["9 place Bellecour", "69002 Lyon"]),
            InvoiceNumber = "F-2026-0092",
            Date = new DateOnly(2026, 5, 7),
            DueDate = new DateOnly(2026, 6, 6),
            Currency = "EUR",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }

    private static SampleInvoice ManyReferenceNumbers()
    {
        var p = Pricing.Net(
            ("Pallet storage, June (per pallet-week)", 64, 3.25m, 0.20m, null),
            ("Inbound handling (per pallet)", 18, 4.10m, 0.20m, null),
            ("Outbound handling (per pallet)", 22, 4.10m, 0.20m, null));

        return new SampleInvoice
        {
            Id = "synthetic-12-many-reference-numbers",
            Purpose = "Invoice number among order, account, PO and delivery note numbers; invoice date among order, delivery and due dates",
            Layout = SampleLayout.Modern,
            Labels = Labels.English,
            Culture = "en-GB",
            DateFormat = "dd/MM/yyyy",
            Money = Pound,
            Supplier = new Party("Westgate Logistics Ltd", ["Units 7-9, Westgate Trade Park", "Leeds LS12 6DL"], "GB 285 9930 14"),
            Customer = new Party("Northern Table Co.", ["3 Wharf Approach", "Leeds LS1 4BR"]),
            InvoiceNumber = "WL-INV-88213",
            Date = new DateOnly(2026, 6, 30),
            OrderDate = new DateOnly(2026, 6, 2),
            DeliveryDate = new DateOnly(2026, 6, 27),
            DueDate = new DateOnly(2026, 7, 30),
            References = [("Sales order", "SO-771902"), ("Account no.", "ACC-33019"), ("Your PO", "PO-2026-4471"), ("Delivery note", "DN-55012")],
            Currency = "GBP",
            Lines = p.Lines, VatRows = p.VatRows, Net = p.NetAmount, Vat = p.VatAmount, Total = p.TotalAmount,
        };
    }
}
