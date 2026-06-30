using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class DiscountRule
    {
        public int Id { get; set; }

        // =========================================================
        // BASIC RULE INFO
        // =========================================================

        [Required]
        [MaxLength(150)]
        public string RuleName { get; set; } = string.Empty;

        // Amount / Percent
        [Required]
        [MaxLength(30)]
        public string DiscountType { get; set; } = "Percent";

        // If DiscountType = Percent, 10 means 10%.
        // If DiscountType = Amount, 100 means Rs. 100.
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } = 0m;

        public int? DiscountReasonId { get; set; }

        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string ReasonName { get; set; } = string.Empty;

        // =========================================================
        // APPLY SCOPE
        // =========================================================
        // All / Category / SubCategory / ItemParent / ItemVariant / CustomerType

        [Required]
        [MaxLength(30)]
        public string AppliesToType { get; set; } = "All";

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

        // All / Walk-In / Retail / Wholesale / Loyalty
        [MaxLength(30)]
        public string CustomerType { get; set; } = "All";

        // =========================================================
        // VALIDITY
        // =========================================================

        public DateTime? ValidFrom { get; set; }

        public DateTime? ValidTo { get; set; }

        public bool IsActive { get; set; } = true;

        // =========================================================
        // LIMITS
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDiscountAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDiscountPercent { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxValuePerInvoice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxValuePerDay { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal MaxQtyPerInvoice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal MaxQtyPerDay { get; set; } = 0m;

        // =========================================================
        // SECURITY / APPROVAL
        // =========================================================

        public bool RequiresManagerApproval { get; set; } = false;

        public bool RequiresAdminApproval { get; set; } = false;

        // If DiscountAmount > this threshold, approval is required.
        // 0 means always follow RequiresManagerApproval flag only.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ManagerApprovalThreshold { get; set; } = 0m;

        // Keep false for safety. If true, rule can sell below item MinimumPrice.
        public bool AllowBelowMinimumPrice { get; set; } = false;

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
        // NAVIGATION
        // =========================================================

        public DiscountReason? DiscountReason { get; set; }

        // =========================================================
        // NOT MAPPED HELPERS
        // =========================================================

        [NotMapped]
        public bool IsPercentDiscount =>
            DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsAmountDiscount =>
            DiscountType.Equals("Amount", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCurrentlyValid
        {
            get
            {
                if (!IsActive)
                    return false;

                DateTime today = DateTime.Today;

                if (ValidFrom.HasValue && ValidFrom.Value.Date > today)
                    return false;

                if (ValidTo.HasValue && ValidTo.Value.Date < today)
                    return false;

                return true;
            }
        }

        [NotMapped]
        public string DisplayDiscountType =>
            IsAmountDiscount ? "Amount" : "Percent";

        [NotMapped]
        public string DisplayDiscountValue
        {
            get
            {
                if (IsAmountDiscount)
                    return $"Rs. {DiscountValue:N2}";

                return $"{DiscountValue:N2}%";
            }
        }

        [NotMapped]
        public string DisplayAppliesTo
        {
            get
            {
                string type = string.IsNullOrWhiteSpace(AppliesToType)
                    ? "All"
                    : AppliesToType.Trim();

                if (type.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(CategoryName) ? "Category" : $"Category: {CategoryName}";

                if (type.Equals("SubCategory", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(SubCategoryName) ? "Sub Category" : $"Sub Category: {SubCategoryName}";

                if (type.Equals("ItemParent", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(ItemName) ? "Item" : $"Item: {ItemName}";

                if (type.Equals("ItemVariant", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(SkuCode) ? "Item Variant" : $"Variant: {SkuCode}";

                if (type.Equals("CustomerType", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(CustomerType) ? "Customer Type" : $"Customer: {CustomerType}";

                return "All Items";
            }
        }
    }
}