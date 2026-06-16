using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class PoLine
    {
        public int Id { get; set; }

        public int PoHeaderId { get; set; }
        public PoHeader PoHeader { get; set; } = null!;

        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // --- UI HELPERS (Not saved to DB, just for the WPF DataGrid) ---
        [NotMapped] public string ItemCode { get; set; } = string.Empty;
        [NotMapped] public string VariantDescription { get; set; } = string.Empty;
        [NotMapped] public string Description { get; set; } = string.Empty;
        [NotMapped] public string Barcode { get; set; } = string.Empty;
        [NotMapped] public decimal SOH { get; set; } = 0m; // Stock on Hand indicator

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // --- QUANTITY TRACKING ---
        public decimal OrderQty { get; set; } = 0m;

        // Updated dynamically when a GRN links to this PO
        public decimal ReceivedQty { get; set; } = 0m;

        // --- ESTIMATED PRICING ---
        public decimal ExpectedCost { get; set; } = 0m;
        public decimal LineDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; } = 0m;

        public decimal LineTotal { get; set; } = 0m;
    }
}