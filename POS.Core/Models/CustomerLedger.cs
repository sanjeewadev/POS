using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CustomerLedger
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerMasterId { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string DocumentRef { get; set; } = string.Empty; // e.g., "INV-2026-001", "RCPT-500", "RTN-102"

        [Required]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty; // e.g., "Credit Sale", "Payment Received", "Return Credit"

        // ==========================================
        // THE MATH
        // ==========================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal DebitAmount { get; set; } = 0m; // Money the customer owes YOU (Increases Debt)

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditAmount { get; set; } = 0m; // Money the customer paid YOU (Reduces Debt)

        // ==========================================
        // AUDIT TRAIL
        // ==========================================
        [MaxLength(100)]
        public string ProcessedBy { get; set; } = string.Empty; // Which Cashier/Admin processed this specific entry

        [MaxLength(255)]
        public string Remarks { get; set; } = string.Empty; // e.g., "Bank Transfer Ref #9982"

        // Navigation Property
        [ForeignKey("CustomerMasterId")]
        public virtual CustomerMaster? CustomerMaster { get; set; }
    }
}