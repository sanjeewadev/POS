using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    [Index(nameof(ReturnNo), IsUnique = true)]
    public class CustomerReturnHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? OriginalInvoiceNo { get; set; }

        // Authoritative source link for future receipt-based returns.
        // Kept nullable because legacy or blind returns may not have a source sale.
        public int? OriginalSalesHeaderId { get; set; }

        [ForeignKey(nameof(OriginalSalesHeaderId))]
        public virtual SalesHeader? OriginalSalesHeader { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRefundAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string RefundMethod { get; set; } = "Cash";

        // Return / CreditNote
        [Required]
        [MaxLength(20)]
        public string DocumentType { get; set; } = "Return";

        [MaxLength(50)]
        public string? CreditNoteNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxableAmountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalVatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StandardRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ZeroRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExemptAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OutOfScopeAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string TaxSnapshotStatus { get; set; } = "LegacyUnknown";

        public virtual ICollection<CustomerReturnLine> Lines { get; set; } = new List<CustomerReturnLine>();
    }
}
