using System;
using POS.Core.Models;

namespace POS.Core.Models.DTOs
{
    public sealed class TaxInvoiceIssueRequest
    {
        public int SalesHeaderId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerTin { get; set; } = string.Empty;

        public string CustomerVatNo { get; set; } = string.Empty;

        public string CustomerAddress { get; set; } = string.Empty;

        public string PerformedBy { get; set; } = string.Empty;

        public string TerminalNo { get; set; } = string.Empty;
    }

    public sealed class PreparedSalesDocument
    {
        public SalesHeader Sale { get; init; } = null!;

        public string DocumentType { get; init; } = string.Empty;

        public string DocumentNumber { get; init; } = string.Empty;

        public string CopyLabel { get; init; } = string.Empty;

        public int NextCopyNumber { get; init; }

        public DateTime? TaxInvoiceIssuedAtUtc { get; init; }
    }
}
