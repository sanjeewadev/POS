using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class PoLine
    {
        public int Id { get; set; }

        // Foreign Key to the Header
        public int PoHeaderId { get; set; }
        public PoHeader PoHeader { get; set; } = null!;

        // Foreign Key to the exact Matrix Variant (e.g., "Linen Shirt - Blue - Large")
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // Snapshot of UOM at the time of ordering
        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // --- QUANTITY TRACKING (The Bridge to GRN) ---
        public decimal OrderQty { get; set; } = 0m;

        // This is updated automatically in the background when a GRN is posted against this PO.
        public decimal ReceivedQty { get; set; } = 0m;

        // --- ESTIMATED PRICING ---
        // These are expected costs. They do NOT update the Item Master's true average cost yet.
        // That only happens during the GRN phase when actual costs are finalized.
        public decimal ExpectedCost { get; set; } = 0m;
        public decimal LineDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; } = 0m;

        public decimal LineTotal { get; set; } = 0m; // (OrderQty * ExpectedCost) - LineDiscount + TaxAmount
    }
}