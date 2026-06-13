using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class ReturnHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string ReturnNumber { get; set; } = string.Empty; // e.g., RTN-2026-0005

        // Foreign Key to Supplier
        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        // The specific historical invoice this return is linked to
        [MaxLength(50)]
        public string OriginalInvoiceNo { get; set; } = string.Empty;

        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty; // Dispatch Notes

        // --- FINANCIALS ---
        public decimal GrossCredit { get; set; } = 0m;      // Sum of all returned items' historical values
        public decimal RestockingFee { get; set; } = 0m;    // Supplier penalty for returning stock

        // The actual amount deducted from the Supplier Ledger (GrossCredit - RestockingFee)
        public decimal NetCredit { get; set; } = 0m;

        // Document Lifecycle: Draft -> Posted
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: One Return Note contains Many Line Items
        public ICollection<ReturnLine> ReturnLines { get; set; } = new List<ReturnLine>();
    }
}