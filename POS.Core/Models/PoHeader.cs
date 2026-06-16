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
        public string PoNumber { get; set; } = string.Empty;

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
        public decimal TotalDiscountAmount { get; set; } = 0m;
        public decimal NetPayable { get; set; } = 0m;

        // CRITICAL UPDATE: Financial consistency with the GRN Module
        public bool IsTaxInclusive { get; set; } = false;

        // Document Lifecycle: Draft -> Approved -> Partially Received -> Closed -> Canceled
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<PoLine> PoLines { get; set; } = new List<PoLine>();
    }
}