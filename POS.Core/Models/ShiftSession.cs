using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ShiftSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; } = DateTime.Now;

        public DateTime? EndTime { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Open"; // "Open" or "Closed"

        // ==========================================
        // Z-REPORT MATH (THE BLIND CLOSE LOGIC)
        // ==========================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningCash { get; set; } = 0m; // The float injected by the manager

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashSales { get; set; } = 0m; // Sum of all cash receipts

        // ExpectedCash is calculated by the system before closing: (OpeningCash + TotalCashSales +/- CashMovements)
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCash { get; set; } = 0m;

        // ActualCash is blindly counted by the cashier using the CashDenominationDialog
        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualCash { get; set; } = 0m;

        // Variance = ActualCash - ExpectedCash (Negative = Shortage, Positive = Overage)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Variance { get; set; } = 0m;

        // ==========================================
        // NAVIGATION PROPERTIES
        // ==========================================

        // Tracks all Float Injections, Safe Drops, and Petty Cash payouts during this shift
        public virtual ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
    }
}