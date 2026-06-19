using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    // Indexing the QuoteNo for fast manager lookups
    [Index(nameof(QuoteNo), IsUnique = true)]
    public class QuotationHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string QuoteNo { get; set; } = string.Empty; // e.g., QT-20260618-001

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Required]
        public DateTime ValidUntil { get; set; } // When the promised price expires

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Draft"; // "Draft", "Sent", "Accepted", "Expired", "Converted"

        // --- CUSTOMER DETAILS ---
        [Required]
        [MaxLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CustomerEmail { get; set; } = string.Empty;

        // --- STAFF DETAILS ---
        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        // --- MATH TOTALS ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetTotal { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty; // e.g., "Delivery not included"

        // Navigation Property
        public virtual ICollection<QuotationLine> QuotationLines { get; set; } = new List<QuotationLine>();
    }
}