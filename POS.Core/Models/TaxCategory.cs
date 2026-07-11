using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class TaxCategory
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string CategoryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string TreatmentType { get; set; } = string.Empty;

        public bool IsRateBased { get; set; }

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        public ICollection<ItemParent> Items { get; set; } = new List<ItemParent>();

        public ICollection<TaxRate> TaxRates { get; set; } = new List<TaxRate>();
    }
}
