using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CashierCartSession
    {
        [Key]
        public int Id { get; set; }

        public Guid CartToken { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReferenceNo { get; set; } = string.Empty;

        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        public int? CustomerMasterId { get; set; }

        [MaxLength(30)]
        public string CustomerCodeSnapshot { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CustomerNameSnapshot { get; set; } = "Walk-In";

        [MaxLength(30)]
        public string CustomerTypeSnapshot { get; set; } = "Walk-In";

        public string CustomerSnapshotJson { get; set; } = string.Empty;

        public bool IsWholesaleMode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceDiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetTotal { get; set; }

        public int ItemCount { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalQuantity { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        public int Revision { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? HeldAtUtc { get; set; }
        public DateTime? RecalledAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string HeldBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string RecalledBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancellationReasonCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReasonText { get; set; } = string.Empty;

        public int RecallCount { get; set; }

        public int? SalesHeaderId { get; set; }

        [ForeignKey(nameof(ShiftSessionId))]
        public ShiftSession? ShiftSession { get; set; }

        [ForeignKey(nameof(CustomerMasterId))]
        public CustomerMaster? CustomerMaster { get; set; }

        [ForeignKey(nameof(SalesHeaderId))]
        public SalesHeader? SalesHeader { get; set; }

        public ICollection<CashierCartLine> Lines { get; set; } = new List<CashierCartLine>();
    }
}
