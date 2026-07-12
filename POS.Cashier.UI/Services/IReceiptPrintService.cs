using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using POS.Core.Configuration;
using POS.Core.Models;

namespace POS.Cashier.UI.Services
{
    public interface IReceiptPrintService
    {
        Task<string> BuildReceiptPreviewAsync(
            SalesHeader transaction,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task<string> BuildTaxInvoicePreviewAsync(
            SalesHeader transaction,
            DateTime issuedAtUtc,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task PrintReceiptAsync(
            SalesHeader transaction,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task PrintTaxInvoiceAsync(
            SalesHeader transaction,
            DateTime issuedAtUtc,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task<string> BuildCreditNotePreviewAsync(
            CustomerReturnHeader returnHeader,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task PrintCreditNoteAsync(
            CustomerReturnHeader returnHeader,
            string printerName,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original);

        Task PrintQuotationAsync(
            QuotationPrintRequest request,
            string printerName,
            int paperWidth);

        Task PrintTextAsync(
            string documentText,
            string printerName,
            string documentName);

        Task OpenCashDrawerAsync(
            string printerName);
    }

    public sealed class QuotationPrintRequest
    {
        public string QuotationNo { get; set; } =
            string.Empty;

        public DateTime QuotationDate { get; set; } =
            DateTime.Now;

        public string CashierName { get; set; } =
            string.Empty;

        public string TerminalNo { get; set; } =
            string.Empty;

        public string CustomerName { get; set; } =
            "Walk-In";

        public decimal GrossTotal { get; set; }

        public decimal TotalDiscount { get; set; }

        public decimal NetTotal { get; set; }

        public List<QuotationPrintLine> Lines
        {
            get;
            set;
        } = new();
    }

    public sealed class QuotationPrintLine
    {
        public int LineNo { get; set; }

        public string ItemDescription { get; set; } =
            string.Empty;

        public string SkuCode { get; set; } =
            string.Empty;

        public string Barcode { get; set; } =
            string.Empty;

        public string Uom { get; set; } =
            string.Empty;

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal LineTotal { get; set; }
    }
}
