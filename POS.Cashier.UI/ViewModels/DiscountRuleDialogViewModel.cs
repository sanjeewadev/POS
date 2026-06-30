using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public class DiscountRuleApplyResult
    {
        public int DiscountRuleId { get; set; }

        public string DiscountRuleName { get; set; } = string.Empty;

        public int? DiscountReasonId { get; set; }

        public string DiscountReasonCode { get; set; } = string.Empty;

        public string DiscountReasonName { get; set; } = string.Empty;

        // Amount / Percent
        public string DiscountType { get; set; } = "Percent";

        public decimal DiscountValue { get; set; } = 0m;

        public decimal DiscountAmount { get; set; } = 0m;

        public decimal GrossAmount { get; set; } = 0m;

        public decimal LineTotalAfterDiscount { get; set; } = 0m;

        public bool RequiresManagerApproval { get; set; } = false;

        public bool RequiresAdminApproval { get; set; } = false;

        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime? ApprovedAt { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public partial class DiscountRuleDialogViewModel : ObservableObject
    {
        private readonly DiscountRuleRepository _discountRuleRepository;

        public ObservableCollection<DiscountRule> ApplicableRules { get; } = new();

        public event Action<bool?>? RequestClose;

        public DiscountRuleDialogViewModel(DiscountRuleRepository discountRuleRepository)
        {
            _discountRuleRepository = discountRuleRepository;
        }

        // =========================================================
        // SELECTED ITEM
        // =========================================================

        [ObservableProperty]
        private CartItem? _selectedItem;

        [ObservableProperty]
        private string _selectedItemText = string.Empty;

        [ObservableProperty]
        private decimal _selectedUnitPrice = 0m;

        [ObservableProperty]
        private decimal _selectedQuantity = 0m;

        [ObservableProperty]
        private decimal _selectedGrossAmount = 0m;

        [ObservableProperty]
        private decimal _selectedMinimumPrice = 0m;

        [ObservableProperty]
        private string _customerType = "Walk-In";

        [ObservableProperty]
        private bool _isManagerModeActive = false;

        // =========================================================
        // RULE SELECTION
        // =========================================================

        [ObservableProperty]
        private DiscountRule? _selectedRule;

        [ObservableProperty]
        private string _statusMessage = "Select a discount rule.";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        [ObservableProperty]
        private decimal _calculatedDiscountAmount = 0m;

        [ObservableProperty]
        private decimal _lineTotalAfterDiscount = 0m;

        [ObservableProperty]
        private bool _approvalRequired = false;

        [ObservableProperty]
        private bool _requiresManagerApproval = false;

        [ObservableProperty]
        private bool _requiresAdminApproval = false;

        [ObservableProperty]
        private string _approvedBy = string.Empty;

        [ObservableProperty]
        private string _remarks = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private bool _hasRules = false;

        private DiscountRuleValidationResult? _currentValidationResult;

        public DiscountRuleApplyResult? Result { get; private set; }

        public bool CanConfirm =>
            !IsBusy &&
            SelectedRule != null &&
            _currentValidationResult != null &&
            _currentValidationResult.IsValid &&
            (!ApprovalRequired || !string.IsNullOrWhiteSpace(ApprovedBy));

        // =========================================================
        // INIT
        // =========================================================

        public async Task InitializeAsync(
            CartItem selectedItem,
            string customerType,
            bool isManagerModeActive,
            string approvedBy)
        {
            SelectedItem = selectedItem;
            CustomerType = string.IsNullOrWhiteSpace(customerType) ? "Walk-In" : customerType.Trim();
            IsManagerModeActive = isManagerModeActive;
            ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? string.Empty : approvedBy.Trim();

            LoadSelectedItemSnapshot();

            await RefreshRulesAsync();
        }

        private void LoadSelectedItemSnapshot()
        {
            if (SelectedItem == null)
                return;

            SelectedItemText = $"{SelectedItem.Description} / {SelectedItem.Barcode}";
            SelectedUnitPrice = SelectedItem.UnitPrice;
            SelectedQuantity = SelectedItem.Quantity;
            SelectedGrossAmount = SelectedItem.GrossAmount;
            SelectedMinimumPrice = SelectedItem.MinimumPrice;

            LineTotalAfterDiscount = SelectedItem.LineAmount;
        }

        // =========================================================
        // LOAD RULES
        // =========================================================

        [RelayCommand]
        public async Task RefreshRulesAsync()
        {
            if (SelectedItem == null)
            {
                SetStatus("No selected item.", "#EF4444");
                return;
            }

            if (SelectedItem.IsGiftVoucherSale)
            {
                SetStatus("Gift voucher sale line cannot use discount rule.", "#EF4444");
                return;
            }

            if (SelectedItem.IsFreeItem)
            {
                SetStatus("Free item line cannot use discount rule.", "#EF4444");
                return;
            }

            if (SelectedItem.IsPriceOverridden)
            {
                SetStatus("New Price line cannot use discount rule.", "#EF4444");
                return;
            }

            try
            {
                IsBusy = true;
                ApplicableRules.Clear();
                SelectedRule = null;
                _currentValidationResult = null;

                var rules = await _discountRuleRepository.GetApplicableRulesAsync(
                    SelectedItem.ItemVariantId,
                    CustomerType,
                    DateTime.Today);

                foreach (var rule in rules)
                    ApplicableRules.Add(rule);

                HasRules = ApplicableRules.Any();

                if (!HasRules)
                {
                    SetStatus("No active discount rules found for this item/customer.", "#F59E0B");
                    return;
                }

                SetStatus("Select a discount rule.", "#64748B");

                if (ApplicableRules.Count == 1)
                    SelectedRule = ApplicableRules.First();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load discount rules: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
                NotifyCanConfirmChanged();
            }
        }

        partial void OnSelectedRuleChanged(DiscountRule? value)
        {
            _ = ValidateSelectedRuleAsync();
        }

        partial void OnApprovedByChanged(string value)
        {
            NotifyCanConfirmChanged();
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private async Task ValidateSelectedRuleAsync()
        {
            if (SelectedItem == null)
                return;

            if (SelectedRule == null)
            {
                _currentValidationResult = null;
                CalculatedDiscountAmount = 0m;
                LineTotalAfterDiscount = SelectedItem.LineAmount;
                ApprovalRequired = false;
                RequiresManagerApproval = false;
                RequiresAdminApproval = false;
                SetStatus("Select a discount rule.", "#64748B");
                NotifyCanConfirmChanged();
                return;
            }

            try
            {
                IsBusy = true;

                var validation = await _discountRuleRepository.ValidateRuleUsageAsync(
                    discountRuleId: SelectedRule.Id,
                    itemVariantId: SelectedItem.ItemVariantId,
                    quantity: SelectedItem.Quantity,
                    unitPrice: SelectedItem.UnitPrice,
                    minimumPrice: SelectedItem.MinimumPrice,
                    customerType: CustomerType,
                    isGiftVoucherSale: SelectedItem.IsGiftVoucherSale,
                    isFreeItem: SelectedItem.IsFreeItem,
                    isPriceOverridden: SelectedItem.IsPriceOverridden,
                    isManagerModeActive: IsManagerModeActive,
                    saleDate: DateTime.Today);

                _currentValidationResult = validation;

                CalculatedDiscountAmount = validation.DiscountAmount;
                LineTotalAfterDiscount = validation.LineTotalAfterDiscount;

                ApprovalRequired = validation.ApprovalRequired;
                RequiresManagerApproval = validation.RequiresManagerApproval;
                RequiresAdminApproval = validation.RequiresAdminApproval;

                if (validation.IsValid)
                {
                    if (ApprovalRequired && string.IsNullOrWhiteSpace(ApprovedBy))
                        ApprovedBy = "Manager Mode";

                    SetStatus(validation.Message, "#10B981");
                }
                else
                {
                    SetStatus(validation.Message, "#EF4444");
                }
            }
            catch (Exception ex)
            {
                _currentValidationResult = null;
                SetStatus($"Rule validation failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
                NotifyCanConfirmChanged();
            }
        }

        // =========================================================
        // CONFIRM / CANCEL
        // =========================================================

        [RelayCommand]
        public async Task ConfirmAsync()
        {
            if (SelectedItem == null)
            {
                SetStatus("Selected item is missing.", "#EF4444");
                return;
            }

            if (SelectedRule == null)
            {
                SetStatus("Select a discount rule first.", "#F59E0B");
                return;
            }

            await ValidateSelectedRuleAsync();

            if (_currentValidationResult == null || !_currentValidationResult.IsValid)
            {
                SetStatus(_currentValidationResult?.Message ?? "Discount rule is not valid.", "#EF4444");
                return;
            }

            if (ApprovalRequired && string.IsNullOrWhiteSpace(ApprovedBy))
            {
                SetStatus("Approval name is required.", "#EF4444");
                return;
            }

            Result = new DiscountRuleApplyResult
            {
                DiscountRuleId = _currentValidationResult.DiscountRuleId,
                DiscountRuleName = _currentValidationResult.DiscountRuleName,

                DiscountReasonId = _currentValidationResult.DiscountReasonId,
                DiscountReasonCode = _currentValidationResult.ReasonCode,
                DiscountReasonName = _currentValidationResult.ReasonName,

                DiscountType = _currentValidationResult.DiscountType,
                DiscountValue = _currentValidationResult.DiscountValue,
                DiscountAmount = _currentValidationResult.DiscountAmount,

                GrossAmount = _currentValidationResult.GrossAmount,
                LineTotalAfterDiscount = _currentValidationResult.LineTotalAfterDiscount,

                RequiresManagerApproval = _currentValidationResult.RequiresManagerApproval,
                RequiresAdminApproval = _currentValidationResult.RequiresAdminApproval,

                ApprovedBy = ApprovalRequired
                    ? ApprovedBy.Trim()
                    : string.Empty,

                ApprovedAt = ApprovalRequired
                    ? DateTime.Now
                    : null,

                Remarks = string.IsNullOrWhiteSpace(Remarks)
                    ? string.Empty
                    : Remarks.Trim()
            };

            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        public void Cancel()
        {
            Result = null;
            RequestClose?.Invoke(false);
        }

        private void SetStatus(string message, string color)
        {
            StatusMessage = message;
            StatusColor = color;
        }

        private void NotifyCanConfirmChanged()
        {
            OnPropertyChanged(nameof(CanConfirm));
        }
    }
}