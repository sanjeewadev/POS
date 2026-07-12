using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    [Index(nameof(ReceiptNo), IsUnique = true)]
    [Index(nameof(ReceiptToken), IsUnique = true)]
    public class CustomerPaymentReceipt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiptNo { get; set; } = string.Empty;

        [Required]
        public Guid ReceiptToken { get; set; }

        [Required]
        public int CustomerMasterId { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(30)]
        public string PaymentMethod { get; set; } = "Cash";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string ReferenceNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankOrCardType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DestinationAccount { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ProcessedBy { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        public int? ShiftSessionId { get; set; }

        public int? CashMovementId { get; set; }

        [MaxLength(255)]
        public string Remarks { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(CustomerMasterId))]
        public virtual CustomerMaster? CustomerMaster { get; set; }

        [ForeignKey(nameof(ShiftSessionId))]
        public virtual ShiftSession? ShiftSession { get; set; }

        [ForeignKey(nameof(CashMovementId))]
        public virtual CashMovement? CashMovement { get; set; }

        public virtual ICollection<CustomerLedgerAllocation> Allocations { get; set; } = new List<CustomerLedgerAllocation>();
    }
}
