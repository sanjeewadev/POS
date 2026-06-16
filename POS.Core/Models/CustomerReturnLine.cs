using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CustomerReturnLine
    {
        [Key]
        public int Id { get; set; }

        // Mapped to the new Customer Header
        [Required]
        public int CustomerReturnHeaderId { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityReturned { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundValue { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalRefund { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReturnReason { get; set; } = string.Empty; // e.g., "Wrong Size", "Damaged"

        [Required]
        [MaxLength(50)]
        public string InventoryAction { get; set; } = string.Empty; // e.g., "Restocked", "Write-Off"

        // Navigation Property
        [ForeignKey("CustomerReturnHeaderId")]
        public virtual CustomerReturnHeader? CustomerReturnHeader { get; set; }
    }
}