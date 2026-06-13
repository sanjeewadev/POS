using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class PoHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string PoNumber { get; set; } = string.Empty; // e.g., PO-2026-0045

        // Foreign Key to Supplier
        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime ExpectedDate { get; set; } = DateTime.Now.AddDays(7);

        [MaxLength(50)]
        public string Terms { get; set; } = "30 Days Credit";

        public int CreditDays { get; set; } = 30;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // --- GLOBAL ESTIMATED FINANCIALS ---
        public decimal Subtotal { get; set; } = 0m;
        public decimal GlobalBillDiscount { get; set; } = 0m;
        public decimal TotalTaxAmount { get; set; } = 0m;
        public decimal TotalDiscountAmount { get; set; } = 0m; // Sum of line discounts + global discount
        public decimal NetPayable { get; set; } = 0m; // Expected final value

        // Document Lifecycle: Draft -> Approved -> Partially Received -> Closed -> Canceled
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty; // Requisitioner

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: One PO contains Many Requested Items
        public ICollection<PoLine> PoLines { get; set; } = new List<PoLine>();
    }
}