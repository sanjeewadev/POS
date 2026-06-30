using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesLine
    {
        public int Id { get; set; }

        // =========================================================
        // SALE HEADER
        // =========================================================

        public int SalesHeaderId { get; set; }

        public SalesHeader SalesHeader { get; set; } = null!;

        // =========================================================
        // EXACT ITEM + BATCH SOLD
        // =========================================================

        public int? ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        public int? ItemBatchId { get; set; }

        public ItemBatch ItemBatch { get; set; } = null!;

        // =========================================================
        // SNAPSHOT FIELDS
        // These values are copied at sale time.
        // They must not change later even if Item Master changes.
        // =========================================================

        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ItemDescription { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(20)]
        public string Uom { get; set; } = "PCS";

        // =========================================================
        // QTY / PRICE / COST
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; } = 1m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; } = 0m;

        // =========================================================
        // DISCOUNT / PRICE OVERRIDE / TOTALS
        // =========================================================

        // Percentage discount entered from "% Disc" button.
        // Example: 10 = 10%
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPercentage { get; set; } = 0m;

        // Total discount value applied to this line.
        // This includes percentage discount amount + fixed rupee discount.
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        // Fixed rupee discount entered from "Rs Disc" button.
        // Example: 100 = Rs. 100 discount.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ManualDiscountAmount { get; set; } = 0m;

        // None / Amount / Percent
        [MaxLength(30)]
        public string DiscountMode { get; set; } = "None";

        // True when cashier manually applied Rs Disc or % Disc.
        public bool IsManualDiscount { get; set; } = false;

        // True when cashier changed UnitPrice using New Price.
        public bool IsPriceOverridden { get; set; } = false;

        // Difference between original price and new price.
        // Example: Original Rs. 1000, New Price Rs. 850 => PriceOverrideAmount = 150.
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceOverrideAmount { get; set; } = 0m;

        [MaxLength(100)]
        public string PriceOverrideApprovedBy { get; set; } = string.Empty;

        public DateTime? PriceOverrideApprovedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitAmount { get; set; } = 0m;

        // =========================================================
        // RETURN / AUDIT
        // =========================================================

        public bool IsReturned { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // =========================================================
        // GIFT VOUCHER SALE LINE
        // =========================================================
        // Used when cashier sells a pre-created physical gift voucher.
        // This is a non-stock sale line, so stock/batch deduction must be skipped.

        public bool IsGiftVoucherSale { get; set; } = false;

        public int? GiftVoucherId { get; set; }

        [MaxLength(50)]
        public string GiftVoucherNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string GiftVoucherBarcode { get; set; } = string.Empty;

        // =========================================================
        // FREE ISSUE / FREE ITEM SNAPSHOT
        // =========================================================
        // Used when an item is given free under an approved BackOffice rule.
        // Supports both shop-cost free issue and supplier recoverable free issue.

        public bool IsFreeItem { get; set; } = false;

        public int? FreeIssueRuleId { get; set; }

        [MaxLength(150)]
        public string FreeIssueRuleName { get; set; } = string.Empty;

        // ShopCost / SupplierClaim
        [MaxLength(30)]
        public string FreeIssueType { get; set; } = string.Empty;

        [MaxLength(50)]
        public string FreeReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string FreeReasonText { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FreeApprovedBy { get; set; } = string.Empty;

        public DateTime? FreeApprovedAt { get; set; }

        // Original selling price before New Price override or Free Issue.
        // For normal sale without override/free issue, this may equal UnitPrice.
        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalUnitPrice { get; set; } = 0m;

        // Cost value lost by shop: CostPrice x Quantity.
        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeIssueCostValue { get; set; } = 0m;

        // Original selling value lost: OriginalUnitPrice x Quantity.
        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeIssueSellingValue { get; set; } = 0m;

        // True only when this free issue should create supplier recoverable claim.
        public bool IsSupplierRecoverable { get; set; } = false;

        public int? SupplierId { get; set; }

        [MaxLength(150)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SupplierPromotionReference { get; set; } = string.Empty;

        public int? SupplierClaimId { get; set; }

        // Pending / Submitted / Settled / Rejected / Written Off / Cancelled
        [MaxLength(30)]
        public string SupplierClaimStatus { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SupplierClaimReferenceNo { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplierClaimValue { get; set; } = 0m;

        // =========================================================
        // RULE-BASED DISCOUNT SNAPSHOT
        // =========================================================
        // Used by "Disc Rule" button.
        // These fields preserve the BackOffice discount rule/reason used at cashier.

        public bool IsRuleDiscount { get; set; } = false;

        public int? DiscountRuleId { get; set; }

        [MaxLength(150)]
        public string DiscountRuleName { get; set; } = string.Empty;

        public int? DiscountReasonId { get; set; }

        [MaxLength(50)]
        public string DiscountReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string DiscountReasonName { get; set; } = string.Empty;

        public bool DiscountRequiresManagerApproval { get; set; } = false;

        public bool DiscountRequiresAdminApproval { get; set; } = false;

        [MaxLength(100)]
        public string DiscountApprovedBy { get; set; } = string.Empty;

        public DateTime? DiscountApprovedAt { get; set; }
    }
}