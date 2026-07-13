using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FreeIssueRuleSetupViewModel : ObservableObject
    {
        private readonly FreeIssueRuleRepository _ruleRepository;
        private readonly AuthService _authService;
        private readonly Dictionary<string, List<FreeIssueLookupDto>> _targetLookups =
            new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<FreeIssueRuleSearchDto> Rules { get; } = new();
        public ObservableCollection<FreeIssueReason> Reasons { get; } = new();
        public ObservableCollection<FreeIssueLookupDto> Suppliers { get; } = new();
        public ObservableCollection<FreeIssueLookupDto> TargetOptions { get; } = new();

        public ObservableCollection<string> FilterOptions { get; } = new()
        {
            "All", "Current", "Active", "Inactive", "Expired", "ShopCost", "SupplierClaim"
        };

        public ObservableCollection<string> FreeIssueTypes { get; } = new()
        {
            FreeIssueTypeCodes.ShopCost,
            FreeIssueTypeCodes.SupplierClaim
        };

        public ObservableCollection<string> AppliesToTypes { get; } = new()
        {
            "All", "ItemVariant", "Category", "SubCategory", "ItemParent", "Supplier"
        };

        public ObservableCollection<string> ClaimValueModes { get; } = new()
        {
            "Cost", "Retail", "Fixed"
        };

        [ObservableProperty] private FreeIssueRuleSearchDto? _selectedRule;
        [ObservableProperty] private FreeIssueReason? _selectedReason;
        [ObservableProperty] private FreeIssueLookupDto? _selectedSupplier;
        [ObservableProperty] private FreeIssueLookupDto? _selectedTarget;
        [ObservableProperty] private string _selectedFilter = "Current";
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = "Ready.";
        [ObservableProperty] private string _statusColorHex = "#374151";

        [ObservableProperty] private int _ruleId;
        [ObservableProperty] private string _ruleName = string.Empty;
        [ObservableProperty] private string _freeIssueType = FreeIssueTypeCodes.ShopCost;
        [ObservableProperty] private string _reasonCode = string.Empty;
        [ObservableProperty] private string _reasonName = string.Empty;
        [ObservableProperty] private string _supplierPromotionReference = string.Empty;
        [ObservableProperty] private string _claimValueMode = "Cost";
        [ObservableProperty] private decimal _fixedClaimValue;
        [ObservableProperty] private string _appliesToType = "ItemVariant";
        [ObservableProperty] private DateTime? _validFrom = DateTime.Today;
        [ObservableProperty] private DateTime? _validTo;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private decimal _maxQtyPerInvoice = 1m;
        [ObservableProperty] private decimal _maxQtyPerDay;
        [ObservableProperty] private decimal _maxValuePerInvoice;
        [ObservableProperty] private decimal _maxValuePerDay;
        [ObservableProperty] private bool _requiresManagerApproval = true;
        [ObservableProperty] private bool _requiresAdminApproval;
        [ObservableProperty] private bool _allowCashierWithoutApproval;
        [ObservableProperty] private decimal _managerApprovalThreshold;
        [ObservableProperty] private string _remarks = string.Empty;

        public bool IsSupplierFunded => FreeIssueType == FreeIssueTypeCodes.SupplierClaim;
        public bool HasTargetSelector => !AppliesToType.Equals("All", StringComparison.OrdinalIgnoreCase);
        public string TargetLabel => AppliesToType switch
        {
            "Category" => "Category",
            "SubCategory" => "Sub-category",
            "ItemParent" => "Item",
            "Supplier" => "Supplier",
            _ => "Item / SKU"
        };

        public FreeIssueRuleSetupViewModel(
            FreeIssueRuleRepository ruleRepository,
            AuthService authService)
        {
            _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task InitializeAsync()
        {
            await RunBusyAsync(async () =>
            {
                await _ruleRepository.SeedDefaultReasonsAsync(CurrentUsername());
                await LoadLookupsAsync();
                await LoadReasonsAsync();
                NewRule();
                await RefreshCoreAsync();
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
                FreeIssueType = FreeIssueTypeCodes.Normalize(value.FreeIssueType);
        }

        partial void OnFreeIssueTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsSupplierFunded));
            _ = LoadReasonsAsync();
        }

        partial void OnAppliesToTypeChanged(string value)
        {
            LoadTargetOptions();
            OnPropertyChanged(nameof(HasTargetSelector));
            OnPropertyChanged(nameof(TargetLabel));
        }

        [RelayCommand]
        private Task RefreshAsync() => RunBusyAsync(RefreshCoreAsync);

        [RelayCommand]
        private Task SearchAsync() => RefreshAsync();

        [RelayCommand]
        private void NewRule()
        {
            SelectedRule = null;
            RuleId = 0;
            RuleName = string.Empty;
            FreeIssueType = FreeIssueTypeCodes.ShopCost;
            SelectedReason = null;
            ReasonCode = string.Empty;
            ReasonName = string.Empty;
            SelectedSupplier = null;
            SupplierPromotionReference = string.Empty;
            ClaimValueMode = "Cost";
            FixedClaimValue = 0m;
            AppliesToType = "ItemVariant";
            LoadTargetOptions();
            SelectedTarget = null;
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
            SetStatus("New Free Issue rule.", "#374151");
        }

        [RelayCommand]
        private Task SaveAsync() => RunBusyAsync(async () =>
        {
            string user = CurrentUsername();
            FreeIssueRule rule = BuildRuleFromEditor();
            FreeIssueRule savedRule = await _ruleRepository.SaveRuleAsync(rule, user);
            await RefreshCoreAsync();
            FreeIssueRuleSearchDto? saved = Rules.FirstOrDefault(row => row.Id == savedRule.Id);
            if (saved != null)
                SelectedRule = saved;
            SetStatus("Free Issue rule saved.", "#166534");
        });

        [RelayCommand]
        private Task ToggleActiveAsync() => RunBusyAsync(async () =>
        {
            if (RuleId <= 0)
                throw new InvalidOperationException("Select a saved rule first.");

            await _ruleRepository.SetRuleActiveAsync(RuleId, !IsActive, CurrentUsername());
            IsActive = !IsActive;
            await RefreshCoreAsync();
            SetStatus(IsActive ? "Rule activated." : "Rule deactivated.", "#166534");
        });

        private async Task RefreshCoreAsync()
        {
            List<FreeIssueRuleSearchDto> rows = await _ruleRepository.SearchRulesAsync(
                SelectedFilter,
                SearchText,
                take: 1000);
            Rules.Clear();
            foreach (FreeIssueRuleSearchDto row in rows)
                Rules.Add(row);
            SetStatus($"Loaded {Rules.Count} Free Issue rule(s).", "#166534");
        }

        private async Task LoadLookupsAsync()
        {
            List<FreeIssueLookupDto> suppliers = await _ruleRepository.GetSupplierLookupsAsync();
            Suppliers.Clear();
            foreach (FreeIssueLookupDto row in suppliers)
                Suppliers.Add(row);

            _targetLookups["Category"] = await _ruleRepository.GetCategoryLookupsAsync();
            _targetLookups["SubCategory"] = await _ruleRepository.GetSubCategoryLookupsAsync();
            _targetLookups["ItemParent"] = await _ruleRepository.GetItemParentLookupsAsync();
            _targetLookups["ItemVariant"] = await _ruleRepository.GetItemVariantLookupsAsync();
            _targetLookups["Supplier"] = suppliers;
            LoadTargetOptions();
        }

        private async Task LoadReasonsAsync()
        {
            List<FreeIssueReason> rows = await _ruleRepository.GetActiveReasonsAsync(FreeIssueType);
            Reasons.Clear();
            foreach (FreeIssueReason row in rows)
                Reasons.Add(row);

            if (!string.IsNullOrWhiteSpace(ReasonCode))
            {
                SelectedReason = Reasons.FirstOrDefault(reason =>
                    reason.ReasonCode.Equals(ReasonCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void LoadTargetOptions()
        {
            int? preserveId = SelectedTarget?.Id;
            TargetOptions.Clear();
            if (_targetLookups.TryGetValue(AppliesToType ?? string.Empty, out List<FreeIssueLookupDto>? rows))
            {
                foreach (FreeIssueLookupDto row in rows)
                    TargetOptions.Add(row);
            }
            SelectedTarget = preserveId.HasValue
                ? TargetOptions.FirstOrDefault(row => row.Id == preserveId.Value)
                : null;
        }

        private void LoadSelectedRuleToEditor(FreeIssueRuleSearchDto selected)
        {
            RuleId = selected.Id;
            RuleName = selected.RuleName;
            FreeIssueType = FreeIssueTypeCodes.Normalize(selected.FreeIssueType);
            ReasonCode = selected.ReasonCode;
            ReasonName = selected.ReasonName;
            SelectedReason = Reasons.FirstOrDefault(reason =>
                reason.ReasonCode.Equals(selected.ReasonCode, StringComparison.OrdinalIgnoreCase));
            SelectedSupplier = Suppliers.FirstOrDefault(row => row.Id == selected.SupplierId);
            SupplierPromotionReference = selected.SupplierPromotionReference;
            ClaimValueMode = NormalizeClaimValueMode(selected.ClaimValueMode);
            FixedClaimValue = selected.FixedClaimValue;
            AppliesToType = NormalizeAppliesToType(selected.AppliesToType);
            LoadTargetOptions();
            int? targetId = GetTargetId(selected);
            SelectedTarget = targetId.HasValue
                ? TargetOptions.FirstOrDefault(row => row.Id == targetId.Value)
                : null;
            ValidFrom = selected.ValidFrom;
            ValidTo = selected.ValidTo;
            IsActive = selected.IsActive;
            MaxQtyPerInvoice = selected.MaxQtyPerInvoice;
            MaxQtyPerDay = selected.MaxQtyPerDay;
            MaxValuePerInvoice = selected.MaxValuePerInvoice;
            MaxValuePerDay = selected.MaxValuePerDay;
            RequiresManagerApproval = selected.RequiresManagerApproval;
            RequiresAdminApproval = selected.RequiresAdminApproval;
            AllowCashierWithoutApproval = selected.AllowCashierWithoutApproval;
            ManagerApprovalThreshold = selected.ManagerApprovalThreshold;
            Remarks = selected.Remarks;
            SetStatus($"Editing: {RuleName}", "#1D4ED8");
        }

        private FreeIssueRule BuildRuleFromEditor()
        {
            string appliesTo = NormalizeAppliesToType(AppliesToType);
            if (appliesTo != "All" && SelectedTarget == null)
                throw new InvalidOperationException($"Select the {TargetLabel.ToLowerInvariant()} target.");
            if (IsSupplierFunded && SelectedSupplier == null)
                throw new InvalidOperationException("Select the funding supplier.");
            if (IsSupplierFunded && appliesTo == "Supplier" &&
                SelectedTarget != null && SelectedSupplier != null &&
                SelectedTarget.Id != SelectedSupplier.Id)
            {
                throw new InvalidOperationException(
                    "A supplier-targeted supplier-funded rule must use the same supplier as both the target and funding supplier.");
            }

            var rule = new FreeIssueRule
            {
                Id = RuleId,
                RuleName = RuleName,
                FreeIssueType = FreeIssueTypeCodes.Normalize(FreeIssueType),
                FreeIssueReasonId = SelectedReason?.Id,
                ReasonCode = ReasonCode,
                ReasonName = ReasonName,
                SupplierId = IsSupplierFunded || appliesTo == "Supplier" ? (SelectedSupplier ?? SelectedTarget)?.Id : null,
                SupplierName = IsSupplierFunded || appliesTo == "Supplier" ? (SelectedSupplier ?? SelectedTarget)?.Name ?? string.Empty : string.Empty,
                SupplierPromotionReference = IsSupplierFunded ? SupplierPromotionReference : string.Empty,
                ClaimValueMode = IsSupplierFunded ? NormalizeClaimValueMode(ClaimValueMode) : "Cost",
                FixedClaimValue = IsSupplierFunded ? FixedClaimValue : 0m,
                AppliesToType = appliesTo,
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

            ApplyTarget(rule, SelectedTarget);
            return rule;
        }

        private void ApplyTarget(FreeIssueRule rule, FreeIssueLookupDto? target)
        {
            if (target == null || rule.AppliesToType == "All")
                return;

            switch (rule.AppliesToType)
            {
                case "Category":
                    rule.CategoryId = target.Id;
                    rule.CategoryName = target.Name;
                    break;
                case "SubCategory":
                    rule.SubCategoryId = target.Id;
                    rule.SubCategoryName = target.Name;
                    break;
                case "ItemParent":
                    rule.ItemParentId = target.Id;
                    rule.ItemName = target.Name;
                    break;
                case "ItemVariant":
                    rule.ItemVariantId = target.Id;
                    rule.ItemName = target.Name;
                    rule.SkuCode = target.Code;
                    break;
                case "Supplier":
                    rule.SupplierId = target.Id;
                    rule.SupplierName = target.Name;
                    break;
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
                SetStatus(ex.Message, "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string CurrentUsername() =>
            _authService.CurrentUser?.Username?.Trim() is { Length: > 0 } username
                ? username
                : throw new InvalidOperationException("A signed-in BackOffice user is required.");

        private void SetStatus(string text, string color)
        {
            StatusText = text;
            StatusColorHex = color;
        }

        private static int? GetTargetId(FreeIssueRuleSearchDto row) => row.AppliesToType switch
        {
            "Category" => row.CategoryId,
            "SubCategory" => row.SubCategoryId,
            "ItemParent" => row.ItemParentId,
            "Supplier" => row.SupplierId,
            "ItemVariant" => row.ItemVariantId,
            _ => null
        };

        private static string NormalizeClaimValueMode(string? value) =>
            string.Equals(value, "Retail", StringComparison.OrdinalIgnoreCase) ? "Retail" :
            string.Equals(value, "Fixed", StringComparison.OrdinalIgnoreCase) ? "Fixed" : "Cost";

        private static string NormalizeAppliesToType(string? value)
        {
            string text = (value ?? string.Empty).Trim();
            return text is "All" or "Category" or "SubCategory" or "ItemParent" or "Supplier"
                ? text
                : "ItemVariant";
        }
    }
}
