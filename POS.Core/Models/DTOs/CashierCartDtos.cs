using System;
using System.Collections.Generic;
using POS.Core.Services.Tax;

namespace POS.Core.Models.DTOs
{
    public sealed class CashierCartOwnerDto
    {
        public string TerminalNo { get; init; } = string.Empty;
        public int ShiftSessionId { get; init; }
        public string CashierName { get; init; } = string.Empty;
    }

    public sealed class CashierCartSaveRequest
    {
        public Guid CartToken { get; init; }
        public CashierCartOwnerDto Owner { get; init; } = new();
        public CustomerSearchDto? Customer { get; init; }
        public bool IsWholesaleMode { get; init; }
        public decimal InvoiceDiscountAmount { get; init; }
        public decimal GrossTotal { get; init; }
        public decimal TotalDiscount { get; init; }
        public decimal NetTotal { get; init; }
        public IReadOnlyList<CashierCartLineSnapshotDto> Lines { get; init; } = Array.Empty<CashierCartLineSnapshotDto>();
    }

    public sealed class CashierCartSessionDto
    {
        public int Id { get; init; }
        public Guid CartToken { get; init; }
        public string ReferenceNo { get; init; } = string.Empty;
        public int ShiftSessionId { get; init; }
        public string TerminalNo { get; init; } = string.Empty;
        public string CashierName { get; init; } = string.Empty;
        public CustomerSearchDto? Customer { get; init; }
        public bool IsWholesaleMode { get; init; }
        public decimal InvoiceDiscountAmount { get; init; }
        public decimal GrossTotal { get; init; }
        public decimal TotalDiscount { get; init; }
        public decimal NetTotal { get; init; }
        public int ItemCount { get; init; }
        public decimal TotalQuantity { get; init; }
        public string Status { get; init; } = string.Empty;
        public int Revision { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public DateTime? HeldAtUtc { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
        public string CancellationReasonCode { get; init; } = string.Empty;
        public string CancellationReasonText { get; init; } = string.Empty;
        public int RecallCount { get; init; }
        public int? SalesHeaderId { get; init; }
        public IReadOnlyList<CashierCartLineSnapshotDto> Lines { get; init; } = Array.Empty<CashierCartLineSnapshotDto>();

        public DateTime SuspendTime => HeldAtUtc ?? UpdatedAtUtc;
        public string ReferenceName => string.IsNullOrWhiteSpace(Customer?.DisplayName)
            ? ReferenceNo
            : $"{ReferenceNo} / {Customer.DisplayName}";
        public decimal TotalValue => NetTotal;
    }

    public sealed class CashierCartLineSnapshotDto
    {
        public int LineNumber { get; set; }
        public string LineType { get; set; } = string.Empty;
        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }
        public int ItemParentId { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int PrimarySupplierId { get; set; }
        public List<int> SupplierIds { get; set; } = new();
        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Uom { get; set; } = "PCS";
        public string ItemType { get; set; } = string.Empty;
        public SalesTaxProfile TaxProfile { get; set; } = new();
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public decimal AvailableBatchStock { get; set; }
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal MaximumPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public string DiscountMode { get; set; } = "None";
        public bool IsManualDiscount { get; set; }
        public bool IsPriceOverridden { get; set; }
        public decimal PriceOverrideAmount { get; set; }
        public string PriceOverrideApprovedBy { get; set; } = string.Empty;
        public DateTime? PriceOverrideApprovedAt { get; set; }
        public bool IsFreeItem { get; set; }
        public int FreeIssueRuleId { get; set; }
        public string FreeIssueRuleName { get; set; } = string.Empty;
        public string FreeIssueType { get; set; } = string.Empty;
        public string FreeReasonCode { get; set; } = string.Empty;
        public string FreeReasonText { get; set; } = string.Empty;
        public string FreeApprovedBy { get; set; } = string.Empty;
        public DateTime? FreeApprovedAt { get; set; }
        public string FreeIssueAppliedBy { get; set; } = string.Empty;
        public DateTime? FreeIssueAppliedAt { get; set; }
        public int FreeApprovedByUserId { get; set; }
        public string FreeApprovedRole { get; set; } = string.Empty;
        public string FreeIssueRuleSnapshotJson { get; set; } = string.Empty;
        public string FreeIssueSnapshotStatus { get; set; } = string.Empty;
        public decimal OriginalUnitPrice { get; set; }
        public decimal FreeIssueCostValue { get; set; }
        public decimal FreeIssueSellingValue { get; set; }
        public bool IsSupplierRecoverable { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierPromotionReference { get; set; } = string.Empty;
        public int SupplierClaimId { get; set; }
        public string SupplierClaimStatus { get; set; } = string.Empty;
        public string SupplierClaimReferenceNo { get; set; } = string.Empty;
        public decimal SupplierClaimValue { get; set; }
        public decimal InvoiceDiscountAllocation { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TaxInclusiveAmount { get; set; }
        public bool IsGiftVoucherSale { get; set; }
        public int GiftVoucherId { get; set; }
        public string GiftVoucherNo { get; set; } = string.Empty;
        public string GiftVoucherBarcode { get; set; } = string.Empty;
        public bool IsRuleDiscount { get; set; }
        public int DiscountRuleId { get; set; }
        public string DiscountRuleName { get; set; } = string.Empty;
        public int DiscountReasonId { get; set; }
        public string DiscountReasonCode { get; set; } = string.Empty;
        public string DiscountReasonName { get; set; } = string.Empty;
        public string DiscountApprovedBy { get; set; } = string.Empty;
        public DateTime? DiscountApprovedAt { get; set; }
        public bool DiscountRequiresManagerApproval { get; set; }
        public bool DiscountRequiresAdminApproval { get; set; }
    }

    public sealed class BackOfficeCartSessionDto
    {
        public int Id { get; init; }
        public string ReferenceNo { get; init; } = string.Empty;
        public int ShiftSessionId { get; init; }
        public string TerminalNo { get; init; } = string.Empty;
        public string CashierName { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public bool IsWholesaleMode { get; init; }
        public decimal GrossTotal { get; init; }
        public decimal TotalDiscount { get; init; }
        public decimal NetTotal { get; init; }
        public int ItemCount { get; init; }
        public decimal TotalQuantity { get; init; }
        public string Status { get; init; } = string.Empty;
        public int Revision { get; init; }
        public int RecallCount { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DateTime? HeldAt { get; init; }
        public DateTime? RecalledAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime? CancelledAt { get; init; }
        public string CancellationReasonCode { get; init; } = string.Empty;
        public string CancellationReasonText { get; init; } = string.Empty;
        public int? SalesHeaderId { get; init; }
        public string CompletedInvoiceNo { get; init; } = string.Empty;
    }

    public sealed class BackOfficeCartDetailsDto
    {
        public BackOfficeCartSessionDto Session { get; init; } = new();
        public IReadOnlyList<BackOfficeCartLineDto> Lines { get; init; } = Array.Empty<BackOfficeCartLineDto>();
    }

    public sealed class BackOfficeCartLineDto
    {
        public int LineNumber { get; init; }
        public string LineType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal LineTotal { get; init; }
    }

}
