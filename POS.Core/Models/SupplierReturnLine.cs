using System;
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

        [ForeignKey(nameof(ReturnHeaderId))]
        public virtual SupplierReturnHeader? ReturnHeader { get; set; }

        // Optional source GRN line.
        public int? GrnLineId { get; set; }

        [ForeignKey(nameof(GrnLineId))]
        public virtual GrnLine? GrnLine { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        [ForeignKey(nameof(ItemVariantId))]
        public virtual ItemVariant? ItemVariant { get; set; }

        // Critical: return must deduct exact physical batch.
        [Required]
        public int ItemBatchId { get; set; }

        [ForeignKey(nameof(ItemBatchId))]
        public virtual ItemBatch? ItemBatch { get; set; }

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal ReturnQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal HistoricalCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditValue { get; set; } = 0m;

        [Required]
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string LineRemarks { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // UI helpers
        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public decimal CurrentBatchStock { get; set; } = 0m;

        [NotMapped]
        public decimal MaxReturnQty { get; set; } = 0m;
    }
}