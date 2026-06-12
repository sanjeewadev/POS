using System;
using System.Collections.ObjectModel;
using System.Linq;
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

        // --- Form Fields ---
        [ObservableProperty]
        private string _supplierCode = string.Empty;

        [ObservableProperty]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _supplierName = string.Empty;

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
        private bool _isActive = true;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        // --- Search & Collections ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<Supplier> Suppliers { get; set; } = new();

        public SupplierViewModel(SupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;
            _ = LoadSuppliersAsync();
        }

        [RelayCommand]
        private async Task LoadSuppliersAsync()
        {
            Suppliers.Clear();
            var data = await _supplierRepository.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                data = data.Where(s =>
                    s.SupplierCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.CompanyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.Phone1.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in data)
            {
                Suppliers.Add(item);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Mandatory Field Validation
            if (string.IsNullOrWhiteSpace(SupplierCode) ||
                string.IsNullOrWhiteSpace(CompanyName) ||
                string.IsNullOrWhiteSpace(SupplierName) ||
                string.IsNullOrWhiteSpace(Phone1))
            {
                MessageBox.Show("Supplier Code, Company Name, Supplier Name, and Phone 1 are mandatory.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Database Concurrency Check (Prevent Duplicate Codes)
            var existingCode = await _supplierRepository.GetByCodeAsync(SupplierCode.Trim());
            if (existingCode != null && (SelectedSupplier == null || existingCode.Id != SelectedSupplier.Id))
            {
                MessageBox.Show($"The Supplier Code '{SupplierCode}' is already assigned to {existingCode.CompanyName}. Please use a unique code.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Data Hygiene Check (Clean up inactive VAT numbers)
            if (!HasVat)
            {
                VatNumber = string.Empty;
            }

            try
            {
                if (SelectedSupplier == null)
                {
                    // Create New Supplier
                    var newSupplier = new Supplier
                    {
                        SupplierCode = this.SupplierCode.Trim(),
                        CompanyName = this.CompanyName.Trim(),
                        SupplierName = this.SupplierName.Trim(),
                        Phone1 = this.Phone1.Trim(),
                        Phone2 = this.Phone2?.Trim(),
                        Email = this.Email?.Trim(),
                        Address = this.Address?.Trim(),
                        HasVat = this.HasVat,
                        VatNumber = this.VatNumber?.Trim(),
                        IsActive = this.IsActive
                    };
                    await _supplierRepository.AddAsync(newSupplier);
                }
                else
                {
                    // Update Existing Supplier
                    SelectedSupplier.SupplierCode = this.SupplierCode.Trim();
                    SelectedSupplier.CompanyName = this.CompanyName.Trim();
                    SelectedSupplier.SupplierName = this.SupplierName.Trim();
                    SelectedSupplier.Phone1 = this.Phone1.Trim();
                    SelectedSupplier.Phone2 = this.Phone2?.Trim();
                    SelectedSupplier.Email = this.Email?.Trim();
                    SelectedSupplier.Address = this.Address?.Trim();
                    SelectedSupplier.HasVat = this.HasVat;
                    SelectedSupplier.VatNumber = this.VatNumber?.Trim();
                    SelectedSupplier.IsActive = this.IsActive;

                    await _supplierRepository.UpdateAsync(SelectedSupplier);
                }

                await LoadSuppliersAsync();
                Clear();
                MessageBox.Show("Supplier profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A database error occurred: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SupplierCode = string.Empty;
            CompanyName = string.Empty;
            SupplierName = string.Empty;
            Phone1 = string.Empty;
            Phone2 = string.Empty;
            Email = string.Empty;
            Address = string.Empty;
            HasVat = false;
            VatNumber = string.Empty;
            IsActive = true;
            SelectedSupplier = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedSupplier == null) return;

            var result = MessageBox.Show($"Are you sure you want to deactivate {SelectedSupplier.CompanyName}? This will not delete historical transactions.", "Confirm Soft Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _supplierRepository.SoftDeleteAsync(SelectedSupplier.Id);
                await LoadSuppliersAsync();
                Clear();
            }
        }

        // --- Auto-Triggers ---

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadSuppliersAsync();
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null)
            {
                SupplierCode = value.SupplierCode;
                CompanyName = value.CompanyName;
                SupplierName = value.SupplierName;
                Phone1 = value.Phone1;
                Phone2 = value.Phone2 ?? string.Empty;
                Email = value.Email ?? string.Empty;
                Address = value.Address ?? string.Empty;
                HasVat = value.HasVat;
                VatNumber = value.VatNumber ?? string.Empty;
                IsActive = value.IsActive;
            }
        }
    }
}