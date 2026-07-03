using System;
using System.Collections.ObjectModel;
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

        // =========================================================
        // IDENTITY FIELDS
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
        private bool _isCodeReadOnly = false;

        // =========================================================
        // CONTACT FIELDS
        // =========================================================

        [ObservableProperty]
        private string _phone1 = string.Empty;

        [ObservableProperty]
        private string _phone2 = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        // =========================================================
        // FINANCIAL / TAX FIELDS
        // =========================================================

        [ObservableProperty]
        private bool _hasVat = false;

        [ObservableProperty]
        private string _vatNumber = string.Empty;

        [ObservableProperty]
        private int _defaultCreditDays = 30;

        // Display-only. Supplier Master should not directly update this.
        [ObservableProperty]
        private decimal _currentBalance = 0m;

        [ObservableProperty]
        private bool _isDeactivated = false;

        // =========================================================
        // STATE / FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

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

            _isInitialized = true;
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                await LoadDataInternalAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load suppliers.";

                _messageBoxService.ShowError(
                    $"Failed to load suppliers:\n\n{ex.Message}",
                    "Database Error");
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
                includeDeactivated: true);

            foreach (var supplier in data)
            {
                Suppliers.Add(supplier);
            }

            StatusMessage = $"{Suppliers.Count} supplier record(s) loaded.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshAsync()
        {
            SearchText = string.Empty;
            await LoadDataAsync();
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
            string vatNumber = HasVat ? NormalizeText(VatNumber) : string.Empty;

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

                        // Supplier code is intentionally kept stable after creation.
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

                        // Repository intentionally does not update CurrentBalance from Supplier Master.
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

            IsCodeReadOnly = false;

            _isApplyingSelection = false;

            StatusMessage = statusMessage;

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedSupplier == null)
                return;

            var selected = SelectedSupplier;

            IsBusy = true;

            try
            {
                var linkedData = await _supplierRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.SupplierName),
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Linked data check failed.";

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
                $"Delete supplier '{selected.SupplierName}'?\n\n" +
                "This is only safe for wrongly-created or unused test suppliers.\n\n" +
                "For real business records, suspend/deactivate the supplier instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _supplierRepository.DeleteAsync(selected.Id);

                await LoadDataInternalAsync();
                ResetForm("Supplier deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Supplier deleted successfully.",
                    "Deleted");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                _messageBoxService.ShowWarning(
                    $"{ex.Message}\n\nTo hide this supplier from new PO/GRN screens, tick 'Suspend / Deactivate Supplier' and click SAVE.",
                    "Delete Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // PROPERTY CHANGE HANDLERS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            if (_isInitialized)
            {
                StatusMessage = "Type search text and click SEARCH.";
            }
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (_isApplyingSelection)
                return;

            if (value != null)
            {
                SupplierCode = value.SupplierCode ?? string.Empty;
                SupplierName = value.SupplierName ?? string.Empty;
                CompanyName = value.CompanyName ?? string.Empty;
                ContactPerson = value.ContactPerson ?? string.Empty;

                Phone1 = value.Phone1 ?? string.Empty;
                Phone2 = value.Phone2 ?? string.Empty;
                Email = value.Email ?? string.Empty;
                Address = value.Address ?? string.Empty;

                HasVat = value.HasVat;
                VatNumber = value.VatNumber ?? string.Empty;
                DefaultCreditDays = value.DefaultCreditDays;
                CurrentBalance = value.CurrentBalance;
                IsDeactivated = value.IsDeactivated;

                IsCodeReadOnly = true;

                StatusMessage = $"Editing supplier: {value.SupplierName}";
            }
            else
            {
                IsCodeReadOnly = false;
            }

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasVatChanged(bool value)
        {
            if (!value)
            {
                VatNumber = string.Empty;
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierCodeChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnSupplierNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnCompanyNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnContactPersonChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnPhone1Changed(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnPhone2Changed(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnEmailChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnAddressChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnVatNumberChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnDefaultCreditDaysChanged(int value) => SaveCommand.NotifyCanExecuteChanged();

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

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
                   !string.IsNullOrWhiteSpace(Phone1);
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedSupplier != null;
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private static string NormalizeCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

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
                _messageBoxService.ShowWarning(
                    "Supplier Code can only contain letters, numbers, dash, and underscore.",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(supplierName))
            {
                _messageBoxService.ShowWarning("Supplier Name is required.", "Validation Error");
                return false;
            }

            if (supplierName.Length > 150)
            {
                _messageBoxService.ShowWarning("Supplier Name cannot be longer than 150 characters.", "Validation Error");
                return false;
            }

            if (companyName.Length > 150)
            {
                _messageBoxService.ShowWarning("Company Name cannot be longer than 150 characters.", "Validation Error");
                return false;
            }

            if (contactPerson.Length > 50)
            {
                _messageBoxService.ShowWarning("Contact Person cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(phone1))
            {
                _messageBoxService.ShowWarning("Phone 1 is required.", "Validation Error");
                return false;
            }

            if (!PhoneRegex.IsMatch(phone1))
            {
                _messageBoxService.ShowWarning("Phone 1 is not valid.", "Validation Error");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(phone2) && !PhoneRegex.IsMatch(phone2))
            {
                _messageBoxService.ShowWarning("Phone 2 is not valid.", "Validation Error");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (email.Length > 100)
                {
                    _messageBoxService.ShowWarning("Email cannot be longer than 100 characters.", "Validation Error");
                    return false;
                }

                try
                {
                    _ = new System.Net.Mail.MailAddress(email);
                }
                catch
                {
                    _messageBoxService.ShowWarning("Email address is not valid.", "Validation Error");
                    return false;
                }
            }

            if (address.Length > 250)
            {
                _messageBoxService.ShowWarning("Address cannot be longer than 250 characters.", "Validation Error");
                return false;
            }

            if (hasVat && string.IsNullOrWhiteSpace(vatNumber))
            {
                _messageBoxService.ShowWarning(
                    "VAT number is required when 'Has VAT Number' is checked.",
                    "Validation Error");

                return false;
            }

            if (vatNumber.Length > 50)
            {
                _messageBoxService.ShowWarning("VAT Number cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (defaultCreditDays < 0)
            {
                _messageBoxService.ShowWarning("Default Credit Days cannot be negative.", "Validation Error");
                return false;
            }

            if (defaultCreditDays > 365)
            {
                _messageBoxService.ShowWarning("Default Credit Days cannot be greater than 365.", "Validation Error");
                return false;
            }

            return true;
        }
    }
}