using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SuspendedTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReferenceNo { get; set; } = string.Empty; // e.g., "HLD-260615-001"

        [Required]
        public int ShiftSessionId { get; set; } // Ties it to the exact till session

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CustomerName { get; set; } = string.Empty; // If a loyalty customer was attached

        [Required]
        public DateTime ParkedDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public int ItemCount { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // "Active", "Recalled", or "Voided"

        // Audit Trail for Resolution
        [MaxLength(100)]
        public string ResolvedBy { get; set; } = string.Empty; // Cashier who recalled, or Admin who voided

        public DateTime? ResolvedDate { get; set; }

        // Navigation Property
        public virtual ICollection<SuspendedTransactionLine> Lines { get; set; } = new List<SuspendedTransactionLine>();
    }
}