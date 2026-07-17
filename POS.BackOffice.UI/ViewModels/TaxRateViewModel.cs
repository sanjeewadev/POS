using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public sealed class TaxCategoryOption
    {
        public int? Id { get; init; }
        public string CategoryCode { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string TreatmentType { get; init; } = string.Empty;
        public bool IsRateBased { get; init; }
        public bool IsActive { get; init; }
        public int DisplayOrder { get; init; }
        public bool IsLegacy { get; init; }

        public string RateModeText =>
            IsLegacy
                ? "Compatibility"
                : IsRateBased
                    ? "Effective rate"
                    : "Fixed 0% treatment";

        public string StatusText => IsActive ? "Active" : "Inactive";

        public static TaxCategoryOption FromCategory(TaxCategory category)
        {
            return new TaxCategoryOption
            {
                Id = category.Id,
                CategoryCode = category.CategoryCode,
                CategoryName = category.CategoryName,
                TreatmentType = category.TreatmentType,
                IsRateBased = category.IsRateBased,
                IsActive = category.IsActive,
                DisplayOrder = category.DisplayOrder,
                IsLegacy = false
            };
        }

        public static TaxCategoryOption Legacy()
        {
            return new TaxCategoryOption
            {
                Id = null,
                CategoryCode = "LEGACY",
                CategoryName = "Legacy / Unclassified",
                TreatmentType = "CompatibilityOnly",
                IsRateBased = false,
                IsActive = true,
                DisplayOrder = 999,
                IsLegacy = true
            };
        }
    }

    public partial class TaxRateViewModel : ObservableObject
    {
        private readonly TaxRateRepository _taxRateRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly AuthService _authService;

        private bool _isInitialized;
        private bool _isApplyingCategory;
        private bool _isApplyingRate;
        private bool _isClearingForm;

        public ObservableCollection<TaxCategoryOption> TaxCategories { get; } = new();
        public ObservableCollection<TaxRate> TaxRates { get; } = new();

        [ObservableProperty]
        private TaxCategoryOption? _selectedTaxCategory;

        [ObservableProperty]
        private TaxRate? _selectedTaxRate;

        [ObservableProperty]
        private string _taxCodeInput = string.Empty;

        [ObservableProperty]
        private string _ratePercentText = "18";

        [ObservableProperty]
        private DateTime? _effectiveFromInput = DateTime.Today;

        [ObservableProperty]
        private bool _hasEffectiveToInput;

        [ObservableProperty]
        private DateTime? _effectiveToInput;

        [ObservableProperty]
        private string _changeReasonInput = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _includeDeactivated = true;

        [ObservableProperty]
        private bool _selectedRateHasUsage;

        [ObservableProperty]
        private string _usageSummary = "Select a rate version to view its usage.";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Loading Tax Master...";

        public bool IsExistingRate => SelectedTaxRate != null;

        public bool IsLegacyCategorySelected =>
            SelectedTaxCategory?.IsLegacy == true;

        public bool IsRateBasedCategorySelected =>
            SelectedTaxCategory?.IsLegacy == false &&
            SelectedTaxCategory?.IsRateBased == true;

        public bool IsFixedTreatmentCategorySelected =>
            SelectedTaxCategory != null &&
            !SelectedTaxCategory.IsLegacy &&
            !SelectedTaxCategory.IsRateBased;

        public bool IsRateEditorEnabled =>
            IsRateBasedCategorySelected;

        public bool CanEditCoreRateFields =>
            IsRateBasedCategorySelected &&
            (SelectedTaxRate == null || !SelectedRateHasUsage);

        public bool IsCoreRateReadOnly =>
            !CanEditCoreRateFields;

        public bool CanEditEffectiveTo =>
            IsRateBasedCategorySelected &&
            HasEffectiveToInput;

        public string SelectedCategoryCode =>
            SelectedTaxCategory?.CategoryCode ?? string.Empty;

        public string SelectedCategoryName =>
            SelectedTaxCategory?.CategoryName ?? "No category selected";

        public string EditorTitle =>
            IsLegacyCategorySelected
                ? "2. LEGACY RATE REVIEW"
                : IsRateBasedCategorySelected
                    ? "2. STANDARD VAT RATE VERSION"
                    : "2. TAX TREATMENT";

        public string CategoryGuidance
        {
            get
            {
                if (SelectedTaxCategory == null)
                    return "Select a tax category.";

                if (IsLegacyCategorySelected)
                {
                    return "Legacy records are preserved only for compatibility with existing Item Master and purchasing data. " +
                           "They are not authoritative tax categories. Reclassification will be completed during the Item Master rebuild.";
                }

                return SelectedTaxCategory.CategoryCode switch
                {
                    TaxCategoryCodes.Standard =>
                        "Standard VAT uses effective-dated percentage versions. Active periods must not overlap.",

                    TaxCategoryCodes.ZeroRated =>
                        "Zero Rated is a taxable 0% treatment. It does not need an editable percentage record.",

                    TaxCategoryCodes.Exempt =>
                        "Exempt is legally distinct from Zero Rated. It does not need an editable percentage record.",

                    TaxCategoryCodes.OutOfScope =>
                        "Out of Scope is outside VAT and does not need an editable percentage record.",

                    _ => "This is a fixed tax treatment."
                };
            }
        }

        public string SaveButtonText =>
            SelectedTaxRate == null
                ? "SAVE NEW VERSION"
                : "SAVE CHANGES";

        public string DeactivateReactivateButtonText =>
            SelectedTaxRate?.IsActive == false
                ? "REACTIVATE"
                : "DEACTIVATE";

        public TaxRateViewModel(
            TaxRateRepository taxRateRepository,
            IMessageBoxService messageBoxService,
            AuthService authService)
        {
            _taxRateRepository = taxRateRepository ?? throw new ArgumentNullException(nameof(taxRateRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _ = InitializeAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;

            try
            {
                await _taxRateRepository.EnsureDefaultsAsync();
                await LoadCategoriesInternalAsync();
                await LoadRatesInternalAsync(selectCurrentRate: true);

                _isInitialized = true;
                StatusMessage = "Tax Category and Rate Management loaded.";
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                StatusMessage = "Failed to initialize Tax Master.";

                _messageBoxService.ShowError(
                    $"Failed to initialize Tax Master:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCategoriesInternalAsync()
        {
            string previousCode = SelectedTaxCategory?.CategoryCode ?? TaxCategoryCodes.Standard;

            var categories = await _taxRateRepository.GetApprovedCategoriesAsync();

            _isApplyingCategory = true;

            try
            {
                TaxCategories.Clear();

                foreach (var category in categories)
                    TaxCategories.Add(TaxCategoryOption.FromCategory(category));

                TaxCategories.Add(TaxCategoryOption.Legacy());

                SelectedTaxCategory = TaxCategories.FirstOrDefault(c =>
                    string.Equals(c.CategoryCode, previousCode, StringComparison.OrdinalIgnoreCase))
                    ?? TaxCategories.FirstOrDefault(c => c.CategoryCode == TaxCategoryCodes.Standard)
                    ?? TaxCategories.FirstOrDefault();
            }
            finally
            {
                _isApplyingCategory = false;
            }

            RaiseCategoryState();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshAsync()
        {
            IsBusy = true;

            try
            {
                int? selectedRateId = SelectedTaxRate?.Id;

                await LoadCategoriesInternalAsync();
                await LoadRatesInternalAsync(
                    selectCurrentRate: selectedRateId == null,
                    preferredRateId: selectedRateId);

                StatusMessage = $"{TaxRates.Count} rate record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Refresh failed.";

                _messageBoxService.ShowError(
                    $"Failed to refresh Tax Master:\n\n{ex.Message}",
                    "Refresh Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchRefreshAsync()
        {
            IsBusy = true;

            try
            {
                await LoadRatesInternalAsync(selectCurrentRate: true);
                StatusMessage = $"{TaxRates.Count} matching rate record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Search failed.";

                _messageBoxService.ShowError(
                    $"Failed to search tax rates:\n\n{ex.Message}",
                    "Search Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearSearchAsync()
        {
            SearchText = string.Empty;
            await SearchRefreshAsync();
        }

        private async Task LoadRatesInternalAsync(
            bool selectCurrentRate,
            int? preferredRateId = null)
        {
            if (SelectedTaxCategory == null)
            {
                _isApplyingRate = true;

                try
                {
                    TaxRates.Clear();
                    SelectedTaxRate = null;
                }
                finally
                {
                    _isApplyingRate = false;
                }

                ClearEditorForCategory();
                return;
            }

            var rates = await _taxRateRepository.GetAllAsync(
                SearchText,
                IncludeDeactivated,
                SelectedTaxCategory.Id,
                SelectedTaxCategory.IsLegacy);

            TaxRate? rateToSelect = null;

            _isApplyingRate = true;

            try
            {
                TaxRates.Clear();

                foreach (var rate in rates)
                    TaxRates.Add(rate);

                if (preferredRateId.HasValue)
                    rateToSelect = TaxRates.FirstOrDefault(r => r.Id == preferredRateId.Value);

                if (rateToSelect == null && selectCurrentRate)
                {
                    DateTime today = DateTime.Today;

                    rateToSelect = TaxRates.FirstOrDefault(r =>
                        r.IsActive &&
                        r.EffectiveFrom.HasValue &&
                        r.EffectiveFrom.Value.Date <= today &&
                        (!r.EffectiveTo.HasValue || r.EffectiveTo.Value.Date >= today));

                    rateToSelect ??= TaxRates.FirstOrDefault();
                }

                SelectedTaxRate = rateToSelect;
            }
            finally
            {
                _isApplyingRate = false;
            }

            await ApplySelectedRateAsync(rateToSelect);
        }

        [RelayCommand(CanExecute = nameof(CanCreateNewRate))]
        private void NewRate()
        {
            _isApplyingRate = true;

            try
            {
                SelectedTaxRate = null;
            }
            finally
            {
                _isApplyingRate = false;
            }

            PrepareNewRateForm("Ready for a new Standard VAT rate version.");
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedTaxCategory?.Id == null || !IsRateBasedCategorySelected)
                return;

            if (!TryParseRatePercent(RatePercentText, out decimal ratePercent))
                return;

            if (!ValidateForm(ratePercent))
                return;

            DateTime effectiveFrom = EffectiveFromInput!.Value.Date;
            DateTime? effectiveTo = HasEffectiveToInput
                ? EffectiveToInput?.Date
                : null;

            string auditUser = GetAuditUser();
            string taxCode = SelectedTaxRate?.TaxCode ?? GenerateTaxCode(effectiveFrom);

            var rate = new TaxRate
            {
                Id = SelectedTaxRate?.Id ?? 0,
                TaxCode = taxCode,
                TaxName = "Standard VAT",
                TaxCategoryId = SelectedTaxCategory.Id,
                RatePercent = ratePercent,
                EffectiveFrom = effectiveFrom,
                EffectiveTo = effectiveTo,
                ChangeReason = NormalizeText(ChangeReasonInput),
                CreatedBy = SelectedTaxRate?.CreatedBy ?? auditUser,
                UpdatedBy = auditUser,
                IsActive = SelectedTaxRate?.IsActive ?? true,
                IsSystemDefault = SelectedTaxRate?.IsSystemDefault ?? false,
                DisplayOrder = SelectedTaxRate?.DisplayOrder ?? 10
            };

            IsBusy = true;

            try
            {
                if (SelectedTaxRate == null)
                    rate = await _taxRateRepository.AddAsync(rate);
                else
                    await _taxRateRepository.UpdateAsync(rate);

                await LoadRatesInternalAsync(
                    selectCurrentRate: false,
                    preferredRateId: rate.Id);

                StatusMessage = "Tax rate version saved successfully.";

                _messageBoxService.ShowInformation(
                    "Tax rate version saved successfully.",
                    "Saved");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                _messageBoxService.ShowError(
                    $"Failed to save the tax rate version:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedTaxRate == null)
                return;

            TaxRate selected = SelectedTaxRate;

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Permanently delete unused rate version '{selected.TaxCode}'?\n\n" +
                "Used or system-standard records cannot be deleted.",
                "Confirm Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _taxRateRepository.DeleteAsync(selected.Id);
                await LoadRatesInternalAsync(selectCurrentRate: true);

                StatusMessage = "Unused tax rate version deleted.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Delete Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"Failed to delete the tax rate version:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeactivateReactivate))]
        private async Task DeactivateReactivateAsync()
        {
            if (SelectedTaxRate == null)
                return;

            TaxRate selected = SelectedTaxRate;
            bool reactivate = !selected.IsActive;

            bool confirmed = _messageBoxService.ShowConfirmation(
                reactivate
                    ? $"Reactivate rate version '{selected.TaxCode}'?"
                    : $"Deactivate rate version '{selected.TaxCode}'?\n\n" +
                      "The currently effective Standard VAT rate cannot be deactivated until a replacement period exists.",
                reactivate ? "Confirm Reactivation" : "Confirm Deactivation",
                reactivate ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                if (reactivate)
                    await _taxRateRepository.ReactivateAsync(selected.Id, GetAuditUser());
                else
                    await _taxRateRepository.DeactivateAsync(selected.Id, GetAuditUser());

                await LoadRatesInternalAsync(
                    selectCurrentRate: false,
                    preferredRateId: selected.Id);

                StatusMessage = reactivate
                    ? "Tax rate version reactivated."
                    : "Tax rate version deactivated.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Status update blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Status Update Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Status update failed.";

                _messageBoxService.ShowError(
                    $"Failed to update the rate status:\n\n{ex.Message}",
                    "Status Update Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplySelectedRateAsync(TaxRate? value)
        {
            if (_isClearingForm)
                return;

            _isClearingForm = true;

            try
            {
                SelectedRateHasUsage = false;

                if (value == null)
                {
                    if (IsRateBasedCategorySelected)
                        PrepareNewRateForm("Ready for a new Standard VAT rate version.");
                    else
                        ClearEditorForCategory();

                    return;
                }

                TaxCodeInput = value.TaxCode;
                RatePercentText = value.RatePercent.ToString("0.##", CultureInfo.InvariantCulture);
                EffectiveFromInput = value.EffectiveFrom?.Date;
                HasEffectiveToInput = value.EffectiveTo.HasValue;
                EffectiveToInput = value.EffectiveTo?.Date;
                ChangeReasonInput = value.ChangeReason ?? string.Empty;
                UsageSummary = "Checking linked records...";
                StatusMessage = $"Selected rate version: {value.TaxCode}";
            }
            finally
            {
                _isClearingForm = false;
            }

            try
            {
                var summary = await _taxRateRepository.GetLinkedDataSummaryAsync(value.Id);

                if (SelectedTaxRate?.Id != value.Id)
                    return;

                SelectedRateHasUsage = summary.HasLinkedData;
                UsageSummary = summary.ToCompactSummary();
            }
            catch (Exception ex)
            {
                if (SelectedTaxRate?.Id == value.Id)
                {
                    UsageSummary = "Could not read linked-record usage.";

                    _messageBoxService.ShowError(
                        $"Failed to check tax-rate usage:\n\n{ex.Message}",
                        "Usage Check Error");
                }
            }
            finally
            {
                RaiseEditorState();
            }
        }

        private void PrepareNewRateForm(string statusMessage)
        {
            _isClearingForm = true;

            try
            {
                DateTime proposedDate = DateTime.Today;
                decimal proposedRate = GetLatestKnownStandardRate();

                TaxCodeInput = GenerateTaxCode(proposedDate);
                RatePercentText = proposedRate.ToString("0.##", CultureInfo.InvariantCulture);
                EffectiveFromInput = proposedDate;
                HasEffectiveToInput = false;
                EffectiveToInput = null;
                ChangeReasonInput = string.Empty;
                SelectedRateHasUsage = false;
                UsageSummary = "New rate version. Save only after confirming its legal effective date.";
                StatusMessage = statusMessage;
            }
            finally
            {
                _isClearingForm = false;
            }

            RaiseEditorState();
        }

        private void ClearEditorForCategory()
        {
            _isClearingForm = true;

            try
            {
                TaxCodeInput = string.Empty;
                RatePercentText = "0";
                EffectiveFromInput = null;
                HasEffectiveToInput = false;
                EffectiveToInput = null;
                ChangeReasonInput = string.Empty;
                SelectedRateHasUsage = false;

                UsageSummary = IsLegacyCategorySelected
                    ? "Select a legacy row to review its references and status."
                    : "No percentage record is required for this fixed 0% treatment.";
            }
            finally
            {
                _isClearingForm = false;
            }

            RaiseEditorState();
        }

        partial void OnSelectedTaxCategoryChanged(TaxCategoryOption? value)
        {
            RaiseCategoryState();

            if (_isApplyingCategory || !_isInitialized)
                return;

            _ = ChangeCategoryAsync();
        }

        private async Task ChangeCategoryAsync()
        {
            IsBusy = true;

            try
            {
                await LoadRatesInternalAsync(selectCurrentRate: true);

                StatusMessage = IsRateBasedCategorySelected
                    ? "Standard VAT rate history loaded."
                    : IsLegacyCategorySelected
                        ? "Legacy tax records loaded for review."
                        : "Fixed tax treatment selected; no rate version is required.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load the selected tax category.";

                _messageBoxService.ShowError(
                    $"Failed to load tax category data:\n\n{ex.Message}",
                    "Load Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedTaxRateChanged(TaxRate? value)
        {
            RaiseEditorState();

            if (_isApplyingRate || _isClearingForm)
                return;

            _ = ApplySelectedRateAsync(value);
        }

        partial void OnEffectiveFromInputChanged(DateTime? value)
        {
            if (_isClearingForm)
                return;

            if (SelectedTaxRate == null && value.HasValue && IsRateBasedCategorySelected)
                TaxCodeInput = GenerateTaxCode(value.Value.Date);

            if (HasEffectiveToInput &&
                value.HasValue &&
                EffectiveToInput.HasValue &&
                EffectiveToInput.Value.Date < value.Value.Date)
            {
                EffectiveToInput = value.Value.Date;
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasEffectiveToInputChanged(bool value)
        {
            if (_isClearingForm)
                return;

            if (!value)
                EffectiveToInput = null;
            else if (!EffectiveToInput.HasValue)
                EffectiveToInput = EffectiveFromInput?.Date ?? DateTime.Today;

            OnPropertyChanged(nameof(CanEditEffectiveTo));
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnEffectiveToInputChanged(DateTime? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnRatePercentTextChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnChangeReasonInputChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIncludeDeactivatedChanged(bool value)
        {
            if (_isInitialized && !IsBusy)
                _ = SearchRefreshAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_isInitialized)
                StatusMessage = "Press Enter or click SEARCH / REFRESH.";
        }

        partial void OnSelectedRateHasUsageChanged(bool value)
        {
            RaiseEditorState();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SearchRefreshCommand.NotifyCanExecuteChanged();
            ClearSearchCommand.NotifyCanExecuteChanged();
            NewRateCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeactivateReactivateCommand.NotifyCanExecuteChanged();
        }

        private void RaiseCategoryState()
        {
            OnPropertyChanged(nameof(IsLegacyCategorySelected));
            OnPropertyChanged(nameof(IsRateBasedCategorySelected));
            OnPropertyChanged(nameof(IsFixedTreatmentCategorySelected));
            OnPropertyChanged(nameof(IsRateEditorEnabled));
            OnPropertyChanged(nameof(SelectedCategoryCode));
            OnPropertyChanged(nameof(SelectedCategoryName));
            OnPropertyChanged(nameof(EditorTitle));
            OnPropertyChanged(nameof(CategoryGuidance));

            RaiseEditorState();
        }

        private void RaiseEditorState()
        {
            OnPropertyChanged(nameof(IsExistingRate));
            OnPropertyChanged(nameof(CanEditCoreRateFields));
            OnPropertyChanged(nameof(IsCoreRateReadOnly));
            OnPropertyChanged(nameof(CanEditEffectiveTo));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(DeactivateReactivateButtonText));

            NewRateCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeactivateReactivateCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanCreateNewRate()
        {
            return !IsBusy && IsRateBasedCategorySelected;
        }

        private bool CanSave()
        {
            return !IsBusy &&
                   IsRateBasedCategorySelected &&
                   EffectiveFromInput.HasValue &&
                   !string.IsNullOrWhiteSpace(RatePercentText) &&
                   !string.IsNullOrWhiteSpace(ChangeReasonInput);
        }

        private bool CanDelete()
        {
            return !IsBusy &&
                   SelectedTaxRate != null &&
                   !SelectedRateHasUsage &&
                   !SelectedTaxRate.IsSystemDefault;
        }

        private bool CanDeactivateReactivate()
        {
            return !IsBusy && SelectedTaxRate != null;
        }

        private bool ValidateForm(decimal ratePercent)
        {
            if (ratePercent <= 0m || ratePercent > 100m)
            {
                _messageBoxService.ShowWarning(
                    "Standard VAT rate must be greater than 0 and not more than 100.",
                    "Validation Error");

                return false;
            }

            if (decimal.Round(ratePercent, 2) != ratePercent)
            {
                _messageBoxService.ShowWarning(
                    "Standard VAT rate can have no more than two decimal places.",
                    "Validation Error");

                return false;
            }

            if (!EffectiveFromInput.HasValue)
            {
                _messageBoxService.ShowWarning(
                    "Effective-from date is required.",
                    "Validation Error");

                return false;
            }

            if (HasEffectiveToInput && !EffectiveToInput.HasValue)
            {
                _messageBoxService.ShowWarning(
                    "Select an effective-to date or untick the end-date option.",
                    "Validation Error");

                return false;
            }

            if (EffectiveToInput.HasValue &&
                EffectiveToInput.Value.Date < EffectiveFromInput.Value.Date)
            {
                _messageBoxService.ShowWarning(
                    "Effective-to date cannot be earlier than effective-from date.",
                    "Validation Error");

                return false;
            }

            string reason = NormalizeText(ChangeReasonInput);

            if (string.IsNullOrWhiteSpace(reason))
            {
                _messageBoxService.ShowWarning(
                    "Change reason is required for the tax-rate audit history.",
                    "Validation Error");

                return false;
            }

            if (reason.Length > 250)
            {
                _messageBoxService.ShowWarning(
                    "Change reason cannot be longer than 250 characters.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private bool TryParseRatePercent(string value, out decimal ratePercent)
        {
            string raw = NormalizeText(value);

            bool parsed = decimal.TryParse(
                raw,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out ratePercent);

            if (!parsed)
            {
                parsed = decimal.TryParse(
                    raw,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out ratePercent);
            }

            if (!parsed)
            {
                _messageBoxService.ShowWarning(
                    "Rate percent must be a valid number.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private decimal GetLatestKnownStandardRate()
        {
            var latest = TaxRates
                .Where(r => r.TaxCategoryId == SelectedTaxCategory?.Id)
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();

            return latest?.RatePercent ?? 18m;
        }

        private string GetAuditUser()
        {
            return string.IsNullOrWhiteSpace(_authService.CurrentUser?.Username)
                ? "System"
                : _authService.CurrentUser.Username.Trim();
        }

        private static string GenerateTaxCode(DateTime effectiveFrom)
        {
            return $"VAT-STD-{effectiveFrom:yyyyMMdd}";
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
