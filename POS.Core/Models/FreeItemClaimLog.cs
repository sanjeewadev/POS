using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    [Table("FreeItemClaimLogs")]
    public class FreeItemClaimLog
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Description { get; set; } = string.Empty;

        // CRITICAL: We capture the exact cost at the time of the transaction.
        // If supplier prices change next month, your claim for today remains accurate.
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCostAtTime { get; set; }

        public int Quantity { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty; // e.g., "Supplier Promo", "Damaged"

        // Determines if this hits your Profit/Loss sheet OR generates a Supplier Invoice
        public bool IsRecoverable { get; set; }

        [Required]
        [MaxLength(50)]
        public string CashierId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}