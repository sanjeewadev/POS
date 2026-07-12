using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ShiftCloseSnapshot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        public Guid CloseToken { get; set; }

        [Required]
        [MaxLength(50)]
        public string ZReportNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }

        public int CompletedSaleCount { get; set; }
        public int CustomerReturnCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VatTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CardTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChequeTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiftVoucherTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomerCreditTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherTenderTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidInTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FloatInTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidOutTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FloatOutTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashRefundTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CountedCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Variance { get; set; }

        [Required]
        [MaxLength(100)]
        public string ClosedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string VarianceNote { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ShiftSessionId))]
        public virtual ShiftSession? ShiftSession { get; set; }
    }
}
