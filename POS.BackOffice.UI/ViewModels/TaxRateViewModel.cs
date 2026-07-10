using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class TaxRateViewModel : ObservableObject
    {
        private const int MaxDisplayOrder = 9999;

        private readonly TaxRateRepository _taxRateRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isApplyingSelection;
        private bool _isClearing;

        public ObservableCollection<TaxRate> TaxRates { get; } = new();

        [ObservableProperty]
        private TaxRate? _selectedTaxRate;

        [ObservableProperty]
        private string _taxCodeInput = string.Empty;

        [ObservableProperty]
        private string _taxNameInput = string.Empty;

        [ObservableProperty]
        private string _ratePercentText = "0";

        [ObservableProperty]
        private string _displayOrderText = "0";

        [ObservableProperty]
        private bool _isActiveInput = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _includeDeactivated = false;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsExistingTaxRate => SelectedTaxRate != null;

        public bool IsTaxCodeReadOnly => IsExistingTaxRate;

        public string DeactivateReactivateButtonText =>
            SelectedTaxRate?.IsActive == false
                ? "REACTIVATE"
                : "DEACTIVATE";

        public TaxRateViewModel(
            TaxRateRepository taxRateRepository,
            IMessageBoxService messageBoxService)
        {
            _taxRateRepository = taxRateRepository ?? throw new ArgumentNullException(nameof(taxRateRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            _ = InitializeAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            IsBusy = true;

            try
            {
                await _taxRateRepository.EnsureDefaultsAsync();
                await LoadTaxRatesInternalAsync();

                StatusMessage = "Tax Master loaded.";
            }
            catch (Exception ex)
            {
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

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadTaxRatesAsync()
        {
            IsBusy = true;

            try
            {
                await LoadTaxRatesInternalAsync();

                StatusMessage = IncludeDeactivated
                    ? $"{TaxRates.Count} tax rate record(s) loaded, including deactivated."
                    : $"{TaxRates.Count} active tax rate record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load tax rates.";

                _messageBoxService.ShowError(
                    $"Failed to load tax rates:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadTaxRatesInternalAsync()
        {
            TaxRates.Clear();

            var taxRates = await _taxRateRepository.GetAllAsync(
                SearchText,
                IncludeDeactivated);

            foreach (var taxRate in taxRates)
                TaxRates.Add(taxRate);
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchRefreshAsync()
        {
            await LoadTaxRatesAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearSearchAsync()
        {
            SearchText = string.Empty;
            await LoadTaxRatesAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string taxCode = NormalizeCode(TaxCodeInput);
            string taxName = NormalizeText(TaxNameInput);

            if (!TryParseRatePercent(RatePercentText, out decimal ratePercent))
                return;

            if (!TryParseDisplayOrder(DisplayOrderText, out int displayOrder))
                return;

            if (!ValidateInput(taxCode, taxName, ratePercent, displayOrder))
                return;

            IsBusy = true;

            try
            {
                if (SelectedTaxRate == null)
                {
                    var newTaxRate = new TaxRate
                    {
                        TaxCode = taxCode,
                        TaxName = taxName,
                        RatePercent = ratePercent,
                        IsActive = IsActiveInput,
                        DisplayOrder = displayOrder,
                        IsSystemDefault = false
                    };

                    await _taxRateRepository.AddAsync(newTaxRate);
                }
                else
                {
                    var updatedTaxRate = new TaxRate
                    {
                        Id = SelectedTaxRate.Id,
                        TaxCode = SelectedTaxRate.TaxCode,
                        TaxName = taxName,
                        RatePercent = ratePercent,
                        IsActive = IsActiveInput,
                        DisplayOrder = displayOrder,
                        IsSystemDefault = SelectedTaxRate.IsSystemDefault
                    };

                    await _taxRateRepository.UpdateAsync(updatedTaxRate);
                }

                await LoadTaxRatesInternalAsync();
                ClearFormOnly("Tax rate saved successfully.");

                _messageBoxService.ShowInformation(
                    "Tax rate saved successfully.",
                    "Success");
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
                    $"Failed to save tax rate:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            ClearFormOnly("Ready for new tax rate.");
        }

        private void ClearFormOnly(string statusMessage)
        {
            _isClearing = true;

            try
            {
                SelectedTaxRate = null;
                TaxCodeInput = string.Empty;
                TaxNameInput = string.Empty;
                RatePercentText = "0";
                DisplayOrderText = "0";
                IsActiveInput = true;
                StatusMessage = statusMessage;
            }
            finally
            {
                _isClearing = false;
            }

            RaiseFormState();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedTaxRate == null)
                return;

            var selected = SelectedTaxRate;

            IsBusy = true;

            try
            {
                var linkedData = await _taxRateRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.TaxCode),
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete check failed.";

                _messageBoxService.ShowError(
                    $"Failed to check linked data:\n\n{ex.Message}",
                    "Delete Check Error");

                return;
            }
            finally
            {
                IsBusy = false;
            }

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Permanently delete tax rate '{selected.TaxCode}'?\n\n" +
                "This is only safe for wrongly-created unused tax records.\n" +
                "For real business records, deactivate instead.",
                "Confirm Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _taxRateRepository.DeleteAsync(selected.Id);
                await LoadTaxRatesInternalAsync();
                ClearFormOnly("Tax rate deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Tax rate deleted successfully.",
                    "Deleted");
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
                    $"Failed to delete tax rate:\n\n{ex.Message}",
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

            var selected = SelectedTaxRate;
            bool shouldReactivate = !selected.IsActive;

            bool confirmed = _messageBoxService.ShowConfirmation(
                shouldReactivate
                    ? $"Reactivate tax rate '{selected.TaxCode}'?"
                    : $"Deactivate tax rate '{selected.TaxCode}'?\n\nIt will be hidden from new Item Master selections, but old item/PO history will remain safe.",
                shouldReactivate ? "Confirm Reactivation" : "Confirm Deactivation",
                shouldReactivate ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                if (shouldReactivate)
                    await _taxRateRepository.ReactivateAsync(selected.Id);
                else
                    await _taxRateRepository.DeactivateAsync(selected.Id);

                await LoadTaxRatesInternalAsync();
                ClearFormOnly(shouldReactivate
                    ? "Tax rate reactivated successfully."
                    : "Tax rate deactivated successfully.");
            }
            catch (Exception ex)
            {
                StatusMessage = shouldReactivate
                    ? "Reactivate failed."
                    : "Deactivate failed.";

                _messageBoxService.ShowError(
                    $"Failed to update tax rate status:\n\n{ex.Message}",
                    "Status Update Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedTaxRateChanged(TaxRate? value)
        {
            if (_isApplyingSelection || _isClearing)
                return;

            _isApplyingSelection = true;

            try
            {
                if (value == null)
                {
                    TaxCodeInput = string.Empty;
                    TaxNameInput = string.Empty;
                    RatePercentText = "0";
                    DisplayOrderText = "0";
                    IsActiveInput = true;
                    StatusMessage = "Ready for new tax rate.";
                    return;
                }

                TaxCodeInput = value.TaxCode ?? string.Empty;
                TaxNameInput = value.TaxName ?? string.Empty;
                RatePercentText = value.RatePercent.ToString("0.##", CultureInfo.InvariantCulture);
                DisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                IsActiveInput = value.IsActive;
                StatusMessage = $"Editing tax rate: {value.TaxCode}";
            }
            finally
            {
                _isApplyingSelection = false;
            }

            RaiseFormState();
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_isInitialized)
                StatusMessage = "Type search text and click SEARCH / REFRESH.";
        }

        partial void OnIncludeDeactivatedChanged(bool value)
        {
            if (_isInitialized && !IsBusy)
                _ = LoadTaxRatesAsync();
        }

        partial void OnTaxCodeInputChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnTaxNameInputChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnRatePercentTextChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnDisplayOrderTextChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadTaxRatesCommand.NotifyCanExecuteChanged();
            SearchRefreshCommand.NotifyCanExecuteChanged();
            ClearSearchCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeactivateReactivateCommand.NotifyCanExecuteChanged();
        }

        private void RaiseFormState()
        {
            OnPropertyChanged(nameof(IsExistingTaxRate));
            OnPropertyChanged(nameof(IsTaxCodeReadOnly));
            OnPropertyChanged(nameof(DeactivateReactivateButtonText));

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeactivateReactivateCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy &&
                   !string.IsNullOrWhiteSpace(TaxCodeInput) &&
                   !string.IsNullOrWhiteSpace(TaxNameInput);
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedTaxRate != null;
        }

        private bool CanDeactivateReactivate()
        {
            return !IsBusy && SelectedTaxRate != null;
        }

        private bool ValidateInput(
            string taxCode,
            string taxName,
            decimal ratePercent,
            int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
            {
                _messageBoxService.ShowWarning("Tax code is required.", "Validation Error");
                return false;
            }

            if (taxCode.Length > 20)
            {
                _messageBoxService.ShowWarning("Tax code cannot be longer than 20 characters.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(taxName))
            {
                _messageBoxService.ShowWarning("Tax name is required.", "Validation Error");
                return false;
            }

            if (taxName.Length > 100)
            {
                _messageBoxService.ShowWarning("Tax name cannot be longer than 100 characters.", "Validation Error");
                return false;
            }

            if (ratePercent < 0 || ratePercent > 100)
            {
                _messageBoxService.ShowWarning("Tax rate must be between 0 and 100.", "Validation Error");
                return false;
            }

            if (displayOrder < 0 || displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning($"Display order must be between 0 and {MaxDisplayOrder}.", "Validation Error");
                return false;
            }

            return true;
        }

        private bool TryParseRatePercent(string value, out decimal ratePercent)
        {
            ratePercent = 0m;

            string raw = NormalizeText(value);

            if (string.IsNullOrWhiteSpace(raw))
                return true;

            bool parsed = decimal.TryParse(
                raw,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out ratePercent);

            if (!parsed)
            {
                _messageBoxService.ShowWarning("Rate percent must be a number.", "Validation Error");
                return false;
            }

            return true;
        }

        private bool TryParseDisplayOrder(string value, out int displayOrder)
        {
            displayOrder = 0;

            string raw = NormalizeText(value);

            if (string.IsNullOrWhiteSpace(raw))
                return true;

            bool parsed = int.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out displayOrder);

            if (!parsed)
            {
                _messageBoxService.ShowWarning("Display order must be a whole number.", "Validation Error");
                return false;
            }

            return true;
        }

        private static string NormalizeCode(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
