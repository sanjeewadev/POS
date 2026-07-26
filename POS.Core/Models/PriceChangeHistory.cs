using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using POS.Core.Configuration;
using POS.Core.Services.Pricing;

namespace POS.Core.Models
{
    public class PriceChangeHistory
    {
        public int Id { get; set; }

        // One save action can create many price history rows.
        // Example: PCH-00001.
        [Required]
        [MaxLength(30)]
        public string PriceChangeNo { get; set; } = string.Empty;

        // Master or Batch.
        [Required]
        [MaxLength(20)]
        public string PriceLevel { get; set; } = "Master";

        // PriceManagement, GRN, Import, AdminCorrection later.
        [Required]
        [MaxLength(50)]
        public string ChangeSource { get; set; } = "PriceManagement";

        [Required]
        [MaxLength(40)]
        public string ChangeAction { get; set; } = PriceChangeActionCodes.Legacy;

        [Required]
        [MaxLength(30)]
        public string OldPriceSource { get; set; } = SellingPriceSourceCodes.LegacyUnknown;

        [Required]
        [MaxLength(30)]
        public string NewPriceSource { get; set; } = SellingPriceSourceCodes.LegacyUnknown;

        // =========================================================
        // ITEM / BATCH REFERENCES
        // =========================================================

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        public int? ItemBatchId { get; set; }

        public ItemBatch? ItemBatch { get; set; }

        // =========================================================
        // SOURCE DOCUMENT AUDIT
        // Populated when a price change originates from a document
        // such as a posted GRN. Existing/manual history remains blank.
        // =========================================================

        [MaxLength(30)]
        public string SourceDocumentType { get; set; } = string.Empty;

        public int? SourceDocumentId { get; set; }

        public int? SourceDocumentLineId { get; set; }

        [MaxLength(50)]
        public string SourceDocumentNo { get; set; } = string.Empty;

        // =========================================================
        // SNAPSHOT FIELDS
        // Keep these even if item/batch names change later.
        // =========================================================

        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ItemDescription { get; set; } = string.Empty;

        [MaxLength(250)]
        public string VariantDescription { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? BatchExpiryDate { get; set; }

        // Cost is read-only in Price Management.
        // This is only a snapshot for margin/audit.
        [Column(TypeName = "decimal(18,2)")]
        public decimal EffectiveCost { get; set; } = 0m;

        // =========================================================
        // OLD PRICE SNAPSHOT
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldMinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewMinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldRetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewRetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldWholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewWholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldMaximumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewMaximumPrice { get; set; } = 0m;

        // =========================================================
        // AUDIT
        // =========================================================

        [Required]
        [MaxLength(100)]
        public string ChangedBy { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string ChangeReason { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [NotMapped]
        public bool IsMasterPriceChange =>
            PriceLevel.Equals("Master", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsBatchPriceChange =>
            PriceLevel.Equals("Batch", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool HasRetailPriceChanged => OldRetailPrice != NewRetailPrice;

        [NotMapped]
        public bool HasWholesalePriceChanged => OldWholesalePrice != NewWholesalePrice;

        [NotMapped]
        public bool HasMinimumPriceChanged => OldMinimumPrice != NewMinimumPrice;

        [NotMapped]
        public bool HasMaximumPriceChanged => OldMaximumPrice != NewMaximumPrice;
    }
}