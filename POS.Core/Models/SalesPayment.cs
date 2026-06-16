using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesPayment
    {
        [Key]
        public int Id { get; set; }

        // Links back to the main receipt
        [Required]
        public int SalesHeaderId { get; set; }

        // e.g., "Cash", "Card", "Cheque", "Credit Note", "Gift Voucher", "Customer Credit"
        [Required]
        [MaxLength(50)]
        public string PaymentType { get; set; } = string.Empty;

        // The exact mathematical amount paid via this specific method
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        // Captures: Card Last 4 Digits, Cheque Number, Credit Note ID, or Voucher Code
        [MaxLength(100)]
        public string ReferenceNo { get; set; } = string.Empty;

        // Captures: Bank Name for Cheques/Transfers, or Card Type (Visa/Mastercard)
        [MaxLength(100)]
        public string BankOrCardType { get; set; } = string.Empty;

        // Important for post-dated cheques or bank transfers
        public DateTime? PaymentDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Property
        [ForeignKey("SalesHeaderId")]
        public virtual SalesHeader? SalesHeader { get; set; }
    }
}