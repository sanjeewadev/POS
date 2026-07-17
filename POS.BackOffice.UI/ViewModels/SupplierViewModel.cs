using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierViewModel : ViewModelBase
    {
        private readonly SupplierRepository _supplierRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isApplyingSelection;

        private static readonly Regex SupplierCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PhoneRegex =
            new("^[0-9+\\-\\s()]{7,20}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BasicEmailRegex =
            new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // =========================================================
        // FORM FIELDS
        // =========================================================

        [ObservableProperty]
        private string _supplierCode = string.Empty;

        [ObservableProperty]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _contactPerson = string.Empty;

        [ObservableProperty]
        private string _phone1 = string.Empty;

        [ObservableProperty]
        private string _phone2 = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private bool _hasVat = false;

        [ObservableProperty]
        private string _vatNumber = string.Empty;

        [ObservableProperty]
        private int _defaultCreditDays = 30;

        [ObservableProperty]
        private decimal _currentBalance = 0m;

        [ObservableProperty]
        private bool _isDeactivated = false;

        // =========================================================
        // SEARCH / SELECTION
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _includeDeactivated = true;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsExistingSupplier => SelectedSupplier != null;

        public bool IsSupplierCodeReadOnly => IsExistingSupplier;

        public bool IsVatNumberInputEnabled => HasVat && !IsBusy;

        public string SupplierStatusText
        {
            get
            {
                if (SelectedSupplier == null)
                    return "New Supplier";

                return IsDeactivated ? "Suspended" : "Active";
            }
        }

        public string DeactivateReactivateButtonText =>
            IsDeactivated ? "REACTIVATE" : "DEACTIVATE";

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public SupplierViewModel(
            SupplierRepository supplierRepository,
            IMessageBoxService messageBoxService)
        {
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanInitialize))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = await TryLoadDataAsync();
            InitializeCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            await TryLoadDataAsync();
        }

        private async Task<bool> TryLoadDataAsync()
        {
            IsBusy = true;

            try
            {
                await LoadDataInternalAsync();
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load suppliers.";

                _messageBoxService.ShowError(
                    $"Failed to load suppliers:\n\n{ex.Message}",
                    "Database Error");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDataInternalAsync()
        {
            Suppliers.Clear();

            var data = await _supplierRepository.GetAllFilteredAsync(
                searchTerm: SearchText,
                includeDeactivated: IncludeDeactivated);

            foreach (var supplier in data)
                Suppliers.Add(supplier);

            StatusMessage = IncludeDeactivated
                ? $"{Suppliers.Count} supplier record(s) loaded, including suspended suppliers."
                : $"{Suppliers.Count} active supplier record(s) loaded.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearSearchAsync()
        {
            SearchText = string.Empty;
            await LoadDataAsync();
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (_isApplyingSelection)
                return;

            ApplySelectedSupplier(value);
        }

        private void ApplySelectedSupplier(Supplier? value)
        {
            _isApplyingSelection = true;

            try
            {
                if (value == null)
                {
                    ResetForm("Ready for new supplier.");
                    return;
                }

                SupplierCode = value.SupplierCode ?? string.Empty;
                SupplierName = value.SupplierName ?? string.Empty;
                CompanyName = value.CompanyName ?? string.Empty;
                ContactPerson = value.ContactPerson ?? string.Empty;
                Phone1 = value.Phone1 ?? string.Empty;
                Phone2 = value.Phone2 ?? string.Empty;
                Email = value.Email ?? string.Empty;
                Address = value.Address ?? string.Empty;
                HasVat = value.HasVat;
                VatNumber = value.HasVat ? value.VatNumber ?? string.Empty : string.Empty;
                DefaultCreditDays = value.DefaultCreditDays;
                CurrentBalance = value.CurrentBalance;
                IsDeactivated = value.IsDeactivated;

                StatusMessage = $"Editing supplier: {value.SupplierName}";
            }
            finally
            {
                _isApplyingSelection = false;
                RaiseFormStateProperties();
                NotifyCommandStates();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string supplierCode = SelectedSupplier == null
                ? NormalizeCode(SupplierCode)
                : NormalizeCode(SelectedSupplier.SupplierCode);

            string supplierName = NormalizeText(SupplierName);
            string companyName = NormalizeText(CompanyName);
            string contactPerson = NormalizeText(ContactPerson);
            string phone1 = NormalizeText(Phone1);
            string phone2 = NormalizeText(Phone2);
            string email = NormalizeText(Email).ToLowerInvariant();
            string address = NormalizeText(Address);
            string vatNumber = HasVat ? NormalizeText(VatNumber).ToUpperInvariant() : string.Empty;

            if (!ValidateInput(
                    supplierCode,
                    supplierName,
                    companyName,
                    contactPerson,
                    phone1,
                    phone2,
                    email,
                    address,
                    HasVat,
                    vatNumber,
                    DefaultCreditDays))
            {
                return;
            }

            IsBusy = true;

            try
            {
                int currentSupplierId = SelectedSupplier?.Id ?? 0;

                bool isCodeUnique = await _supplierRepository.IsCodeUniqueAsync(
                    supplierCode,
                    currentSupplierId);

                if (!isCodeUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Supplier Code '{supplierCode}' is already in use.",
                        "Duplicate Supplier Code");

                    return;
                }

                if (SelectedSupplier == null)
                {
                    var newSupplier = new Supplier
                    {
                        SupplierCode = supplierCode,
                        SupplierName = supplierName,
                        CompanyName = companyName,
                        ContactPerson = contactPerson,
                        Phone1 = phone1,
                        Phone2 = phone2,
                        Email = email,
                        Address = address,
                        HasVat = HasVat,
                        VatNumber = vatNumber,
                        DefaultCreditDays = DefaultCreditDays,
                        CurrentBalance = 0m,
                        IsDeactivated = IsDeactivated
                    };

                    await _supplierRepository.AddAsync(newSupplier);

                    await LoadDataInternalAsync();
                    ResetForm("Supplier created successfully.");

                    _messageBoxService.ShowInformation(
                        "Supplier created successfully.",
                        "Success");
                }
                else
                {
                    var updatedSupplier = new Supplier
                    {
                        Id = SelectedSupplier.Id,
                        SupplierCode = SelectedSupplier.SupplierCode,
                        SupplierName = supplierName,
                        CompanyName = companyName,
                        ContactPerson = contactPerson,
                        Phone1 = phone1,
                        Phone2 = phone2,
                        Email = email,
                        Address = address,
                        HasVat = HasVat,
                        VatNumber = vatNumber,
                        DefaultCreditDays = DefaultCreditDays,
                        CurrentBalance = SelectedSupplier.CurrentBalance,
                        IsDeactivated = IsDeactivated
                    };

                    await _supplierRepository.UpdateAsync(updatedSupplier);

                    await LoadDataInternalAsync();
                    ResetForm("Supplier updated successfully.");

                    _messageBoxService.ShowInformation(
                        "Supplier updated successfully.",
                        "Success");
                }
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
                    $"An error occurred while saving:\n\n{ex.Message}",
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
            ResetForm("Ready for new supplier.");
        }

        private void ResetForm(string statusMessage)
        {
            _isApplyingSelection = true;

            try
            {
                SelectedSupplier = null;

                SupplierCode = string.Empty;
                SupplierName = string.Empty;
                CompanyName = string.Empty;
                ContactPerson = string.Empty;
                Phone1 = string.Empty;
                Phone2 = string.Empty;
                Email = string.Empty;
                Address = string.Empty;
                HasVat = false;
                VatNumber = string.Empty;
                DefaultCreditDays = 30;
                CurrentBalance = 0m;
                IsDeactivated = false;

                StatusMessage = statusMessage;
            }
            finally
            {
                _isApplyingSelection = false;
                RaiseFormStateProperties();
                NotifyCommandStates();
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSupplier))]
        private async Task DeleteSupplierAsync()
        {
            if (SelectedSupplier == null)
                return;

            var selected = SelectedSupplier;

            IsBusy = true;

            try
            {
                var linkedData = await _supplierRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData || selected.CurrentBalance != 0m)
                {
                    StatusMessage = "Delete blocked. Supplier has links or balance.";

                    string message = linkedData.HasLinkedData
                        ? linkedData.ToUserMessage(selected.SupplierName)
                        : "Supplier cannot be permanently deleted while current balance is not zero. Use Deactivate instead.";

                    _messageBoxService.ShowWarning(
                        message,
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete check failed.";

                _messageBoxService.ShowError(
                    $"Could not check linked records:\n\n{ex.Message}",
                    "Delete Check Error");

                return;
            }
            finally
            {
                IsBusy = false;
            }

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Permanently delete unused supplier '{selected.SupplierName}'?\n\n" +
                "This is only safe for wrongly-created suppliers with no documents, no ledger, no item links, and zero balance.",
                "Confirm Permanent Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _supplierRepository.HardDeleteAsync(selected.Id);
                await LoadDataInternalAsync();
                ResetForm("Supplier deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Supplier deleted successfully.",
                    "Deleted");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"Failed to delete supplier:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeactivateSupplier))]
        private async Task DeactivateSupplierAsync()
        {
            if (SelectedSupplier == null)
                return;

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Deactivate / suspend supplier '{SelectedSupplier.SupplierName}'?\n\n" +
                "This keeps all old PO, GRN, ledger, supplier return, and report history safe. " +
                "The supplier will be hidden from new purchasing selections.",
                "Confirm Supplier Deactivation",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _supplierRepository.DeactivateAsync(SelectedSupplier.Id);
                await LoadDataInternalAsync();
                ResetForm("Supplier deactivated successfully.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Deactivate failed.";

                _messageBoxService.ShowError(
                    $"Failed to deactivate supplier:\n\n{ex.Message}",
                    "Deactivate Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanReactivateSupplier))]
        private async Task ReactivateSupplierAsync()
        {
            if (SelectedSupplier == null)
                return;

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Reactivate supplier '{SelectedSupplier.SupplierName}'?",
                "Confirm Supplier Reactivation",
                MessageBoxImage.Question);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _supplierRepository.ReactivateAsync(SelectedSupplier.Id);
                await LoadDataInternalAsync();
                ResetForm("Supplier reactivated successfully.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Reactivate failed.";

                _messageBoxService.ShowError(
                    $"Failed to reactivate supplier:\n\n{ex.Message}",
                    "Reactivate Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // PROPERTY CHANGED / COMMAND STATE
        // =========================================================

        partial void OnSupplierCodeChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierNameChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnPhone1Changed(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasVatChanged(bool value)
        {
            if (!value)
                VatNumber = string.Empty;

            OnPropertyChanged(nameof(IsVatNumberInputEnabled));
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnVatNumberChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnDefaultCreditDaysChanged(int value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIncludeDeactivatedChanged(bool value)
        {
            if (_isInitialized && !IsBusy)
                _ = LoadDataAsync();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsVatNumberInputEnabled));
            RaiseFormStateProperties();
            NotifyCommandStates();
        }

        private void RaiseFormStateProperties()
        {
            OnPropertyChanged(nameof(IsExistingSupplier));
            OnPropertyChanged(nameof(IsSupplierCodeReadOnly));
            OnPropertyChanged(nameof(IsVatNumberInputEnabled));
            OnPropertyChanged(nameof(SupplierStatusText));
            OnPropertyChanged(nameof(DeactivateReactivateButtonText));
        }

        private void NotifyCommandStates()
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            ClearSearchCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteSupplierCommand.NotifyCanExecuteChanged();
            DeactivateSupplierCommand.NotifyCanExecuteChanged();
            ReactivateSupplierCommand.NotifyCanExecuteChanged();
        }

        private bool CanInitialize()
        {
            return !IsBusy && !_isInitialized;
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy &&
                   !string.IsNullOrWhiteSpace(SupplierCode) &&
                   !string.IsNullOrWhiteSpace(SupplierName) &&
                   !string.IsNullOrWhiteSpace(Phone1) &&
                   (!HasVat || !string.IsNullOrWhiteSpace(VatNumber));
        }

        private bool CanDeleteSupplier()
        {
            return !IsBusy && SelectedSupplier != null;
        }

        private bool CanDeactivateSupplier()
        {
            return !IsBusy && SelectedSupplier != null && !IsDeactivated;
        }

        private bool CanReactivateSupplier()
        {
            return !IsBusy && SelectedSupplier != null && IsDeactivated;
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private bool ValidateInput(
            string supplierCode,
            string supplierName,
            string companyName,
            string contactPerson,
            string phone1,
            string phone2,
            string email,
            string address,
            bool hasVat,
            string vatNumber,
            int defaultCreditDays)
        {
            if (string.IsNullOrWhiteSpace(supplierCode))
            {
                _messageBoxService.ShowWarning("Supplier Code is required.", "Validation Error");
                return false;
            }

            if (supplierCode.Length > 20)
            {
                _messageBoxService.ShowWarning("Supplier Code cannot be longer than 20 characters.", "Validation Error");
                return false;
            }

            if (!SupplierCodeRegex.IsMatch(supplierCode))
            {
                _messageBoxService.ShowWarning("Supplier Code can only contain letters, numbers, dash, and underscore.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(supplierName))
            {
                _messageBoxService.ShowWarning("Supplier Name is required.", "Validation Error");
                return false;
            }

            if (supplierName.Length > 150 || companyName.Length > 150)
            {
                _messageBoxService.ShowWarning("Supplier Name and Company Name cannot be longer than 150 characters.", "Validation Error");
                return false;
            }

            if (contactPerson.Length > 50)
            {
                _messageBoxService.ShowWarning("Contact Person cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(phone1))
            {
                _messageBoxService.ShowWarning("Primary Phone is required.", "Validation Error");
                return false;
            }

            if (!PhoneRegex.IsMatch(phone1))
            {
                _messageBoxService.ShowWarning("Primary Phone number is invalid.", "Validation Error");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(phone2) && !PhoneRegex.IsMatch(phone2))
            {
                _messageBoxService.ShowWarning("Secondary Phone number is invalid.", "Validation Error");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email) && !BasicEmailRegex.IsMatch(email))
            {
                _messageBoxService.ShowWarning("Email address is invalid.", "Validation Error");
                return false;
            }

            if (email.Length > 100 || address.Length > 250)
            {
                _messageBoxService.ShowWarning("Email or Address is too long.", "Validation Error");
                return false;
            }

            if (hasVat && string.IsNullOrWhiteSpace(vatNumber))
            {
                _messageBoxService.ShowWarning(
                    "VAT Registration Number is required when VAT Registered Supplier is ticked.",
                    "VAT Validation");

                return false;
            }

            if (vatNumber.Length > 50)
            {
                _messageBoxService.ShowWarning("VAT Registration Number cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (defaultCreditDays < 0 || defaultCreditDays > 365)
            {
                _messageBoxService.ShowWarning("Default Credit Days must be between 0 and 365.", "Validation Error");
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
