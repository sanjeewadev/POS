using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FreeIssueRuleSetupViewModel : ObservableObject
    {
        private readonly FreeIssueRuleRepository _ruleRepository;

        public ObservableCollection<FreeIssueRuleSearchDto> Rules { get; } = new();

        public ObservableCollection<FreeIssueReason> Reasons { get; } = new();

        public ObservableCollection<string> FilterOptions { get; } = new()
        {
            "All",
            "Current",
            "Active",
            "Inactive",
            "Expired",
            "ShopCost",
            "SupplierClaim"
        };

        public ObservableCollection<string> FreeIssueTypes { get; } = new()
        {
            "ShopCost",
            "SupplierClaim"
        };

        public ObservableCollection<string> AppliesToTypes { get; } = new()
        {
            "All",
            "Category",
            "SubCategory",
            "ItemParent",
            "ItemVariant",
            "Supplier"
        };

        public ObservableCollection<string> ClaimValueModes { get; } = new()
        {
            "Cost",
            "Retail",
            "Fixed"
        };

        [ObservableProperty]
        private FreeIssueRuleSearchDto? _selectedRule;

        [ObservableProperty]
        private FreeIssueReason? _selectedReason;

        [ObservableProperty]
        private string _selectedFilter = "Current";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Ready.";

        [ObservableProperty]
        private string _statusColorHex = "#374151";

        // =========================================================
        // EDITOR FIELDS
        // =========================================================

        [ObservableProperty]
        private int _ruleId = 0;

        [ObservableProperty]
        private string _ruleName = string.Empty;

        [ObservableProperty]
        private string _freeIssueType = "ShopCost";

        [ObservableProperty]
        private string _reasonCode = string.Empty;

        [ObservableProperty]
        private string _reasonName = string.Empty;

        [ObservableProperty]
        private string _supplierIdInput = string.Empty;

        [ObservableProperty]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private string _supplierPromotionReference = string.Empty;

        [ObservableProperty]
        private string _claimValueMode = "Cost";

        [ObservableProperty]
        private decimal _fixedClaimValue = 0m;

        [ObservableProperty]
        private string _appliesToType = "ItemVariant";

        [ObservableProperty]
        private string _categoryIdInput = string.Empty;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private string _subCategoryIdInput = string.Empty;

        [ObservableProperty]
        private string _subCategoryName = string.Empty;

        [ObservableProperty]
        private string _itemParentIdInput = string.Empty;

        [ObservableProperty]
        private string _itemName = string.Empty;

        [ObservableProperty]
        private string _itemVariantIdInput = string.Empty;

        [ObservableProperty]
        private string _skuCode = string.Empty;

        [ObservableProperty]
        private string _barcode = string.Empty;

        [ObservableProperty]
        private DateTime? _validFrom = DateTime.Today;

        [ObservableProperty]
        private DateTime? _validTo;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private decimal _maxQtyPerInvoice = 1m;

        [ObservableProperty]
        private decimal _maxQtyPerDay = 0m;

        [ObservableProperty]
        private decimal _maxValuePerInvoice = 0m;

        [ObservableProperty]
        private decimal _maxValuePerDay = 0m;

        [ObservableProperty]
        private bool _requiresManagerApproval = true;

        [ObservableProperty]
        private bool _requiresAdminApproval = false;

        [ObservableProperty]
        private bool _allowCashierWithoutApproval = false;

        [ObservableProperty]
        private decimal _managerApprovalThreshold = 0m;

        [ObservableProperty]
        private string _remarks = string.Empty;

        public FreeIssueRuleSetupViewModel(FreeIssueRuleRepository ruleRepository)
        {
            _ruleRepository = ruleRepository;
        }

        public async Task InitializeAsync()
        {
            await RunBusyAsync(async () =>
            {
                await _ruleRepository.SeedDefaultReasonsAsync("System");
                await LoadReasonsAsync();
                NewRule();
                await RefreshAsync();
            });
        }

        partial void OnSelectedRuleChanged(FreeIssueRuleSearchDto? value)
        {
            if (value != null)
                LoadSelectedRuleToEditor(value);
        }

        partial void OnSelectedReasonChanged(FreeIssueReason? value)
        {
            if (value == null)
                return;

            ReasonCode = value.ReasonCode;
            ReasonName = value.ReasonName;

            if (!value.FreeIssueType.Equals("Both", StringComparison.OrdinalIgnoreCase))
                FreeIssueType = value.FreeIssueType;
        }

        partial void OnFreeIssueTypeChanged(string value)
        {
            _ = LoadReasonsAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await RunBusyAsync(async () =>
            {
                Rules.Clear();

                var rows = await _ruleRepository.SearchRulesAsync(
                    SelectedFilter,
                    SearchText,
                    take: 1000);

                foreach (var row in rows)
                    Rules.Add(row);

                StatusText = $"Loaded {Rules.Count} free issue rules.";
                StatusColorHex = "#10B981";
            });
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await RefreshAsync();
        }

        [RelayCommand]
        private void NewRule()
        {
            RuleId = 0;
            RuleName = string.Empty;
            FreeIssueType = "ShopCost";
            SelectedReason = null;
            ReasonCode = string.Empty;
            ReasonName = string.Empty;

            SupplierIdInput = string.Empty;
            SupplierName = string.Empty;
            SupplierPromotionReference = string.Empty;
            ClaimValueMode = "Cost";
            FixedClaimValue = 0m;

            AppliesToType = "ItemVariant";
            CategoryIdInput = string.Empty;
            CategoryName = string.Empty;
            SubCategoryIdInput = string.Empty;
            SubCategoryName = string.Empty;
            ItemParentIdInput = string.Empty;
            ItemName = string.Empty;
            ItemVariantIdInput = string.Empty;
            SkuCode = string.Empty;
            Barcode = string.Empty;

            ValidFrom = DateTime.Today;
            ValidTo = null;
            IsActive = true;

            MaxQtyPerInvoice = 1m;
            MaxQtyPerDay = 0m;
            MaxValuePerInvoice = 0m;
            MaxValuePerDay = 0m;

            RequiresManagerApproval = true;
            RequiresAdminApproval = false;
            AllowCashierWithoutApproval = false;
            ManagerApprovalThreshold = 0m;

            Remarks = string.Empty;

            StatusText = "New free issue rule.";
            StatusColorHex = "#374151";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await RunBusyAsync(async () =>
            {
                var rule = BuildRuleFromEditor();

                await _ruleRepository.SaveRuleAsync(rule, "Admin");

                StatusText = "Free issue rule saved successfully.";
                StatusColorHex = "#10B981";

                await RefreshAsync();
                NewRule();
            });
        }

        [RelayCommand]
        private async Task ToggleActiveAsync()
        {
            if (RuleId <= 0)
            {
                StatusText = "Select a saved rule before changing active status.";
                StatusColorHex = "#F59E0B";
                return;
            }

            await RunBusyAsync(async () =>
            {
                await _ruleRepository.SetRuleActiveAsync(RuleId, !IsActive, "Admin");

                IsActive = !IsActive;

                StatusText = IsActive
                    ? "Free issue rule activated."
                    : "Free issue rule deactivated.";

                StatusColorHex = "#10B981";

                await RefreshAsync();
            });
        }

        private async Task LoadReasonsAsync()
        {
            var reasons = await _ruleRepository.GetActiveReasonsAsync(FreeIssueType);

            Reasons.Clear();

            foreach (var reason in reasons)
                Reasons.Add(reason);
        }

        private void LoadSelectedRuleToEditor(FreeIssueRuleSearchDto selected)
        {
            RuleId = selected.Id;
            RuleName = selected.RuleName;
            FreeIssueType = NormalizeFreeIssueType(selected.FreeIssueType);
            ReasonCode = selected.ReasonCode;
            ReasonName = selected.ReasonName;

            SupplierIdInput = selected.SupplierId?.ToString() ?? string.Empty;
            SupplierName = selected.SupplierName;
            SupplierPromotionReference = selected.SupplierPromotionReference;
            ClaimValueMode = NormalizeClaimValueMode(selected.ClaimValueMode);
            FixedClaimValue = selected.FixedClaimValue;

            AppliesToType = NormalizeAppliesToType(selected.AppliesToType);

            ValidFrom = selected.ValidFrom;
            ValidTo = selected.ValidTo;
            IsActive = selected.IsActive;

            MaxQtyPerInvoice = selected.MaxQtyPerInvoice;
            MaxQtyPerDay = selected.MaxQtyPerDay;
            MaxValuePerInvoice = selected.MaxValuePerInvoice;
            MaxValuePerDay = selected.MaxValuePerDay;

            RequiresManagerApproval = selected.RequiresManagerApproval;
            AllowCashierWithoutApproval = selected.AllowCashierWithoutApproval;
            ManagerApprovalThreshold = selected.ManagerApprovalThreshold;
            Remarks = selected.Remarks;

            SelectedReason = Reasons.FirstOrDefault(r =>
                r.ReasonCode.Equals(ReasonCode, StringComparison.OrdinalIgnoreCase));

            StatusText = $"Editing rule: {RuleName}";
            StatusColorHex = "#2563EB";
        }

        private FreeIssueRule BuildRuleFromEditor()
        {
            return new FreeIssueRule
            {
                Id = RuleId,

                RuleName = RuleName,
                FreeIssueType = NormalizeFreeIssueType(FreeIssueType),

                ReasonCode = ReasonCode,
                ReasonName = ReasonName,
                FreeIssueReasonId = SelectedReason?.Id,

                SupplierId = ParseNullableInt(SupplierIdInput),
                SupplierName = SupplierName,
                SupplierPromotionReference = SupplierPromotionReference,
                ClaimValueMode = NormalizeClaimValueMode(ClaimValueMode),
                FixedClaimValue = FixedClaimValue,

                AppliesToType = NormalizeAppliesToType(AppliesToType),
                CategoryId = ParseNullableInt(CategoryIdInput),
                CategoryName = CategoryName,
                SubCategoryId = ParseNullableInt(SubCategoryIdInput),
                SubCategoryName = SubCategoryName,
                ItemParentId = ParseNullableInt(ItemParentIdInput),
                ItemName = ItemName,
                ItemVariantId = ParseNullableInt(ItemVariantIdInput),
                SkuCode = SkuCode,
                Barcode = Barcode,

                ValidFrom = ValidFrom ?? DateTime.Today,
                ValidTo = ValidTo,
                IsActive = IsActive,

                MaxQtyPerInvoice = MaxQtyPerInvoice,
                MaxQtyPerDay = MaxQtyPerDay,
                MaxValuePerInvoice = MaxValuePerInvoice,
                MaxValuePerDay = MaxValuePerDay,

                RequiresManagerApproval = RequiresManagerApproval,
                RequiresAdminApproval = RequiresAdminApproval,
                AllowCashierWithoutApproval = AllowCashierWithoutApproval,
                ManagerApprovalThreshold = ManagerApprovalThreshold,

                Remarks = Remarks
            };
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
                StatusText = ex.Message;
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static int? ParseNullableInt(string? value)
        {
            string text = (value ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            return int.TryParse(text, out int number) && number > 0
                ? number
                : null;
        }

        private static string NormalizeFreeIssueType(string? value)
        {
            string text = (value ?? string.Empty).Trim();

            if (text.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Supplier Recoverable", StringComparison.OrdinalIgnoreCase))
                return "SupplierClaim";

            return "ShopCost";
        }

        private static string NormalizeClaimValueMode(string? value)
        {
            string text = (value ?? string.Empty).Trim();

            if (text.Equals("Retail", StringComparison.OrdinalIgnoreCase))
                return "Retail";

            if (text.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                return "Fixed";

            return "Cost";
        }

        private static string NormalizeAppliesToType(string? value)
        {
            string text = (value ?? string.Empty).Trim();

            if (text.Equals("All", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (text.Equals("Category", StringComparison.OrdinalIgnoreCase))
                return "Category";

            if (text.Equals("SubCategory", StringComparison.OrdinalIgnoreCase))
                return "SubCategory";

            if (text.Equals("ItemParent", StringComparison.OrdinalIgnoreCase))
                return "ItemParent";

            if (text.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                return "Supplier";

            return "ItemVariant";
        }
    }
}