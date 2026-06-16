using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Add this using statement

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

        // --- UI HELPERS (Not saved to DB, just for the DataGrid) ---
        [NotMapped] public string ItemCode { get; set; } = string.Empty;
        [NotMapped] public string VariantDescription { get; set; } = string.Empty;
        [NotMapped] public string Description { get; set; } = string.Empty;
        [NotMapped] public string Barcode { get; set; } = string.Empty;

        // --- LOGISTICS ---
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // --- QUANTITIES ---
        public decimal OrderedQty { get; set; } = 0m;
        public decimal ReceivedQty { get; set; } = 0m;
        public decimal FocQty { get; set; } = 0m;

        // --- PRICING & MATH ---
        public decimal UnitCost { get; set; } = 0m;
        public decimal LineDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; } = 0m;

        public decimal LandedCost { get; set; } = 0m;

        public decimal LineTotal { get; set; } = 0m;

        // --- SELLING PRICE UPDATES ---
        public decimal RetailPrice { get; set; } = 0m;
        public decimal WholesalePrice { get; set; } = 0m;
        public decimal MinimumPrice { get; set; } = 0m;
    }
}