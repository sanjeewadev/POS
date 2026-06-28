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
        public decimal SOH { get; set; } = 0m;

        [NotMapped]
        public int Moq { get; set; } = 1;

        [NotMapped]
        public decimal RemainingQty => OrderQty - ReceivedQty < 0 ? 0 : OrderQty - ReceivedQty;

        [NotMapped]
        public bool IsFullyReceived => OrderQty > 0 && ReceivedQty >= OrderQty;

        // =========================================================
        // SAVED LINE DATA
        // =========================================================

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // Supplier's catalog/invoice item code.
        [MaxLength(100)]
        public string SupplierItemCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,3)")]
        public decimal OrderQty { get; set; } = 0m;

        // Updated by GRN receiving, not by PO editing.
        [Column(TypeName = "decimal(18,3)")]
        public decimal ReceivedQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0m;

        // Open, Partially Received, Closed, Cancelled.
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }
    }
}