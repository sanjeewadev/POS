using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GrnLine
    {
        public int Id { get; set; }

        public int GrnHeaderId { get; set; }

        public GrnHeader GrnHeader { get; set; } = null!;

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        // Optional exact PO line link.
        public int? PoLineId { get; set; }

        public PoLine? PoLine { get; set; }

        // Exact batch created or updated by this GRN line.
        // This is required for enterprise audit and batch-selected cashier sales.
        public int? ItemBatchId { get; set; }

        public ItemBatch? ItemBatch { get; set; }

        // =========================================================
        // UI HELPERS - NOT SAVED
        // =========================================================

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string Barcode { get; set; } = string.Empty;

        [NotMapped]
        public decimal RemainingPoQty => OrderedQty - ReceivedQty < 0 ? 0 : OrderedQty - ReceivedQty;

        [NotMapped]
        public string BatchDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BatchNo))
                    return "[AUTO]";

                return BatchNo;
            }
        }

        [NotMapped]
        public string ExpiryDisplayText
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return "No Expiry";

                return ExpiryDate.Value.ToString("yyyy-MM-dd");
            }
        }

        // =========================================================
        // LOGISTICS
        // =========================================================

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // =========================================================
        // QUANTITIES
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal OrderedQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal ReceivedQty { get; set; } = 0m;

        // =========================================================
        // COSTING
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscount { get; set; } = 0m;

        // Unit landed cost after freight/global discount allocation.
        [Column(TypeName = "decimal(18,2)")]
        public decimal LandedCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0m;

        // Open, Posted, Cancelled.
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}