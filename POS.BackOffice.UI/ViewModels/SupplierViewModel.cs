using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierViewModel : ViewModelBase
    {
        private readonly SupplierRepository _supplierRepository;

        // --- Identity Fields ---
        [ObservableProperty]
        private string _supplierCode = string.Empty;

        [ObservableProperty]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _contactPerson = string.Empty;

        // --- Contact Fields ---
        [ObservableProperty]
        private string _phone1 = string.Empty;

        [ObservableProperty]
        private string _phone2 = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        // --- Financial Fields ---
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

        // --- State & Collections ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        public ObservableCollection<Supplier> Suppliers { get; set; } = new();

        public SupplierViewModel(SupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                Suppliers.Clear();
                var data = await _supplierRepository.GetAllFilteredAsync(SearchText);

                foreach (var item in data)
                {
                    Suppliers.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load suppliers: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            SearchText = string.Empty;
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Mandatory Validation
            if (string.IsNullOrWhiteSpace(SupplierCode) || string.IsNullOrWhiteSpace(SupplierName) || string.IsNullOrWhiteSpace(Phone1))
            {
                MessageBox.Show("Supplier Code, Supplier Name, and Phone 1 are mandatory fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Duplicate Code Check
                int currentId = SelectedSupplier?.Id ?? 0;
                bool isUnique = await _supplierRepository.IsCodeUniqueAsync(SupplierCode, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The Supplier Code '{SupplierCode}' is already in use.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Setup Entity
                if (SelectedSupplier == null)
                {
                    var newSupplier = new Supplier
                    {
                        SupplierCode = this.SupplierCode.Trim().ToUpper(),
                        SupplierName = this.SupplierName.Trim(),
                        CompanyName = this.CompanyName.Trim(),
                        ContactPerson = this.ContactPerson.Trim(),
                        Phone1 = this.Phone1.Trim(),
                        Phone2 = this.Phone2.Trim(),
                        Email = this.Email.Trim(),
                        Address = this.Address.Trim(),
                        HasVat = this.HasVat,
                        VatNumber = this.HasVat ? this.VatNumber.Trim() : string.Empty, // Clear VAT if unchecked
                        DefaultCreditDays = this.DefaultCreditDays,
                        CurrentBalance = 0m, // New suppliers always start at 0
                        IsDeactivated = this.IsDeactivated,
                        CreatedAt = DateTime.Now
                    };
                    await _supplierRepository.AddAsync(newSupplier);
                }
                else
                {
                    SelectedSupplier.SupplierCode = this.SupplierCode.Trim().ToUpper();
                    SelectedSupplier.SupplierName = this.SupplierName.Trim();
                    SelectedSupplier.CompanyName = this.CompanyName.Trim();
                    SelectedSupplier.ContactPerson = this.ContactPerson.Trim();
                    SelectedSupplier.Phone1 = this.Phone1.Trim();
                    SelectedSupplier.Phone2 = this.Phone2.Trim();
                    SelectedSupplier.Email = this.Email.Trim();
                    SelectedSupplier.Address = this.Address.Trim();
                    SelectedSupplier.HasVat = this.HasVat;
                    SelectedSupplier.VatNumber = this.HasVat ? this.VatNumber.Trim() : string.Empty;
                    SelectedSupplier.DefaultCreditDays = this.DefaultCreditDays;
                    SelectedSupplier.IsDeactivated = this.IsDeactivated;

                    await _supplierRepository.UpdateAsync(SelectedSupplier);
                }

                await LoadDataAsync();
                Clear();
                MessageBox.Show("Supplier saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
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
            DefaultCreditDays = 30; // Reset to standard default
            CurrentBalance = 0m;
            IsDeactivated = false;
            SelectedSupplier = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedSupplier == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{SelectedSupplier.SupplierName}'?",
                                         "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _supplierRepository.DeleteAsync(SelectedSupplier.Id);
                    await LoadDataAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    // Defensive Architecture: Catching Foreign Key Violations
                    MessageBox.Show("This supplier cannot be deleted because they are linked to existing Purchase Orders or GRNs. \n\nPlease Suspend them instead.",
                                    "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Fills the UI fields when a user clicks a row in the DataGrid
        partial void OnSelectedSupplierChanged(Supplier? value)
        {
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
            }
        }
    }
}