using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class InventoryTransaction
    {
        public int Id { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        // Optional batch link.
        public int? ItemBatchId { get; set; }

        public ItemBatch? ItemBatch { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // GRN, SALE, RETURN, ADJUSTMENT, SUPPLIER_RETURN.
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        // Example: GRN-00001, INV-00001.
        [Required]
        [MaxLength(50)]
        public string ReferenceDocument { get; set; } = string.Empty;

        // Optional link to source document row.
        // For GRN, this can store GrnLine.Id.
        public int? ReferenceLineId { get; set; }

        // Positive for stock-in, negative for stock-out.
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        // Snapshot cost at transaction time.
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; } = 0m;

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Remarks { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal TotalCost => Quantity * UnitCost;
    }
}