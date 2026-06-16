using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CustomerName { get; set; } = "Walk-In";

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // --- MATH SUMMARY ---
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossTotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetTotal { get; set; } = 0m;

        // We keep Tendered and Returned for the physical cash handling aspect of the summary
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountTendered { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceReturned { get; set; } = 0m;

        // Note: The old single "PaymentMethod" string is deprecated by the SalesPayments collection below.
        // We will keep the property for backwards compatibility during the transition, but it can be ignored.
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Split";

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Completed"; // "Completed", "Voided", "Returned"

        public bool IsVoided { get; set; } = false;

        // --- NAVIGATION PROPERTIES ---
        public virtual ICollection<SalesLine> SalesLines { get; set; } = new List<SalesLine>();

        // ADDED: The new 1-to-Many payment basket connection!
        public virtual ICollection<SalesPayment> SalesPayments { get; set; } = new List<SalesPayment>();
    }
}