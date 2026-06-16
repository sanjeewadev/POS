using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SupplierLedger
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // e.g., "GRN", "PAYMENT", "DEBIT_NOTE"
        [Required]
        [MaxLength(20)]
        public string TransactionType { get; set; } = string.Empty;

        // e.g., "GRN-20260613-0001" or "PAY-000001"
        [Required]
        [MaxLength(50)]
        public string ReferenceDocument { get; set; } = string.Empty;

        // Debt goes up (We received goods)
        public decimal ChargeAmount { get; set; } = 0m;

        // Debt goes down (We paid the supplier)
        public decimal PaymentAmount { get; set; } = 0m;

        // Snapshot of balance for easy audit trail
        public decimal BalanceAfterTransaction { get; set; } = 0m;

        // --- NEW PAYMENT TRACKING FIELDS ---
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty; // Cash, Cheque, Credit / Debit Card, Direct Bank Transfer

        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ReferenceNumber { get; set; } = string.Empty; // Cheque No, Transaction ID

        public DateTime? DueDate { get; set; }
        public bool IsPaid { get; set; } = false;

        [MaxLength(250)]
        public string Remarks { get; set; } = string.Empty;
    }
}