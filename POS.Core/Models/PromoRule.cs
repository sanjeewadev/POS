using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class PromoRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RuleName { get; set; } = string.Empty;

        // 1 = Highest Priority (Overrides lower priorities during conflicts)
        [Required]
        public int Priority { get; set; }

        [Required]
        [MaxLength(50)]
        public string FamilyType { get; set; } = string.Empty; // e.g., "Customer-Centric", "Product-Centric"

        // Nullable dates mean the rule runs forever (until deactivated)
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        // If true, this discount adds on top of others. Use with extreme caution!
        public bool IsStackable { get; set; } = false;

        // Navigation Properties
        public virtual ICollection<PromoCondition> Conditions { get; set; } = new List<PromoCondition>();
        public virtual ICollection<PromoReward> Rewards { get; set; } = new List<PromoReward>();
    }
}