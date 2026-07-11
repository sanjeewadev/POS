using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using POS.Core.Configuration;

namespace POS.Core.Models
{
    public class GrnHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string GrnNumber { get; set; } = string.Empty;

        // Optional link to Purchase Order.
        public int? PurchaseOrderId { get; set; }

        public PoHeader? PurchaseOrder { get; set; }

        [Required]
        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; } = null!;

        // Supplier/vendor invoice number.
        // Must be unique per supplier in AppDbContext.
        [Required]
        [MaxLength(50)]
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

        public int CreditDays { get; set; } = 30;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL TOTALS
        // =========================================================

        // Before VAT and after line discount depends on repository calculation.
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GlobalBillDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreightAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscountAmount { get; set; } = 0m;

        // Product VAT only.
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVatAmount { get; set; } = 0m;

        // Final amount posted to supplier ledger.
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPayable { get; set; } = 0m;

        // =========================================================
        // IMMUTABLE TAX SNAPSHOT FOUNDATION
        // =========================================================
        // Nullable values avoid inventing VAT classifications for legacy GRNs.

        public bool? IsTaxInclusive { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxableAmountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StandardRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ZeroRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExemptAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OutOfScopeAmount { get; set; }

        [MaxLength(30)]
        public string? FreightTaxCategoryCodeSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FreightTaxableAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FreightVatAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string FreightTaxSnapshotStatus { get; set; } = TaxSnapshotStatuses.LegacyUnknown;

        [Required]
        [MaxLength(30)]
        public string TaxSnapshotStatus { get; set; } = TaxSnapshotStatuses.LegacyUnknown;

        // Posted, Cancelled.
        // Draft removed from GRN workflow.
        [MaxLength(30)]
        public string Status { get; set; } = "Posted";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PostedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? PostedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public ICollection<GrnLine> GrnLines { get; set; } = new List<GrnLine>();

        [NotMapped]
        public bool IsPosted => Status == "Posted";

        [NotMapped]
        public bool IsCancelled => Status == "Cancelled";
    }
}