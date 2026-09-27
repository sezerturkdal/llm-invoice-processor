namespace InvoiceProcessor.Evals.Samples;

/// <summary>Printed labels, so samples come in the languages real invoices do.</summary>
public sealed record Labels(
    string Invoice,
    string InvoiceNumber,
    string InvoiceDate,
    string DueDate,
    string DeliveryDate,
    string OrderDate,
    string BillTo,
    string Description,
    string Qty,
    string UnitPrice,
    string VatRate,
    string LineTotal,
    string LineTotalInclVat,
    string Net,
    string VatOn,
    string Total,
    string TaxId,
    string Page)
{
    public static readonly Labels English = new(
        "Invoice", "Invoice number", "Invoice date", "Due date", "Delivery date", "Order date", "Bill to",
        "Description", "Qty", "Unit price", "VAT", "Amount", "Amount (incl. VAT)",
        "Subtotal (excl. VAT)", "VAT {0} on {1}", "Total due", "VAT no.", "Page");

    public static readonly Labels UsEnglish = English with
    {
        VatRate = "Tax",
        Net = "Subtotal",
        VatOn = "Sales tax {0} on {1}",
        TaxId = "EIN",
    };

    public static readonly Labels German = new(
        "Rechnung", "Rechnungsnummer", "Rechnungsdatum", "Fällig am", "Lieferdatum", "Bestelldatum", "Rechnungsempfänger",
        "Bezeichnung", "Menge", "Einzelpreis", "USt.", "Betrag", "Betrag (brutto)",
        "Nettobetrag", "USt. {0} auf {1}", "Rechnungsbetrag", "USt-IdNr.", "Seite");

    public static readonly Labels Turkish = new(
        "Fatura", "Fatura No", "Fatura Tarihi", "Son Ödeme Tarihi", "Teslim Tarihi", "Sipariş Tarihi", "Sayın",
        "Açıklama", "Miktar", "Birim Fiyat", "KDV", "Tutar", "Tutar (KDV dahil)",
        "Ara Toplam", "KDV {0} ({1} üzerinden)", "Genel Toplam", "Vergi No", "Sayfa");

    public static readonly Labels French = new(
        "Facture", "Facture n°", "Date de facture", "Échéance", "Date de livraison", "Date de commande", "Facturé à",
        "Désignation", "Qté", "Prix unitaire", "TVA", "Montant HT", "Montant TTC",
        "Total HT", "TVA {0} sur {1}", "Total TTC", "N° TVA", "Page");
}
