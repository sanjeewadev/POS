using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SupplierLedger
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; } = null!;

        // Optional direct GRN link.
        public int? GrnHeaderId { get; set; }

        public GrnHeader? GrnHeader { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // GRN, PAYMENT, DEBIT_NOTE, CREDIT_NOTE, OPENING_BALANCE.
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReferenceDocument { get; set; } = string.Empty;

        // Supplier debt increases.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ChargeAmount { get; set; } = 0m;

        // Supplier debt decreases.
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaymentAmount { get; set; } = 0m;

        // Snapshot after this transaction.
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfterTransaction { get; set; } = 0m;

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }

        public bool IsPaid { get; set; } = false;

        [MaxLength(250)]
        public string Remarks { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal NetMovement => ChargeAmount - PaymentAmount;
    }
}