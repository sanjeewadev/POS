using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Models;
using System;

namespace POS.Cashier.UI.Models
{
    public partial class CartItem : ObservableObject
    {
        // =========================================================
        // IDENTITY
        // =========================================================

        [ObservableProperty]
        private int _itemVariantId;

        // Important:
        // Final checkout must sell from this exact selected batch.
        [ObservableProperty]
        private int _itemBatchId;

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

        // Backward compatibility for old XAML column.
        // Later we can replace the cart grid column with a row number or BatchId column.
        public int ItemId => ItemBatchId;

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
        // New code should use AvailableBatchStock.
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

        // Percentage discount entered from "% Disc" button.
        // Example: 10 = 10%
        [ObservableProperty]
        private decimal _discountPercentage = 0m;

        // Fixed rupee discount entered from "Rs Disc" button.
        // Example: 100 = Rs. 100 discount.
        [ObservableProperty]
        private decimal _manualDiscountAmount = 0m;

        // None / Amount / Percent
        // This is mainly for audit/reporting.
        [ObservableProperty]
        private string _discountMode = "None";

        // True when cashier manually applied Rs Disc or % Disc.
        [ObservableProperty]
        private bool _isManualDiscount = false;

        // True when cashier changed the selling price using New Price.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PriceOverrideDisplayText))]
        private bool _isPriceOverridden = false;

        // Difference between original price and new price.
        // Example: Original Rs. 1000, New Price Rs. 850 => PriceOverrideAmount = 150.
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
        // Cashier can only apply free issue using an active BackOffice rule.

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

        public decimal CostAmount =>
            Math.Round(CostPrice * Quantity, 2);

        public decimal ProfitAmount =>
            Math.Round(LineAmount - CostAmount, 2);

        public bool IsBelowMinimumPrice =>
            MinimumPrice > 0 &&
            UnitPrice < MinimumPrice &&
            !IsFreeItem;

        public string LineKey =>
            $"{ItemVariantId}:{ItemBatchId}";

        partial void OnItemBatchIdChanged(int value)
        {
            OnPropertyChanged(nameof(ItemId));
            OnPropertyChanged(nameof(LineKey));
        }

        partial void OnItemVariantIdChanged(int value)
        {
            OnPropertyChanged(nameof(LineKey));
        }

        partial void OnBatchNoChanged(string value)
        {
            OnPropertyChanged(nameof(BatchDisplayText));
        }

        partial void OnExpiryDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(HasExpiry));
            OnPropertyChanged(nameof(ExpiryDisplayText));
        }

        partial void OnAvailableBatchStockChanged(decimal value)
        {
            OnPropertyChanged(nameof(AvailableStock));
        }

        partial void OnQuantityChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            NotifyAmountChanges();
        }

        partial void OnCostPriceChanged(decimal value)
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
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(LineAmount));
        }

        partial void OnDiscountModeChanged(string value)
        {
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(LineAmount));
        }

        partial void OnPriceOverrideAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
        }

        partial void OnOriginalUnitPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
        }

        partial void OnIsFreeItemChanged(bool value)
        {
            NotifyAmountChanges();
        }

        partial void OnMinimumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
        }

        private void NotifyAmountChanges()
        {
            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(PercentageDiscountAmount));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(LineAmount));
            OnPropertyChanged(nameof(CostAmount));
            OnPropertyChanged(nameof(ProfitAmount));
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
            OnPropertyChanged(nameof(DiscountRuleDisplayText));
            OnPropertyChanged(nameof(PriceOverrideDisplayText));
        }

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
        // Used by "Disc Rule" button.
        // This is different from manual Rs Disc / % Disc.

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
    }
}