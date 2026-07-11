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

        public int? TaxCategoryId { get; set; }

        public TaxCategory? TaxCategory { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal RatePercent { get; set; } = 0m;

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        [MaxLength(250)]
        public string? ChangeReason { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

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
        public string PeriodStatusText
        {
            get
            {
                if (!IsActive)
                    return "Inactive";

                if (!EffectiveFrom.HasValue)
                    return "Date Required";

                DateTime today = DateTime.Today;

                if (EffectiveFrom.Value.Date > today)
                    return "Future";

                if (EffectiveTo.HasValue && EffectiveTo.Value.Date < today)
                    return "Ended";

                return "Current";
            }
        }

        [NotMapped]
        public string CategoryDisplayText =>
            TaxCategory?.CategoryName ?? "Legacy / Unclassified";

        [NotMapped]
        public string DisplayText => $"{TaxCode} - {TaxName} ({RatePercent:N2}%)";
    }
}
