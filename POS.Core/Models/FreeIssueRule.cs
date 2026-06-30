using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class FreeIssueRule
    {
        [Key]
        public int Id { get; set; }

        // Example: "Anchor July Supplier Promotion", "Manager Approved Shop Loss"
        [Required]
        [MaxLength(150)]
        public string RuleName { get; set; } = string.Empty;

        // ShopCost / SupplierClaim
        [Required]
        [MaxLength(30)]
        public string FreeIssueType { get; set; } = "ShopCost";

        // Optional link to a controlled reason.
        public int? FreeIssueReasonId { get; set; }

        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string ReasonName { get; set; } = string.Empty;

        // =========================================================
        // SUPPLIER RECOVERABLE SETTINGS
        // =========================================================

        public int? SupplierId { get; set; }

        [MaxLength(150)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SupplierPromotionReference { get; set; } = string.Empty;

        // Cost / Retail / Fixed
        [MaxLength(30)]
        public string ClaimValueMode { get; set; } = "Cost";

        [Column(TypeName = "decimal(18,2)")]
        public decimal FixedClaimValue { get; set; } = 0m;

        // =========================================================
        // APPLIES TO
        // =========================================================
        // All / Category / SubCategory / ItemParent / ItemVariant / Supplier
        // Keep as string because this rule engine must stay flexible.

        [Required]
        [MaxLength(30)]
        public string AppliesToType { get; set; } = "ItemVariant";

        public int? CategoryId { get; set; }

        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public int? SubCategoryId { get; set; }

        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty;

        public int? ItemParentId { get; set; }

        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        public int? ItemVariantId { get; set; }

        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // =========================================================
        // VALIDITY
        // =========================================================

        public DateTime ValidFrom { get; set; } = DateTime.Today;

        public DateTime? ValidTo { get; set; }

        public bool IsActive { get; set; } = true;

        // =========================================================
        // LIMITS
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal MaxQtyPerInvoice { get; set; } = 1m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal MaxQtyPerDay { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxValuePerInvoice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxValuePerDay { get; set; } = 0m;

        // =========================================================
        // APPROVAL / SECURITY
        // =========================================================

        public bool RequiresManagerApproval { get; set; } = true;

        public bool RequiresAdminApproval { get; set; } = false;

        public bool AllowCashierWithoutApproval { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ManagerApprovalThreshold { get; set; } = 0m;

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
        // DISPLAY HELPERS
        // =========================================================

        [NotMapped]
        public bool IsShopCost =>
            FreeIssueType.Equals("ShopCost", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsSupplierClaim =>
            FreeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCurrentlyValid =>
            IsActive &&
            ValidFrom.Date <= DateTime.Today &&
            (!ValidTo.HasValue || ValidTo.Value.Date >= DateTime.Today);

        [NotMapped]
        public string DisplayFreeIssueType =>
            IsSupplierClaim ? "Supplier Recoverable" : "Shop Cost / Loss";

        [NotMapped]
        public string DisplayAppliesTo
        {
            get
            {
                if (AppliesToType.Equals("All", StringComparison.OrdinalIgnoreCase))
                    return "All Items";

                if (AppliesToType.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(CategoryName) ? "Category" : CategoryName;

                if (AppliesToType.Equals("SubCategory", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(SubCategoryName) ? "SubCategory" : SubCategoryName;

                if (AppliesToType.Equals("ItemParent", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(ItemName) ? "Item" : ItemName;

                if (AppliesToType.Equals("ItemVariant", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(ItemName) && !string.IsNullOrWhiteSpace(SkuCode))
                        return $"{ItemName} / {SkuCode}";

                    if (!string.IsNullOrWhiteSpace(ItemName))
                        return ItemName;

                    return "Item Variant";
                }

                if (AppliesToType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(SupplierName) ? "Supplier" : SupplierName;

                return AppliesToType;
            }
        }
    }
}