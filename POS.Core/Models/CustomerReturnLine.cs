using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CustomerReturnLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerReturnHeaderId { get; set; }

        [ForeignKey(nameof(CustomerReturnHeaderId))]
        public virtual CustomerReturnHeader? CustomerReturnHeader { get; set; }

        // Original sales line used for exact tax reversal.
        public int? SalesLineId { get; set; }

        [ForeignKey(nameof(SalesLineId))]
        public virtual SalesLine? SalesLine { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        // Null for Service returns/refunds.
        public int? ItemBatchId { get; set; }

        [ForeignKey(nameof(ItemBatchId))]
        public virtual ItemBatch? ItemBatch { get; set; }

        [Required]
        [MaxLength(255)]
        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityReturned { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundValue { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalRefund { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReturnReason { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InventoryAction { get; set; } = string.Empty;

        // =========================================================
        // ORIGINAL ITEM/TAX SNAPSHOTS
        // =========================================================

        [MaxLength(20)]
        public string? ItemTypeSnapshot { get; set; }

        public int? TaxCategoryId { get; set; }

        public TaxCategory? TaxCategory { get; set; }

        public int? TaxRateId { get; set; }

        public TaxRate? TaxRate { get; set; }

        [MaxLength(30)]
        public string? TaxCategoryCodeSnapshot { get; set; }

        [MaxLength(20)]
        public string? TaxCodeSnapshot { get; set; }

        [MaxLength(100)]
        public string? TaxNameSnapshot { get; set; }

        [Column(TypeName = "decimal(7,4)")]
        public decimal? TaxRatePercentSnapshot { get; set; }

        public bool? IsTaxInclusiveSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxableAmountSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? VatAmountSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxInclusiveAmountSnapshot { get; set; }

        // Full original sales-line values are preserved separately so a partial
        // return can store both the source values and the returned portion.
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalTaxableAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalVatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalTaxInclusiveAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string TaxSnapshotStatus { get; set; } = "LegacyUnknown";
    }
}
