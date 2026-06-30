using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesPayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SalesHeaderId { get; set; }

        // Cash, Card, Cheque, GiftVoucher, CustomerCredit, CreditNote, BankTransfer
        [Required]
        [MaxLength(50)]
        public string PaymentType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } = 0m;

        [MaxLength(100)]
        public string ReferenceNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankOrCardType { get; set; } = string.Empty;

        public DateTime? PaymentDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // =========================================================
        // GIFT VOUCHER PAYMENT
        // =========================================================

        public int? GiftVoucherId { get; set; }

        [MaxLength(50)]
        public string GiftVoucherNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string GiftVoucherBarcode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiftVoucherAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiftVoucherForfeitedAmount { get; set; } = 0m;

        [ForeignKey(nameof(SalesHeaderId))]
        public virtual SalesHeader? SalesHeader { get; set; }
    }
}