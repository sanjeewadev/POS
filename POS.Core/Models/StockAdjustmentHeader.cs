using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class StockAdjustmentHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string AdjustmentNo { get; set; } = string.Empty;

        public DateTime AdjustmentDate { get; set; } = DateTime.Now;

        // Physical Count Correction, Stock Increase, Stock Decrease.
        [Required]
        [MaxLength(50)]
        public string AdjustmentMode { get; set; } = "Physical Count Correction";

        [Required]
        [MaxLength(50)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // Net financial impact.
        // Negative = loss/shrinkage.
        // Positive = found stock/gain.
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalImpact { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalIncreaseQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalDecreaseQty { get; set; } = 0m;

        // Draft, Posted, Cancelled.
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PostedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? PostedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public ICollection<StockAdjustmentLine> AdjustmentLines { get; set; } = new List<StockAdjustmentLine>();

        [NotMapped]
        public bool IsPosted => Status == "Posted";

        [NotMapped]
        public bool IsCancelled => Status == "Cancelled";
    }
}