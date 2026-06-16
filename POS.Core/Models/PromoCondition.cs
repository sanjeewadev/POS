using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class PromoCondition
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PromoRuleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string TargetFamily { get; set; } = string.Empty; // e.g., "Customer Group", "Specific Item", "Minimum Spend"

        [Required]
        [MaxLength(255)]
        public string TargetValue { get; set; } = string.Empty; // e.g., "VIP Gold" or "BARCODE-123"

        // Navigation Property
        [ForeignKey("PromoRuleId")]
        public virtual PromoRule? PromoRule { get; set; }
    }
}