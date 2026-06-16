using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SuspendedTransactionLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SuspendedTransactionId { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        // Navigation Property
        [ForeignKey("SuspendedTransactionId")]
        public virtual SuspendedTransaction? SuspendedTransaction { get; set; }
    }
}