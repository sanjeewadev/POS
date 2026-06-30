using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GiftVoucherTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GiftVoucherId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // Created
        // Printed
        // Activated
        // Redeemed
        // Expired
        // Blocked
        // Cancelled
        // Reopened
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        // Snapshot of voucher number for easier reporting.
        [MaxLength(50)]
        public string VoucherNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // Voucher face value at the time of transaction.
        [Column(TypeName = "decimal(18,2)")]
        public decimal VoucherAmount { get; set; } = 0m;

        // Main transaction amount.
        // For Activated: usually VoucherAmount.
        // For Redeemed: actual amount applied to invoice.
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } = 0m;

        // Used mainly for redemption.
        [Column(TypeName = "decimal(18,2)")]
        public decimal AppliedAmount { get; set; } = 0m;

        // Used when one-time voucher value is greater than invoice balance.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ForfeitedAmount { get; set; } = 0m;

        // Status after transaction.
        // Example: Created, Active, Redeemed, Blocked.
        [MaxLength(30)]
        public string StatusAfter { get; set; } = string.Empty;

        // Optional sales reference.
        public int? SalesHeaderId { get; set; }

        [MaxLength(50)]
        public string ReferenceInvoiceNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // =========================================================
        // NAVIGATION
        // =========================================================

        [ForeignKey(nameof(GiftVoucherId))]
        public virtual GiftVoucher? GiftVoucher { get; set; }

        [ForeignKey(nameof(SalesHeaderId))]
        public virtual SalesHeader? SalesHeader { get; set; }
    }
}