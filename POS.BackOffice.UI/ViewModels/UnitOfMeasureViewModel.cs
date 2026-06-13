using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class UnitOfMeasureViewModel : ViewModelBase
    {
        private readonly UnitOfMeasureRepository _uomRepository;

        // --- Form Fields ---
        [ObservableProperty]
        private string _uomCode = string.Empty;

        [ObservableProperty]
        private string _uomDescription = string.Empty;

        [ObservableProperty]
        private bool _allowDecimals = false;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private UnitOfMeasure? _selectedUom;

        // --- Filter Fields ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        // --- Data Collection ---
        public ObservableCollection<UnitOfMeasure> Uoms { get; set; } = new();

        public UnitOfMeasureViewModel(UnitOfMeasureRepository uomRepository)
        {
            _uomRepository = uomRepository;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                Uoms.Clear();
                var data = await _uomRepository.GetAllAsync(SearchText);

                foreach (var item in data)
                {
                    Uoms.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load UOMs: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Mandatory Validation
            if (string.IsNullOrWhiteSpace(UomCode) || string.IsNullOrWhiteSpace(UomDescription))
            {
                MessageBox.Show("UOM Code and Description are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Duplicate Code Check
                int currentId = SelectedUom?.Id ?? 0;
                bool isUnique = await _uomRepository.IsCodeUniqueAsync(UomCode, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The UOM Code '{UomCode}' is already in use.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Save or Update
                if (SelectedUom == null)
                {
                    var newUom = new UnitOfMeasure
                    {
                        UomCode = this.UomCode.Trim().ToUpper(), // Force Codes to Uppercase
                        UomDescription = this.UomDescription.Trim(),
                        AllowDecimals = this.AllowDecimals,
                        IsActive = this.IsActive,
                        CreatedAt = DateTime.Now
                    };
                    await _uomRepository.AddAsync(newUom);
                }
                else
                {
                    SelectedUom.UomCode = this.UomCode.Trim().ToUpper();
                    SelectedUom.UomDescription = this.UomDescription.Trim();
                    SelectedUom.AllowDecimals = this.AllowDecimals;
                    SelectedUom.IsActive = this.IsActive;

                    await _uomRepository.UpdateAsync(SelectedUom);
                }

                await LoadDataAsync();
                Clear();
                MessageBox.Show("Unit of Measure saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            UomCode = string.Empty;
            UomDescription = string.Empty;
            AllowDecimals = false;
            IsActive = true;
            SelectedUom = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedUom == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{SelectedUom.UomCode}'?",
                                         "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _uomRepository.DeleteAsync(SelectedUom.Id);
                    await LoadDataAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    // Defensive Architecture: Catching Foreign Key Violations
                    MessageBox.Show("This Unit of Measure cannot be deleted because it is currently assigned to items in your inventory. \n\nPlease uncheck 'Unit is Active' instead.",
                                    "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- AUTO-TRIGGERS ---
        partial void OnSearchTextChanged(string value)
        {
            _ = LoadDataAsync();
        }

        partial void OnSelectedUomChanged(UnitOfMeasure? value)
        {
            if (value != null)
            {
                UomCode = value.UomCode ?? string.Empty;
                UomDescription = value.UomDescription ?? string.Empty;
                AllowDecimals = value.AllowDecimals;
                IsActive = value.IsActive;
            }
        }
    }
}