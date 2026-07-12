using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    [Index(nameof(DebitLedgerId), nameof(CreditLedgerId), IsUnique = true)]
    public class CustomerLedgerAllocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerMasterId { get; set; }

        [Required]
        public int DebitLedgerId { get; set; }

        [Required]
        public int CreditLedgerId { get; set; }

        public int? CustomerPaymentReceiptId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [ForeignKey(nameof(CustomerMasterId))]
        public virtual CustomerMaster? CustomerMaster { get; set; }

        [ForeignKey(nameof(DebitLedgerId))]
        public virtual CustomerLedger? DebitLedger { get; set; }

        [ForeignKey(nameof(CreditLedgerId))]
        public virtual CustomerLedger? CreditLedger { get; set; }

        [ForeignKey(nameof(CustomerPaymentReceiptId))]
        public virtual CustomerPaymentReceipt? CustomerPaymentReceipt { get; set; }
    }
}
