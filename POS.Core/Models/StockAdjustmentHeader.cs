using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class StockAdjustmentHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string AdjustmentNo { get; set; } = string.Empty; // e.g., ADJ-2026-0012

        public DateTime AdjustmentDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty; // e.g., "Monthly Store Audit"

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty; // Final Audit Notes

        // The net financial result of this adjustment (Can be negative for a loss, or positive for found stock)
        public decimal TotalImpact { get; set; } = 0m;

        // Document Lifecycle: Draft -> Posted
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: One Audit contains Many Line Items
        public ICollection<StockAdjustmentLine> AdjustmentLines { get; set; } = new List<StockAdjustmentLine>();
    }
}