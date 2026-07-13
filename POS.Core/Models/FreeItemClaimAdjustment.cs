using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class FreeItemClaimAdjustment
    {
        [Key]
        public int Id { get; set; }

        public int FreeItemClaimLogId { get; set; }

        public int CustomerReturnLineId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal QuantityReturned { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimValueReduction { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Remarks { get; set; } = string.Empty;

        [ForeignKey(nameof(FreeItemClaimLogId))]
        public FreeItemClaimLog? FreeItemClaimLog { get; set; }

        [ForeignKey(nameof(CustomerReturnLineId))]
        public CustomerReturnLine? CustomerReturnLine { get; set; }
    }
}
