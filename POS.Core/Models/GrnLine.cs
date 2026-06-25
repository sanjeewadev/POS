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

        // --- UI HELPERS (Not saved to DB, just for the WPF DataGrid) ---
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

        // --- PRICING & MATH (Pure Cost Data) ---
        public decimal UnitCost { get; set; } = 0m;
        public decimal LineDiscount { get; set; } = 0m;
        public decimal LandedCost { get; set; } = 0m;
        public decimal LineTotal { get; set; } = 0m;
    }
}