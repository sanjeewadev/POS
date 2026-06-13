using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class GrnLine
    {
        public int Id { get; set; }

        // Foreign Key to the Header
        public int GrnHeaderId { get; set; }
        public GrnHeader GrnHeader { get; set; } = null!;

        // Foreign Key to the exact Matrix Variant (e.g., "Linen Shirt - Red - XL")
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // --- LOGISTICS ---
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        // Snapshot of UOM at the time of receiving
        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // --- QUANTITIES ---
        public decimal OrderedQty { get; set; } = 0m;  // If pulled from a PO
        public decimal ReceivedQty { get; set; } = 0m; // Actual physical count
        public decimal FocQty { get; set; } = 0m;      // Free of Charge items

        // --- PRICING & MATH ---
        public decimal UnitCost { get; set; } = 0m;    // The base supplier cost
        public decimal LineDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; } = 0m;

        // The exact final cost of this specific item after Freight is added and FOC/Discounts are subtracted
        public decimal LandedCost { get; set; } = 0m;

        public decimal LineTotal { get; set; } = 0m;   // (ReceivedQty * UnitCost) - Discount + Tax

        // --- SELLING PRICE UPDATES ---
        // Storing these here allows the system to update the Item Master's selling prices during the GRN post
        public decimal RetailPrice { get; set; } = 0m;
        public decimal WholesalePrice { get; set; } = 0m;
        public decimal MinimumPrice { get; set; } = 0m;
    }
}