using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierViewModel : ViewModelBase
    {
        private readonly SupplierRepository _supplierRepository;

        private bool _isApplyingSelection = false;

        private static readonly Regex SupplierCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        private static readonly Regex PhoneRegex =
            new Regex("^[0-9+\\-\\s()]{7,20}$", RegexOptions.Compiled);

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

        public SupplierViewModel(SupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                Suppliers.Clear();

                var data = await _supplierRepository.GetAllFilteredAsync(SearchText);

                foreach (var supplier in data)
                {
                    Suppliers.Add(supplier);
                }

                StatusMessage = $"{Suppliers.Count} supplier record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load suppliers.";

                MessageBox.Show(
                    $"Failed to load suppliers:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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
            string supplierCode = NormalizeCode(SupplierCode);
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
                if (SelectedSupplier == null)
                {
                    bool isCodeUnique = await _supplierRepository.IsCodeUniqueAsync(supplierCode, 0);

                    if (!isCodeUnique)
                    {
                        MessageBox.Show(
                            $"The Supplier Code '{supplierCode}' is already in use.",
                            "Duplicate Supplier Code",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

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

                    StatusMessage = "Supplier created successfully.";
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

                    StatusMessage = "Supplier updated successfully.";
                }

                await LoadDataAsync();
                Clear();

                MessageBox.Show(
                    "Supplier saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Save Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"An error occurred while saving:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
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

            StatusMessage = "Ready for new supplier.";

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedSupplier == null)
                return;

            var result = MessageBox.Show(
                $"Delete supplier '{SelectedSupplier.SupplierName}'?\n\n" +
                "This only works if the supplier has no linked PO, GRN, supplier return, supplier ledger, or item-supplier records.\n\n" +
                "If it is already used, suspend/deactivate the supplier instead.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _supplierRepository.DeleteAsync(SelectedSupplier.Id);

                await LoadDataAsync();
                Clear();

                StatusMessage = "Supplier deleted successfully.";

                MessageBox.Show(
                    "Supplier deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this supplier from new PO/GRN screens, tick 'Suspend / Deactivate Supplier' and click SAVE.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                MessageBox.Show(
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            // Do not hit the database on every key press.
            StatusMessage = "Type search text and click SEARCH.";
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
        partial void OnPhone1Changed(string value) => SaveCommand.NotifyCanExecuteChanged();
        partial void OnDefaultCreditDaysChanged(int value) => SaveCommand.NotifyCanExecuteChanged();

        partial void OnIsBusyChanged(bool value)
        {
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy;
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

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool ValidateInput(
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
                MessageBox.Show("Supplier Code is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (supplierCode.Length > 20)
            {
                MessageBox.Show("Supplier Code cannot be longer than 20 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!SupplierCodeRegex.IsMatch(supplierCode))
            {
                MessageBox.Show(
                    "Supplier Code can only contain letters, numbers, dash, and underscore.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(supplierName))
            {
                MessageBox.Show("Supplier Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (supplierName.Length > 150)
            {
                MessageBox.Show("Supplier Name cannot be longer than 150 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (companyName.Length > 150)
            {
                MessageBox.Show("Company Name cannot be longer than 150 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (contactPerson.Length > 50)
            {
                MessageBox.Show("Contact Person cannot be longer than 50 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(phone1))
            {
                MessageBox.Show("Phone 1 is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!PhoneRegex.IsMatch(phone1))
            {
                MessageBox.Show("Phone 1 is not valid.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(phone2) && !PhoneRegex.IsMatch(phone2))
            {
                MessageBox.Show("Phone 2 is not valid.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (email.Length > 100)
                {
                    MessageBox.Show("Email cannot be longer than 100 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                try
                {
                    _ = new System.Net.Mail.MailAddress(email);
                }
                catch
                {
                    MessageBox.Show("Email address is not valid.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (address.Length > 250)
            {
                MessageBox.Show("Address cannot be longer than 250 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (hasVat && string.IsNullOrWhiteSpace(vatNumber))
            {
                MessageBox.Show("VAT number is required when 'Has VAT Number' is checked.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (vatNumber.Length > 50)
            {
                MessageBox.Show("VAT Number cannot be longer than 50 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (defaultCreditDays < 0)
            {
                MessageBox.Show("Default Credit Days cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (defaultCreditDays > 365)
            {
                MessageBox.Show("Default Credit Days cannot be greater than 365.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}