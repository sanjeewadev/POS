using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CashMovement
    {
        [Key]
        public int Id { get; set; }

        // Links this movement to the exact till and cashier session
        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string MovementType { get; set; } = string.Empty; // e.g., "Paid In", "Paid Out"

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReasonCategory { get; set; } = string.Empty; // e.g., "Opening Float", "Safe Drop", "Supplier Payout"

        [MaxLength(255)]
        public string Remarks { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        // CRITICAL: The manager who authorized the physical cash removal/injection
        [Required]
        [MaxLength(100)]
        public string AuthorizedBy { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string ReferenceVoucherNo { get; set; } = string.Empty;

        // Navigation Property (Activated!)
        [ForeignKey("ShiftSessionId")]
        public virtual ShiftSession? ShiftSession { get; set; }
    }
}