using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class TaxRate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TaxName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal RatePercent { get; set; } = 0m;

        public bool IsActive { get; set; } = true;

        public bool IsSystemDefault { get; set; } = false;

        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        [NotMapped]
        public bool IsDeactivated
        {
            get => !IsActive;
            set
            {
                IsActive = !value;
                DeactivatedAt = value ? DateTime.Now : null;
            }
        }

        [NotMapped]
        public string StatusText => IsActive ? "Active" : "Deactivated";

        [NotMapped]
        public string DisplayText => $"{TaxCode} - {TaxName} ({RatePercent:N2}%)";
    }
}
