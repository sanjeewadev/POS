using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CashierCartLine
    {
        [Key]
        public int Id { get; set; }

        public int CashierCartSessionId { get; set; }

        public int LineNumber { get; set; }

        [Required]
        [MaxLength(30)]
        public string LineType { get; set; } = string.Empty;

        public int? ItemVariantId { get; set; }
        public int? ItemBatchId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Required]
        public string SnapshotJson { get; set; } = string.Empty;

        [ForeignKey(nameof(CashierCartSessionId))]
        public CashierCartSession? CashierCartSession { get; set; }
    }
}
