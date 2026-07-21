using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using POS.Core.Utilities;

namespace POS.Cashier.UI.ViewModels
{
    public sealed class FreeItemApplyResult
    {
        public int FreeIssueRuleId { get; set; }
        public string FreeIssueRuleName { get; set; } = string.Empty;
        public string FreeIssueType { get; set; } = string.Empty;
        public string FreeReasonCode { get; set; } = string.Empty;
        public string FreeReasonText { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierPromotionReference { get; set; } = string.Empty;
        public decimal ClaimValue { get; set; }
        public decimal FreeQuantity { get; set; }
        public bool RequiresManagerApproval { get; set; }
        public bool RequiresAdminApproval { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public int? ApprovedByUserId { get; set; }
        public string ApprovedRole { get; set; } = string.Empty;
        public DateTime? ApprovedAt { get; set; }
        public string AppliedBy { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string RuleSnapshotJson { get; set; } = string.Empty;
        public string SnapshotStatus { get; set; } = FreeIssueSnapshotStatusCodes.LegacyUnknown;
        public decimal OriginalUnitPrice { get; set; }
        public decimal FreeIssueCostValue { get; set; }
        public decimal FreeIssueSellingValue { get; set; }
    }

    public partial class FreeItemReasonModalViewModel : ObservableObject
    {
        private readonly FreeIssueRuleRepository _ruleRepository;
        private CartItem? _selectedCartItem;
        private IReadOnlyCollection<CartItem> _cartItems = Array.Empty<CartItem>();
        private string _cashierName = string.Empty;

        public ObservableCollection<FreeIssueRule> ApplicableRules { get; } = new();

        [ObservableProperty]
        private FreeIssueRule? _selectedRule;

        [ObservableProperty]
        private string _itemDescription = string.Empty;

        [ObservableProperty]
        private string _barcode = string.Empty;

        [ObservableProperty]
        private string _skuCode = string.Empty;

        [ObservableProperty]
        private string _batchNo = string.Empty;

        [ObservableProperty]
        private decimal _quantity;

        [ObservableProperty]
        private decimal _maximumQuantity;

        [ObservableProperty]
        private decimal _costPrice;

        [ObservableProperty]
        private decimal _originalUnitPrice;

        [ObservableProperty]
        private decimal _freeIssueCostValue;

        [ObservableProperty]
        private decimal _freeIssueSellingValue;

        [ObservableProperty]
        private decimal _claimValue;

        [ObservableProperty]
        private bool _requiresManagerApproval;

        [ObservableProperty]
        private bool _requiresAdminApproval;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Select a free issue rule.";

        [ObservableProperty]
        private string _statusColorHex = "#374151";

        [ObservableProperty]
        private bool _canConfirm;

        public FreeItemApplyResult? Result { get; private set; }
        public Action<bool>? ActionCompleted;

        public FreeItemReasonModalViewModel(FreeIssueRuleRepository ruleRepository)
        {
            _ruleRepository = ruleRepository;
        }

        partial void OnSelectedRuleChanged(FreeIssueRule? value) => _ = ValidateSelectedRuleAsync();
        partial void OnQuantityChanged(decimal value) => _ = ValidateSelectedRuleAsync();

        public async Task InitializeAsync(
            CartItem selectedCartItem,
            IReadOnlyCollection<CartItem> cartItems,
            string cashierName)
        {
            _selectedCartItem = selectedCartItem ?? throw new ArgumentNullException(nameof(selectedCartItem));
            _cartItems = cartItems ?? Array.Empty<CartItem>();
            _cashierName = (cashierName ?? string.Empty).Trim();

            Result = null;
            ApplicableRules.Clear();

            ItemDescription = selectedCartItem.Description;
            Barcode = selectedCartItem.Barcode;
            SkuCode = selectedCartItem.SkuCode;
            BatchNo = selectedCartItem.IsService ? "Service / No stock" : selectedCartItem.BatchNo;
            MaximumQuantity = Math.Round(selectedCartItem.Quantity, 3);
            Quantity = MaximumQuantity;
            CostPrice = Math.Round(selectedCartItem.CostPrice, 2);
            OriginalUnitPrice = selectedCartItem.UnitPrice > 0m
                ? Math.Round(selectedCartItem.UnitPrice, 2)
                : Math.Round(selectedCartItem.RetailPrice, 2);

            ResetCalculatedValues();
            await LoadApplicableRulesAsync();
        }

        [RelayCommand]
        private Task RefreshRulesAsync() => LoadApplicableRulesAsync();

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (_selectedCartItem == null || SelectedRule == null)
            {
                SetStatus("Select an applicable free issue rule.", "#B91C1C");
                return;
            }

            if (!await ValidateSelectedRuleAsync())
                return;

            DateTime appliedAt = DateTime.Now;
            Result = new FreeItemApplyResult
            {
                FreeIssueRuleId = SelectedRule.Id,
                FreeIssueRuleName = SelectedRule.RuleName,
                FreeIssueType = FreeIssueTypeCodes.Normalize(SelectedRule.FreeIssueType),
                FreeReasonCode = SelectedRule.ReasonCode,
                FreeReasonText = string.IsNullOrWhiteSpace(SelectedRule.ReasonName)
                    ? SelectedRule.RuleName
                    : SelectedRule.ReasonName,
                SupplierId = SelectedRule.SupplierId,
                SupplierName = SelectedRule.SupplierName,
                SupplierPromotionReference = SelectedRule.SupplierPromotionReference,
                ClaimValue = ClaimValue,
                FreeQuantity = Quantity,
                RequiresManagerApproval = RequiresManagerApproval,
                RequiresAdminApproval = RequiresAdminApproval,
                AppliedBy = _cashierName,
                AppliedAt = appliedAt,
                RuleSnapshotJson = FreeIssueRuleSnapshot.FromRule(SelectedRule).ToJson(),
                SnapshotStatus = FreeIssueSnapshotStatusCodes.Complete,
                OriginalUnitPrice = OriginalUnitPrice,
                FreeIssueCostValue = FreeIssueCostValue,
                FreeIssueSellingValue = FreeIssueSellingValue
            };

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel() => ActionCompleted?.Invoke(false);

        private async Task LoadApplicableRulesAsync()
        {
            if (_selectedCartItem == null)
                return;

            await RunBusyAsync(async () =>
            {
                ApplicableRules.Clear();
                SelectedRule = null;

                var rules = await _ruleRepository.GetApplicableRulesAsync(
                    itemVariantId: PositiveOrNull(_selectedCartItem.ItemVariantId),
                    itemParentId: PositiveOrNull(_selectedCartItem.ItemParentId),
                    categoryId: PositiveOrNull(_selectedCartItem.CategoryId),
                    subCategoryId: PositiveOrNull(_selectedCartItem.SubCategoryId),
                    supplierId: PositiveOrNull(_selectedCartItem.PrimarySupplierId),
                    supplierIds: _selectedCartItem.SupplierIds,
                    skuCode: _selectedCartItem.SkuCode,
                    barcode: _selectedCartItem.Barcode);

                foreach (FreeIssueRule rule in rules)
                    ApplicableRules.Add(rule);

                if (ApplicableRules.Count == 0)
                {
                    SetStatus("No active free issue rule applies to this item.", "#B91C1C");
                    CanConfirm = false;
                    return;
                }

                SelectedRule = ApplicableRules[0];
            });
        }

        private async Task<bool> ValidateSelectedRuleAsync()
        {
            if (_selectedCartItem == null || SelectedRule == null)
            {
                CanConfirm = false;
                return false;
            }

            decimal safeQuantity = Math.Round(Quantity, 3);
            if (safeQuantity <= 0m || safeQuantity > MaximumQuantity)
            {
                ResetCalculatedValues();
                SetStatus($"Free quantity must be between 0.001 and {QuantityDisplayFormatter.Format(MaximumQuantity)}.", "#B91C1C");
                return false;
            }

            try
            {
                decimal alreadyQty = _cartItems
                    .Where(c => !ReferenceEquals(c, _selectedCartItem) && c.IsFreeItem && c.FreeIssueRuleId == SelectedRule.Id)
                    .Sum(c => c.Quantity);
                decimal alreadyValue = _cartItems
                    .Where(c => !ReferenceEquals(c, _selectedCartItem) && c.IsFreeItem && c.FreeIssueRuleId == SelectedRule.Id)
                    .Sum(c => c.FreeIssueSellingValue);

                FreeIssueRuleValidationResult validation = await _ruleRepository.ValidateRuleUsageAsync(
                    SelectedRule.Id,
                    safeQuantity,
                    OriginalUnitPrice,
                    CostPrice,
                    itemVariantId: PositiveOrNull(_selectedCartItem.ItemVariantId),
                    itemParentId: PositiveOrNull(_selectedCartItem.ItemParentId),
                    categoryId: PositiveOrNull(_selectedCartItem.CategoryId),
                    subCategoryId: PositiveOrNull(_selectedCartItem.SubCategoryId),
                    supplierId: PositiveOrNull(_selectedCartItem.PrimarySupplierId),
                    supplierIds: _selectedCartItem.SupplierIds,
                    skuCode: _selectedCartItem.SkuCode,
                    barcode: _selectedCartItem.Barcode,
                    alreadyInInvoiceQty: alreadyQty,
                    alreadyInInvoiceValue: alreadyValue);

                if (!validation.IsAllowed)
                {
                    ResetCalculatedValues();
                    SetStatus(validation.Message, "#B91C1C");
                    return false;
                }

                FreeIssueCostValue = Math.Round(CostPrice * safeQuantity, 2);
                FreeIssueSellingValue = Math.Round(OriginalUnitPrice * safeQuantity, 2);
                ClaimValue = validation.ClaimValue;
                RequiresManagerApproval = validation.RequiresManagerApproval;
                RequiresAdminApproval = validation.RequiresAdminApproval;
                CanConfirm = true;
                SetStatus(validation.Message, RequiresManagerApproval || RequiresAdminApproval ? "#92400E" : "#166534");
                return true;
            }
            catch (Exception ex)
            {
                ResetCalculatedValues();
                SetStatus(ex.Message, "#B91C1C");
                return false;
            }
        }

        private void ResetCalculatedValues()
        {
            FreeIssueCostValue = 0m;
            FreeIssueSellingValue = 0m;
            ClaimValue = 0m;
            RequiresManagerApproval = false;
            RequiresAdminApproval = false;
            CanConfirm = false;
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                await action();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetStatus(string message, string color)
        {
            StatusText = message;
            StatusColorHex = color;
        }

        private static int? PositiveOrNull(int value) => value > 0 ? value : null;
    }
}
