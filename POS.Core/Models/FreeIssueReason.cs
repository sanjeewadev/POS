using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class FreeIssueReason
    {
        [Key]
        public int Id { get; set; }

        // Example: CUSTOMER_GOODWILL, SUPPLIER_PROMO, DAMAGED_REPLACEMENT
        [Required]
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string ReasonName { get; set; } = string.Empty;

        // ShopCost / SupplierClaim / Both
        [Required]
        [MaxLength(30)]
        public string FreeIssueType { get; set; } = "ShopCost";

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public bool RequiresManagerApproval { get; set; } = true;

        public bool RequiresSupplier { get; set; } = false;

        public bool RequiresClaimReference { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        // Optional amount threshold for this reason.
        // Example: Customer Goodwill over Rs. 500 requires approval.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ManagerApprovalThreshold { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [NotMapped]
        public bool IsShopCost =>
            FreeIssueType.Equals("ShopCost", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsSupplierClaim =>
            FreeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool AppliesToBoth =>
            FreeIssueType.Equals("Both", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string DisplayFreeIssueType
        {
            get
            {
                if (IsSupplierClaim)
                    return "Supplier Recoverable";

                if (AppliesToBoth)
                    return "Both";

                return "Shop Cost / Loss";
            }
        }
    }
}