using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace POS.Cashier.UI.ViewModels
{
    public class FreeItemApplyResult
    {
        public int FreeIssueRuleId { get; set; }

        public string FreeIssueRuleName { get; set; } = string.Empty;

        public string FreeIssueType { get; set; } = string.Empty;

        public string FreeReasonCode { get; set; } = string.Empty;

        public string FreeReasonText { get; set; } = string.Empty;

        public int? SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string SupplierPromotionReference { get; set; } = string.Empty;

        public string SupplierClaimReferenceNo { get; set; } = string.Empty;

        public decimal ClaimValue { get; set; }

        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime? ApprovedAt { get; set; }

        public decimal OriginalUnitPrice { get; set; }

        public decimal FreeIssueCostValue { get; set; }

        public decimal FreeIssueSellingValue { get; set; }
    }

    public partial class FreeItemReasonModalViewModel : ObservableObject
    {
        private readonly FreeIssueRuleRepository _ruleRepository;

        private CartItem? _selectedCartItem;

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
        private decimal _quantity = 0m;

        [ObservableProperty]
        private decimal _costPrice = 0m;

        [ObservableProperty]
        private decimal _originalUnitPrice = 0m;

        [ObservableProperty]
        private decimal _freeIssueCostValue = 0m;

        [ObservableProperty]
        private decimal _freeIssueSellingValue = 0m;

        [ObservableProperty]
        private decimal _claimValue = 0m;

        [ObservableProperty]
        private bool _requiresManagerApproval = false;

        [ObservableProperty]
        private string _approvedBy = string.Empty;

        [ObservableProperty]
        private string _claimReferenceNo = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Select a free issue rule.";

        [ObservableProperty]
        private string _statusColorHex = "#374151";

        [ObservableProperty]
        private bool _canConfirm = false;

        public FreeItemApplyResult? Result { get; private set; }

        public Action<bool>? ActionCompleted;

        public FreeItemReasonModalViewModel(FreeIssueRuleRepository ruleRepository)
        {
            _ruleRepository = ruleRepository;
        }

        partial void OnSelectedRuleChanged(FreeIssueRule? value)
        {
            _ = ValidateSelectedRuleAsync();
        }

        public async Task InitializeAsync(CartItem selectedCartItem)
        {
            if (selectedCartItem == null)
                throw new ArgumentNullException(nameof(selectedCartItem));

            _selectedCartItem = selectedCartItem;

            Result = null;
            ApplicableRules.Clear();

            ItemDescription = selectedCartItem.Description;
            Barcode = selectedCartItem.Barcode;
            SkuCode = selectedCartItem.SkuCode;
            BatchNo = selectedCartItem.BatchNo;
            Quantity = Math.Round(selectedCartItem.Quantity, 3);
            CostPrice = Math.Round(selectedCartItem.CostPrice, 2);

            OriginalUnitPrice = selectedCartItem.UnitPrice > 0m
                ? Math.Round(selectedCartItem.UnitPrice, 2)
                : Math.Round(selectedCartItem.RetailPrice, 2);

            FreeIssueCostValue = Math.Round(CostPrice * Quantity, 2);
            FreeIssueSellingValue = Math.Round(OriginalUnitPrice * Quantity, 2);

            ApprovedBy = string.Empty;
            ClaimReferenceNo = string.Empty;
            ClaimValue = 0m;
            RequiresManagerApproval = false;
            CanConfirm = false;

            await LoadApplicableRulesAsync();
        }

        [RelayCommand]
        private async Task RefreshRulesAsync()
        {
            await LoadApplicableRulesAsync();
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (_selectedCartItem == null)
            {
                SetStatus("No selected cart item.", "#EF4444");
                return;
            }

            if (SelectedRule == null)
            {
                SetStatus("Please select a free issue rule.", "#F59E0B");
                return;
            }

            var validation = await ValidateSelectedRuleAsync();

            if (!validation)
                return;

            if (RequiresManagerApproval && string.IsNullOrWhiteSpace(ApprovedBy))
            {
                SetStatus("Manager approval name/code is required.", "#F59E0B");
                return;
            }

            string claimReference = (ClaimReferenceNo ?? string.Empty).Trim();

            if (SelectedRule.IsSupplierClaim && string.IsNullOrWhiteSpace(claimReference))
            {
                claimReference = string.IsNullOrWhiteSpace(SelectedRule.SupplierPromotionReference)
                    ? $"FI-{DateTime.Now:yyyyMMddHHmmss}"
                    : SelectedRule.SupplierPromotionReference;
            }

            Result = new FreeItemApplyResult
            {
                FreeIssueRuleId = SelectedRule.Id,
                FreeIssueRuleName = SelectedRule.RuleName,
                FreeIssueType = SelectedRule.FreeIssueType,
                FreeReasonCode = SelectedRule.ReasonCode,
                FreeReasonText = string.IsNullOrWhiteSpace(SelectedRule.ReasonName)
                    ? SelectedRule.RuleName
                    : SelectedRule.ReasonName,

                SupplierId = SelectedRule.SupplierId,
                SupplierName = SelectedRule.SupplierName,
                SupplierPromotionReference = SelectedRule.SupplierPromotionReference,
                SupplierClaimReferenceNo = claimReference,

                ClaimValue = ClaimValue,

                ApprovedBy = RequiresManagerApproval
                    ? ApprovedBy.Trim()
                    : string.Empty,

                ApprovedAt = RequiresManagerApproval
                    ? DateTime.Now
                    : null,

                OriginalUnitPrice = OriginalUnitPrice,
                FreeIssueCostValue = FreeIssueCostValue,
                FreeIssueSellingValue = FreeIssueSellingValue
            };

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        private async Task LoadApplicableRulesAsync()
        {
            if (_selectedCartItem == null)
                return;

            await RunBusyAsync(async () =>
            {
                ApplicableRules.Clear();
                SelectedRule = null;

                var rules = await _ruleRepository.GetApplicableRulesAsync(
                    itemVariantId: _selectedCartItem.ItemVariantId > 0 ? _selectedCartItem.ItemVariantId : null,
                    itemParentId: null,
                    categoryId: null,
                    subCategoryId: null,
                    supplierId: null,
                    skuCode: _selectedCartItem.SkuCode,
                    barcode: _selectedCartItem.Barcode);

                foreach (var rule in rules)
                    ApplicableRules.Add(rule);

                if (ApplicableRules.Count == 0)
                {
                    SetStatus(
                        "No active free issue rule found for this item. Manager/admin rule setup is required.",
                        "#EF4444");

                    CanConfirm = false;
                    return;
                }

                SelectedRule = ApplicableRules[0];

                SetStatus(
                    $"Found {ApplicableRules.Count} applicable free issue rule(s).",
                    "#10B981");
            });
        }

        private async Task<bool> ValidateSelectedRuleAsync()
        {
            if (_selectedCartItem == null || SelectedRule == null)
            {
                CanConfirm = false;
                return false;
            }

            try
            {
                var result = await _ruleRepository.ValidateRuleUsageAsync(
                    SelectedRule.Id,
                    Quantity,
                    OriginalUnitPrice,
                    CostPrice);

                if (!result.IsAllowed)
                {
                    ClaimValue = 0m;
                    RequiresManagerApproval = false;
                    CanConfirm = false;
                    SetStatus(result.Message, "#EF4444");
                    return false;
                }

                ClaimValue = result.ClaimValue;
                RequiresManagerApproval = result.RequiresManagerApproval;

                CanConfirm = true;

                if (RequiresManagerApproval)
                    SetStatus(result.Message, "#F59E0B");
                else
                    SetStatus(result.Message, "#10B981");

                return true;
            }
            catch (Exception ex)
            {
                ClaimValue = 0m;
                RequiresManagerApproval = false;
                CanConfirm = false;
                SetStatus(ex.Message, "#EF4444");
                return false;
            }
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
                SetStatus(ex.Message, "#EF4444");
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
    }
}