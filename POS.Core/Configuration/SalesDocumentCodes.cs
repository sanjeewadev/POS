namespace POS.Core.Configuration
{
    public static class SalesDocumentTypes
    {
        public const string Receipt = "Receipt";
        public const string TaxInvoice = "TaxInvoice";
    }

    public static class SalesDocumentEventTypes
    {
        public const string TaxInvoiceIssued = "TaxInvoiceIssued";
        public const string OriginalPrinted = "OriginalPrinted";
        public const string Reprinted = "Reprinted";
        public const string PrintFailed = "PrintFailed";
    }

    public static class SalesDocumentCopyLabels
    {
        public const string Original = "ORIGINAL";
        public const string Reprint = "REPRINT";
    }
}
