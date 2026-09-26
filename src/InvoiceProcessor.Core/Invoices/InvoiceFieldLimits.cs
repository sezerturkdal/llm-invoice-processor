namespace InvoiceProcessor.Core.Invoices;

/// <summary>Maximum text lengths, shared by the database mapping, extraction and review input checks.</summary>
public static class InvoiceFieldLimits
{
    public const int FileName = 260;
    public const int Supplier = 256;
    public const int InvoiceNumber = 128;
    public const int Currency = 8;
    public const int LineDescription = 1024;
}
