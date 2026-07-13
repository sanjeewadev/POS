using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        // =========================================================
        // CUSTOMER SNAPSHOT
        // =========================================================
        // CustomerMasterId points to the real customer record.
        // The text fields below are snapshots saved with the invoice.
        // This protects old invoices if customer details change later.

        public int? CustomerMasterId { get; set; }

        [MaxLength(30)]
        public string CustomerCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CustomerName { get; set; } = "Walk-In";

        [MaxLength(150)]
        public string CustomerCompanyName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string CustomerPhone { get; set; } = string.Empty;

        // Walk-In / Retail / Wholesale
        [MaxLength(20)]
        public string CustomerType { get; set; } = "Walk-In";

        [MaxLength(50)]
        public string CustomerNicOrBrNumber { get; set; } = string.Empty;

        // Retail = Loyalty flag.
        // Wholesale = Discount flag.
        public bool CustomerIsDiscountEligible { get; set; } = false;

        public bool CustomerIsCreditEnabled { get; set; } = false;

        // None / PendingApproval / Active / Hold
        [MaxLength(30)]
        public string CustomerCreditStatus { get; set; } = "None";

        public bool IsWholesaleSale { get; set; } = false;

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // =========================================================
        // DOCUMENT AND TAX-INVOICE SNAPSHOT FOUNDATION
        // =========================================================
        // These fields are storage only in Phase 7B3. Cashier and printing
        // behavior will be connected in later controlled phases.

        // Receipt / TaxInvoice
        [Required]
        [MaxLength(20)]
        public string DocumentType { get; set; } = "Receipt";

        [MaxLength(50)]
        public string? TaxInvoiceNo { get; set; }

        // Stable cart idempotency token. Historical sales remain null.
        public Guid? CheckoutToken { get; set; }

        public bool IsVatRegisteredSale { get; set; } = false;

        [MaxLength(30)]
        public string SupplierTinSnapshot { get; set; } = string.Empty;

        [MaxLength(30)]
        public string SupplierVatNoSnapshot { get; set; } = string.Empty;

        [MaxLength(30)]
        public string CustomerTinSnapshot { get; set; } = string.Empty;

        [MaxLength(30)]
        public string CustomerVatNoSnapshot { get; set; } = string.Empty;

        [MaxLength(500)]
        public string CustomerAddressSnapshot { get; set; } = string.Empty;

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

        // =========================================================
        // MATH SUMMARY
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossTotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; } = 0m;

        // Checkout-only request value. The database already preserves the full
        // document discount in TotalDiscount and each line's exact allocation
        // in SalesLine.DiscountAmount, so this helper is intentionally not mapped.
        [NotMapped]
        public decimal InvoiceDiscountAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetTotal { get; set; } = 0m;

        // Face value of one-time gift vouchers issued on this receipt.
        // This remains part of the amount payable but is excluded from VAT turnover
        // and merchandise revenue calculations.
        [Column(TypeName = "decimal(18,2)")]
        public decimal GiftVoucherIssueTotal { get; set; } = 0m;

        // Physical cash handling summary.
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountTendered { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceReturned { get; set; } = 0m;

        // Kept for backward compatibility.
        // Actual payments are stored in SalesPayments.
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Split";

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Completed";

        public bool IsVoided { get; set; } = false;

        // =========================================================
        // NAVIGATION
        // =========================================================

        [ForeignKey(nameof(CustomerMasterId))]
        public virtual CustomerMaster? CustomerMaster { get; set; }

        public virtual ICollection<SalesLine> SalesLines { get; set; } = new List<SalesLine>();

        public virtual ICollection<SalesPayment> SalesPayments { get; set; } = new List<SalesPayment>();

        public virtual ICollection<SalesDocumentAudit> SalesDocumentAudits { get; set; } = new List<SalesDocumentAudit>();

        public virtual CashierCartSession? CashierCartSession { get; set; }
    }
}
