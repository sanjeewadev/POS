using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SupplierReturnLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReturnHeaderId { get; set; }

        [ForeignKey("ReturnHeaderId")]
        public virtual SupplierReturnHeader? ReturnHeader { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        [ForeignKey("ItemVariantId")]
        public virtual ItemVariant? ItemVariant { get; set; }

        // CRITICAL: We must know exactly which batch we are returning 
        // to deduct the physical stock and grab the exact historical cost.
        [Required]
        public int ItemBatchId { get; set; }

        [ForeignKey("ItemBatchId")]
        public virtual ItemBatch? ItemBatch { get; set; }

        // --- QUANTITY & FINANCIAL IMPACT ---
        [Required]
        public decimal ReturnQty { get; set; } = 0m;

        [Required]
        public decimal HistoricalCost { get; set; } = 0m;

        [Required]
        public decimal CreditValue { get; set; } = 0m; // ReturnQty * HistoricalCost

        [MaxLength(100)]
        public string ReasonCode { get; set; } = string.Empty; // Damaged, Expired, Excess, etc.

        // ==========================================
        // UI HELPERS (Not mapped to the database)
        // ==========================================
        [NotMapped] public string ItemCode { get; set; } = string.Empty;
        [NotMapped] public string Description { get; set; } = string.Empty;
        [NotMapped] public string VariantDescription { get; set; } = string.Empty;
    }
}