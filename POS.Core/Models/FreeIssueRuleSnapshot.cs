using POS.Core.Configuration;
using System;
using System.Text.Json;

namespace POS.Core.Models
{
    public sealed class FreeIssueRuleSnapshot
    {
        public int Version { get; set; } = 1;
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string FreeIssueType { get; set; } = string.Empty;
        public int? FreeIssueReasonId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string ReasonName { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierPromotionReference { get; set; } = string.Empty;
        public string ClaimValueMode { get; set; } = string.Empty;
        public decimal FixedClaimValue { get; set; }
        public string AppliesToType { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; } = string.Empty;
        public int? ItemParentId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int? ItemVariantId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsActive { get; set; }
        public decimal MaxQtyPerInvoice { get; set; }
        public decimal MaxQtyPerDay { get; set; }
        public decimal MaxValuePerInvoice { get; set; }
        public decimal MaxValuePerDay { get; set; }
        public bool RequiresManagerApproval { get; set; }
        public bool RequiresAdminApproval { get; set; }
        public bool AllowCashierWithoutApproval { get; set; }
        public decimal ManagerApprovalThreshold { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        public static FreeIssueRuleSnapshot FromRule(FreeIssueRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            return new FreeIssueRuleSnapshot
            {
                RuleId = rule.Id,
                RuleName = rule.RuleName,
                FreeIssueType = FreeIssueTypeCodes.Normalize(rule.FreeIssueType),
                FreeIssueReasonId = rule.FreeIssueReasonId,
                ReasonCode = rule.ReasonCode,
                ReasonName = rule.ReasonName,
                SupplierId = rule.SupplierId,
                SupplierName = rule.SupplierName,
                SupplierPromotionReference = rule.SupplierPromotionReference,
                ClaimValueMode = rule.ClaimValueMode,
                FixedClaimValue = rule.FixedClaimValue,
                AppliesToType = rule.AppliesToType,
                CategoryId = rule.CategoryId,
                CategoryName = rule.CategoryName,
                SubCategoryId = rule.SubCategoryId,
                SubCategoryName = rule.SubCategoryName,
                ItemParentId = rule.ItemParentId,
                ItemName = rule.ItemName,
                ItemVariantId = rule.ItemVariantId,
                SkuCode = rule.SkuCode,
                Barcode = rule.Barcode,
                ValidFrom = rule.ValidFrom,
                ValidTo = rule.ValidTo,
                IsActive = rule.IsActive,
                MaxQtyPerInvoice = rule.MaxQtyPerInvoice,
                MaxQtyPerDay = rule.MaxQtyPerDay,
                MaxValuePerInvoice = rule.MaxValuePerInvoice,
                MaxValuePerDay = rule.MaxValuePerDay,
                RequiresManagerApproval = rule.RequiresManagerApproval,
                RequiresAdminApproval = rule.RequiresAdminApproval,
                AllowCashierWithoutApproval = rule.AllowCashierWithoutApproval,
                ManagerApprovalThreshold = rule.ManagerApprovalThreshold,
                CreatedAt = rule.CreatedAt,
                CreatedBy = rule.CreatedBy,
                UpdatedAt = rule.UpdatedAt,
                UpdatedBy = rule.UpdatedBy,
                Remarks = rule.Remarks
            };
        }

        public string ToJson() => JsonSerializer.Serialize(this);
    }
}
