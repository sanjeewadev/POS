using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class DiscountReason
    {
        public int Id { get; set; }

        // =========================================================
        // BASIC REASON INFO
        // =========================================================

        [Required]
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string ReasonName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        // =========================================================
        // RULE CONTROL
        // =========================================================

        public bool RequiresManagerApproval { get; set; } = false;

        public bool RequiresAdminApproval { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ManagerApprovalThreshold { get; set; } = 0m;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        // =========================================================
        // AUDIT
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // NOT MAPPED HELPERS
        // =========================================================

        [NotMapped]
        public string DisplayText =>
            string.IsNullOrWhiteSpace(ReasonCode)
                ? ReasonName
                : $"{ReasonCode} - {ReasonName}";

        [NotMapped]
        public string ApprovalDisplayText
        {
            get
            {
                if (RequiresAdminApproval)
                    return "Admin Approval";

                if (RequiresManagerApproval)
                    return "Manager Approval";

                return "No Approval";
            }
        }
    }
}