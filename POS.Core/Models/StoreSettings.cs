using System;

namespace POS.Core.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }

        // =========================================================
        // COMPANY / STORE IDENTITY
        // =========================================================

        public string LegalName { get; set; } = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        public string Brn { get; set; } = string.Empty;

        public string TaxNo { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;

        public string AddressLine2 { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string Country { get; set; } = "Sri Lanka";

        public string Phone { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // =========================================================
        // TAX / CURRENCY
        // =========================================================

        public decimal GlobalVatRate { get; set; } = 0m;

        public bool IsVatRegistered { get; set; } = false;

        public string TaxpayerIdentificationNumber { get; set; } = string.Empty;

        public string VatRegistrationNumber { get; set; } = string.Empty;

        public string TaxInvoicePrefix { get; set; } = "TI";

        public string CurrencyCode { get; set; } = "LKR";

        public string CurrencySymbol { get; set; } = "Rs.";

        // =========================================================
        // DOCUMENT NUMBER PREFIXES
        // =========================================================

        public string InvoicePrefix { get; set; } = "INV";

        public string PurchaseOrderPrefix { get; set; } = "PO";

        public string QuotationPrefix { get; set; } = "QT";

        // =========================================================
        // RECEIPT / INVOICE TEXT
        // =========================================================

        public string ReceiptHeader { get; set; } = string.Empty;

        public string ReceiptFooter { get; set; } = "Thank You! Come Again.";

        public string InvoiceTerms { get; set; } = string.Empty;

        // =========================================================
        // REGIONAL / FINANCIAL
        // =========================================================

        public string TimeZoneId { get; set; } = "Sri Lanka Standard Time";

        public string DateFormat { get; set; } = "dd/MM/yyyy";

        public int FinancialYearStartMonth { get; set; } = 1;

        // =========================================================
        // SYSTEM / AUDIT
        // =========================================================

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;
    }
}