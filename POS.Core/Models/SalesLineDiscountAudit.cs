using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesLineDiscountAudit
    {
        public int Id { get; set; }

        // =========================================================
        // SALE REFERENCES
        // =========================================================

        public int SalesHeaderId { get; set; }

        public int SalesLineId { get; set; }

        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        // =========================================================
        // DISCOUNT RULE SNAPSHOT
        // =========================================================

        public int? DiscountRuleId { get; set; }

        [MaxLength(150)]
        public string DiscountRuleName { get; set; } = string.Empty;

        public int? DiscountReasonId { get; set; }

        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string ReasonName { get; set; } = string.Empty;

        // Amount / Percent
        [MaxLength(30)]
        public string DiscountType { get; set; } = string.Empty;

        // Rule value.
        // Example: 10 for 10%, or 100 for Rs. 100.
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        // =========================================================
        // LINE VALUE SNAPSHOT
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalUnitPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalAfterDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitAfterDiscount { get; set; } = 0m;

        // =========================================================
        // ITEM SNAPSHOT
        // =========================================================

        public int? ItemVariantId { get; set; }

        public int? ItemBatchId { get; set; }

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ItemDescription { get; set; } = string.Empty;

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Uom { get; set; } = "PCS";

        // =========================================================
        // APPROVAL SNAPSHOT
        // =========================================================

        public bool RequiresManagerApproval { get; set; } = false;

        public bool RequiresAdminApproval { get; set; } = false;

        [MaxLength(100)]
        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime? ApprovedAt { get; set; }

        // =========================================================
        // AUDIT
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // NAVIGATION
        // =========================================================

        public SalesHeader SalesHeader { get; set; } = null!;

        public SalesLine SalesLine { get; set; } = null!;

        public DiscountRule? DiscountRule { get; set; }

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
        public string DisplayDiscount
        {
            get
            {
                if (IsAmountDiscount)
                    return $"Rs. {DiscountValue:N2}";

                if (IsPercentDiscount)
                    return $"{DiscountValue:N2}%";

                return DiscountAmount.ToString("N2");
            }
        }

        [NotMapped]
        public string ApprovalDisplayText
        {
            get
            {
                if (RequiresAdminApproval)
                    return string.IsNullOrWhiteSpace(ApprovedBy)
                        ? "Admin approval required"
                        : $"Admin approved by {ApprovedBy}";

                if (RequiresManagerApproval)
                    return string.IsNullOrWhiteSpace(ApprovedBy)
                        ? "Manager approval required"
                        : $"Manager approved by {ApprovedBy}";

                return "No approval required";
            }
        }
    }
}