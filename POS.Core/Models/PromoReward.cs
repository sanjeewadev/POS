using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class PromoReward
    {
        [Key]
        public int Id { get; set; }

        // Links this reward back to the master rule header
        [Required]
        public int PromoRuleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RewardType { get; set; } = string.Empty; // e.g., "Percentage (%)" or "Flat Amount (Rs.)"

        // The actual mathematical value (e.g., 10 for 10%, or 500 for Rs. 500)
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RewardValue { get; set; }

        // Navigation Property
        [ForeignKey("PromoRuleId")]
        public virtual PromoRule? PromoRule { get; set; }
    }
}