using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using POS.Core.Configuration;
using POS.Core.Services.Tax;
using POS.Core.Utilities;

namespace POS.Cashier.UI.Models
{
    public partial class CartItem : ObservableObject
    {
        // =========================================================
        // IDENTITY
        // =========================================================

        [ObservableProperty]
        private int _itemVariantId;

        [ObservableProperty]
        private int _itemBatchId;

        [ObservableProperty]
        private int _itemParentId;

        [ObservableProperty]
        private int _categoryId;

        [ObservableProperty]
        private int _subCategoryId;

        [ObservableProperty]
        private int _primarySupplierId;

        public List<int> SupplierIds { get; set; } = new();

        [ObservableProperty]
        private string _itemCode = string.Empty;

        [ObservableProperty]
        private string _skuCode = string.Empty;

        [ObservableProperty]
        private string _barcode = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _variantDescription = string.Empty;

        [ObservableProperty]
        private string _uom = "PCS";

        [ObservableProperty]
        private string _itemType = ItemTypeCodes.StockItem;

        public SalesTaxProfile TaxProfile { get; set; } = new();

        public bool IsService =>
            string.Equals(
                ItemType,
                ItemTypeCodes.Service,
                StringComparison.Ordinal);

        public bool IsStockItem =>
            string.Equals(
                ItemType,
                ItemTypeCodes.StockItem,
                StringComparison.Ordinal);

        public bool RequiresStockBatch =>
            !IsGiftVoucherSale &&
            IsStockItem;

        // Backward compatibility for old XAML/code.
        public int ItemId => ItemBatchId;

        // =========================================================
        // CASHIER DISPLAY HELPERS
        // =========================================================

        public string CashierCodeDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Barcode))
                    return Barcode.Trim();

                if (!string.IsNullOrWhiteSpace(SkuCode))
                    return SkuCode.Trim();

                return ItemCode.Trim();
            }
        }

        public string CashierItemDisplay
        {
            get
            {
                if (IsGiftVoucherSale)
                    return string.IsNullOrWhiteSpace(Description)
                        ? "Gift Voucher"
                        : Description.Trim();

                return string.IsNullOrWhiteSpace(Description)
                    ? SkuCode.Trim()
                    : Description.Trim();
            }
        }

        public string SmartQuantityText => FormatQuantity(Quantity);

        public string SmartAvailableStockText => FormatQuantity(AvailableBatchStock);

        public string QuantityUomDisplay
        {
            get
            {
                string uom = string.IsNullOrWhiteSpace(Uom)
                    ? string.Empty
                    : Uom.Trim();

                return string.IsNullOrWhiteSpace(uom)
                    ? SmartQuantityText
                    : $"{SmartQuantityText} {uom}";
            }
        }

        public string CashierBatchInfoDisplay
        {
            get
            {
                if (IsGiftVoucherSale)
                {
                    string voucher = string.IsNullOrWhiteSpace(GiftVoucherNo)
                        ? GiftVoucherBarcode
                        : GiftVoucherNo;

                    return string.IsNullOrWhiteSpace(voucher)
                        ? "Gift voucher sale"
                        : $"Voucher: {voucher}";
                }

                var parts = new List<string>();

                if (IsService)
                {
                    if (!string.IsNullOrWhiteSpace(SkuCode))
                        parts.Add($"SKU: {SkuCode.Trim()}");

                    parts.Add("Service / No stock");
                    return string.Join(" | ", parts);
                }

                if (!string.IsNullOrWhiteSpace(SkuCode))
                    parts.Add($"SKU: {SkuCode.Trim()}");

                if (!string.IsNullOrWhiteSpace(BatchNo))
                    parts.Add($"Batch: {BatchNo.Trim()}");

                if (ExpiryDate.HasValue)
                    parts.Add($"Exp: {ExpiryDate.Value:yyyy-MM-dd}");

                if (AvailableBatchStock > 0m)
                    parts.Add($"Stock: {SmartAvailableStockText}");

                return parts.Count == 0
                    ? string.Empty
                    : string.Join(" | ", parts);
            }
        }

        public string DiscountDisplayText
        {
            get
            {
                if (IsGiftVoucherSale)
                    return "-";

                if (IsFreeItem)
                    return "FREE";

                if (DiscountAmount <= 0m)
                    return "-";

                if (IsRuleDiscount)
                    return $"RULE {DiscountAmount:N2}";

                if (ManualDiscountAmount > 0m ||
                    DiscountMode.Equals("Amount", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Rs. {ManualDiscountAmount:N2}";
                }

                if (DiscountPercentage > 0m ||
                    DiscountMode.Equals("Percent", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{DiscountPercentage:0.##}%";
                }

                return DiscountAmount.ToString("N2");
            }
        }

        public string CashierLineStatusDisplay
        {
            get
            {
                var parts = new List<string>();

                if (IsGiftVoucherSale)
                    parts.Add("Gift Voucher");

                if (IsFreeItem)
                    parts.Add($"Free: {FreeIssueDisplayText}");

                if (IsRuleDiscount)
                    parts.Add($"Rule: {DiscountRuleDisplayText}");

                if (IsPriceOverridden)
                    parts.Add(PriceOverrideDisplayText);

                if (IsBelowMinimumPrice)
                    parts.Add("Below Min Price");

                return parts.Count == 0
                    ? string.Empty
                    : string.Join(" | ", parts);
            }
        }

        public bool HasCashierLineStatus =>
            !string.IsNullOrWhiteSpace(CashierLineStatusDisplay);

        // =========================================================
        // BATCH SNAPSHOT
        // =========================================================

        [ObservableProperty]
        private string _batchNo = string.Empty;

        [ObservableProperty]
        private DateTime? _expiryDate;

        [ObservableProperty]
        private DateTime? _receivedDate;

        [ObservableProperty]
        private decimal _availableBatchStock = 0m;

        // Backward compatibility with old SalesViewModel code.
        public decimal AvailableStock
        {
            get => AvailableBatchStock;
            set
            {
                if (AvailableBatchStock != value)
                {
                    AvailableBatchStock = value;
                    OnPropertyChanged(nameof(AvailableStock));
                }
            }
        }

        public bool HasExpiry => ExpiryDate.HasValue;

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo;

        public string ExpiryDisplayText =>
            ExpiryDate.HasValue
                ? ExpiryDate.Value.ToString("yyyy-MM-dd")
                : "-";

        // =========================================================
        // PRICE SNAPSHOT
        // =========================================================

        [ObservableProperty]
        private decimal _costPrice = 0m;

        [ObservableProperty]
        private decimal _retailPrice = 0m;

        [ObservableProperty]
        private decimal _wholesalePrice = 0m;

        [ObservableProperty]
        private decimal _minimumPrice = 0m;

        [ObservableProperty]
        private decimal _maximumPrice = 0m;

        [ObservableProperty]
        private decimal _unitPrice = 0m;

        // =========================================================
        // QUANTITY / DISCOUNT / PRICE OVERRIDE
        // =========================================================

        [ObservableProperty]
        private decimal _quantity = 1m;

        [ObservableProperty]
        private decimal _discountPercentage = 0m;

        [ObservableProperty]
        private decimal _manualDiscountAmount = 0m;

        // None / Amount / Percent / Rule
        [ObservableProperty]
        private string _discountMode = "None";

        [ObservableProperty]
        private bool _isManualDiscount = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PriceOverrideDisplayText))]
        private bool _isPriceOverridden = false;

        [ObservableProperty]
        private decimal _priceOverrideAmount = 0m;

        [ObservableProperty]
        private string _priceOverrideApprovedBy = string.Empty;

        [ObservableProperty]
        private DateTime? _priceOverrideApprovedAt;

        public string PriceOverrideDisplayText
        {
            get
            {
                if (!IsPriceOverridden)
                    return string.Empty;

                return PriceOverrideAmount > 0m
                    ? $"New Price / Reduced Rs. {PriceOverrideAmount:N2}"
                    : "New Price";
            }
        }

        // =========================================================
        // FREE ISSUE / FREE ITEM
        // =========================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsShopCostFreeItem))]
        [NotifyPropertyChangedFor(nameof(IsSupplierClaimFreeItem))]
        [NotifyPropertyChangedFor(nameof(FreeIssueTypeDisplay))]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private bool _isFreeItem = false;

        [ObservableProperty]
        private int _freeIssueRuleId = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private string _freeIssueRuleName = string.Empty;

        // ShopCost / SupplierClaim
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsShopCostFreeItem))]
        [NotifyPropertyChangedFor(nameof(IsSupplierClaimFreeItem))]
        [NotifyPropertyChangedFor(nameof(FreeIssueTypeDisplay))]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private string _freeIssueType = string.Empty;

        [ObservableProperty]
        private string _freeReasonCode = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private string _freeReasonText = string.Empty;

        [ObservableProperty]
        private string _freeApprovedBy = string.Empty;

        [ObservableProperty]
        private DateTime? _freeApprovedAt;

        [ObservableProperty]
        private string _freeIssueAppliedBy = string.Empty;

        [ObservableProperty]
        private DateTime? _freeIssueAppliedAt;

        [ObservableProperty]
        private int _freeApprovedByUserId;

        [ObservableProperty]
        private string _freeApprovedRole = string.Empty;

        [ObservableProperty]
        private string _freeIssueRuleSnapshotJson = string.Empty;

        [ObservableProperty]
        private string _freeIssueSnapshotStatus = FreeIssueSnapshotStatusCodes.LegacyUnknown;

        [ObservableProperty]
        private decimal _originalUnitPrice = 0m;

        [ObservableProperty]
        private decimal _freeIssueCostValue = 0m;

        [ObservableProperty]
        private decimal _freeIssueSellingValue = 0m;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSupplierClaimFreeItem))]
        [NotifyPropertyChangedFor(nameof(FreeIssueTypeDisplay))]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private bool _isSupplierRecoverable = false;

        [ObservableProperty]
        private int _supplierId = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FreeIssueDisplayText))]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private string _supplierPromotionReference = string.Empty;

        [ObservableProperty]
        private int _supplierClaimId = 0;

        [ObservableProperty]
        private string _supplierClaimStatus = string.Empty;

        [ObservableProperty]
        private string _supplierClaimReferenceNo = string.Empty;

        [ObservableProperty]
        private decimal _supplierClaimValue = 0m;

        public bool IsShopCostFreeItem =>
            IsFreeItem &&
            FreeIssueType.Equals("ShopCost", StringComparison.OrdinalIgnoreCase);

        public bool IsSupplierClaimFreeItem =>
            IsFreeItem &&
            (
                IsSupplierRecoverable ||
                FreeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase)
            );

        public string FreeIssueTypeDisplay
        {
            get
            {
                if (!IsFreeItem)
                    return string.Empty;

                if (IsSupplierClaimFreeItem)
                    return "Supplier Recoverable";

                return "Shop Cost / Loss";
            }
        }

        public string FreeIssueDisplayText
        {
            get
            {
                if (!IsFreeItem)
                    return string.Empty;

                string reason = string.IsNullOrWhiteSpace(FreeReasonText)
                    ? "Free Item"
                    : FreeReasonText.Trim();

                if (IsSupplierClaimFreeItem)
                {
                    string supplier = string.IsNullOrWhiteSpace(SupplierName)
                        ? "Supplier Claim"
                        : SupplierName.Trim();

                    return $"{reason} / {supplier}";
                }

                return reason;
            }
        }

        // =========================================================
        // CALCULATED VALUES
        // =========================================================

        public decimal GrossAmount =>
            Math.Round(UnitPrice * Quantity, 2);

        public decimal PercentageDiscountAmount =>
            Math.Round(GrossAmount * (DiscountPercentage / 100m), 2);

        public decimal DiscountAmount
        {
            get
            {
                decimal discount = PercentageDiscountAmount + ManualDiscountAmount;

                if (discount < 0)
                    discount = 0m;

                if (discount > GrossAmount)
                    discount = GrossAmount;

                return Math.Round(discount, 2);
            }
        }

        public decimal LineAmount =>
            Math.Round(Math.Max(0m, GrossAmount - DiscountAmount), 2);

        [ObservableProperty]
        private decimal _invoiceDiscountAllocation = 0m;

        [ObservableProperty]
        private decimal _taxableAmount = 0m;

        [ObservableProperty]
        private decimal _vatAmount = 0m;

        [ObservableProperty]
        private decimal _taxInclusiveAmount = 0m;

        public decimal FinalDiscountAmount =>
            Math.Round(
                DiscountAmount + InvoiceDiscountAllocation,
                2);

        public decimal FinalLineAmount =>
            IsGiftVoucherSale || IsFreeItem
                ? LineAmount
                : Math.Round(TaxInclusiveAmount, 2);

        public decimal CostAmount =>
            Math.Round(CostPrice * Quantity, 2);

        public decimal ProfitAmount =>
            Math.Round(LineAmount - CostAmount, 2);

        public decimal FinalProfitAmount =>
            Math.Round(FinalLineAmount - CostAmount, 2);

        public bool IsBelowMinimumPrice =>
            MinimumPrice > 0 &&
            UnitPrice < MinimumPrice &&
            !IsFreeItem;

        public string LineKey =>
            $"{ItemVariantId}:{ItemBatchId}";

        // =========================================================
        // GIFT VOUCHER SALE LINE
        // =========================================================

        [ObservableProperty]
        private bool _isGiftVoucherSale = false;

        [ObservableProperty]
        private int _giftVoucherId = 0;

        [ObservableProperty]
        private string _giftVoucherNo = string.Empty;

        [ObservableProperty]
        private string _giftVoucherBarcode = string.Empty;

        // =========================================================
        // RULE-BASED DISCOUNT
        // =========================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountRuleDisplayText))]
        private bool _isRuleDiscount = false;

        [ObservableProperty]
        private int _discountRuleId = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountRuleDisplayText))]
        private string _discountRuleName = string.Empty;

        [ObservableProperty]
        private int _discountReasonId = 0;

        [ObservableProperty]
        private string _discountReasonCode = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountRuleDisplayText))]
        private string _discountReasonName = string.Empty;

        [ObservableProperty]
        private string _discountApprovedBy = string.Empty;

        [ObservableProperty]
        private DateTime? _discountApprovedAt;

        [ObservableProperty]
        private bool _discountRequiresManagerApproval = false;

        [ObservableProperty]
        private bool _discountRequiresAdminApproval = false;

        public string DiscountRuleDisplayText
        {
            get
            {
                if (!IsRuleDiscount)
                    return string.Empty;

                if (!string.IsNullOrWhiteSpace(DiscountRuleName) &&
                    !string.IsNullOrWhiteSpace(DiscountReasonName))
                {
                    return $"{DiscountRuleName} / {DiscountReasonName}";
                }

                if (!string.IsNullOrWhiteSpace(DiscountRuleName))
                    return DiscountRuleName;

                if (!string.IsNullOrWhiteSpace(DiscountReasonName))
                    return DiscountReasonName;

                return "Rule Discount";
            }
        }

        // =========================================================
        // PROPERTY CHANGE HELPERS
        // =========================================================

        partial void OnItemBatchIdChanged(int value)
        {
            OnPropertyChanged(nameof(ItemId));
            OnPropertyChanged(nameof(LineKey));
        }

        partial void OnItemVariantIdChanged(int value)
        {
            OnPropertyChanged(nameof(LineKey));
        }

        partial void OnItemCodeChanged(string value)
        {
            NotifyCashierIdentityDisplayChanges();
        }

        partial void OnSkuCodeChanged(string value)
        {
            NotifyCashierIdentityDisplayChanges();
        }

        partial void OnBarcodeChanged(string value)
        {
            NotifyCashierIdentityDisplayChanges();
        }

        partial void OnDescriptionChanged(string value)
        {
            NotifyCashierIdentityDisplayChanges();
        }

        partial void OnVariantDescriptionChanged(string value)
        {
            NotifyCashierIdentityDisplayChanges();
        }

        partial void OnUomChanged(string value)
        {
            OnPropertyChanged(nameof(QuantityUomDisplay));
        }

        partial void OnItemTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsService));
            OnPropertyChanged(nameof(IsStockItem));
            OnPropertyChanged(nameof(RequiresStockBatch));
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
            OnPropertyChanged(nameof(CashierLineStatusDisplay));
            OnPropertyChanged(nameof(HasCashierLineStatus));
        }

        partial void OnBatchNoChanged(string value)
        {
            OnPropertyChanged(nameof(BatchDisplayText));
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnExpiryDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(HasExpiry));
            OnPropertyChanged(nameof(ExpiryDisplayText));
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnReceivedDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnAvailableBatchStockChanged(decimal value)
        {
            OnPropertyChanged(nameof(AvailableStock));
            OnPropertyChanged(nameof(SmartAvailableStockText));
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnQuantityChanged(decimal value)
        {
            NotifyAmountChanges();
            OnPropertyChanged(nameof(SmartQuantityText));
            OnPropertyChanged(nameof(QuantityUomDisplay));
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnCostPriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnRetailPriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnWholesalePriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnMinimumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
            OnPropertyChanged(nameof(CashierLineStatusDisplay));
            OnPropertyChanged(nameof(HasCashierLineStatus));
        }

        partial void OnMaximumPriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnDiscountPercentageChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnManualDiscountAmountChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnIsManualDiscountChanged(bool value)
        {
            NotifyAmountChanges();
        }

        partial void OnDiscountModeChanged(string value)
        {
            NotifyAmountChanges();
        }

        partial void OnIsPriceOverriddenChanged(bool value)
        {
            NotifyAmountChanges();
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnPriceOverrideAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnOriginalUnitPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnIsFreeItemChanged(bool value)
        {
            NotifyAmountChanges();
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnFreeIssueRuleNameChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnFreeIssueTypeChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnFreeReasonTextChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnIsSupplierRecoverableChanged(bool value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnSupplierNameChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnIsGiftVoucherSaleChanged(bool value)
        {
            NotifyCashierIdentityDisplayChanges();
            NotifyCashierStatusDisplayChanges();
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
            OnPropertyChanged(nameof(DiscountDisplayText));
            OnPropertyChanged(nameof(RequiresStockBatch));
        }

        partial void OnGiftVoucherNoChanged(string value)
        {
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnGiftVoucherBarcodeChanged(string value)
        {
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        partial void OnIsRuleDiscountChanged(bool value)
        {
            NotifyAmountChanges();
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnDiscountRuleNameChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnDiscountReasonNameChanged(string value)
        {
            NotifyCashierStatusDisplayChanges();
        }

        partial void OnInvoiceDiscountAllocationChanged(decimal value)
        {
            OnPropertyChanged(nameof(FinalDiscountAmount));
            OnPropertyChanged(nameof(FinalLineAmount));
            OnPropertyChanged(nameof(FinalProfitAmount));
        }

        partial void OnTaxInclusiveAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(FinalLineAmount));
            OnPropertyChanged(nameof(FinalProfitAmount));
        }

        private void NotifyAmountChanges()
        {
            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(PercentageDiscountAmount));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(FinalDiscountAmount));
            OnPropertyChanged(nameof(LineAmount));
            OnPropertyChanged(nameof(FinalLineAmount));
            OnPropertyChanged(nameof(CostAmount));
            OnPropertyChanged(nameof(ProfitAmount));
            OnPropertyChanged(nameof(FinalProfitAmount));
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
            OnPropertyChanged(nameof(DiscountDisplayText));
            OnPropertyChanged(nameof(DiscountRuleDisplayText));
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
            OnPropertyChanged(nameof(CashierLineStatusDisplay));
            OnPropertyChanged(nameof(HasCashierLineStatus));
        }

        private void NotifyCashierIdentityDisplayChanges()
        {
            OnPropertyChanged(nameof(CashierCodeDisplay));
            OnPropertyChanged(nameof(CashierItemDisplay));
            OnPropertyChanged(nameof(CashierBatchInfoDisplay));
        }

        private void NotifyCashierStatusDisplayChanges()
        {
            OnPropertyChanged(nameof(FreeIssueDisplayText));
            OnPropertyChanged(nameof(FreeIssueTypeDisplay));
            OnPropertyChanged(nameof(DiscountRuleDisplayText));
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
            OnPropertyChanged(nameof(DiscountDisplayText));
            OnPropertyChanged(nameof(CashierLineStatusDisplay));
            OnPropertyChanged(nameof(HasCashierLineStatus));
        }

        private static string FormatQuantity(decimal value)
        {
            return QuantityDisplayFormatter.Format(value);
        }
    }
}