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
    public partial class BarcodeManagementViewModel : ObservableObject
    {
        private readonly BarcodeManagementRepository _barcodeRepo;
        private readonly CategoryRepository _categoryRepository;

        // --- FILTERS ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private bool _showActiveOnly = true;

        // --- DATA COLLECTIONS & STATE ---
        // Explicitly pointed to POS.Core.Models.DTOs to fix the clash
        [ObservableProperty] private ObservableCollection<POS.Core.Models.DTOs.BarcodeManagementDto> _barcodeItems = new();
        public ObservableCollection<Category> Categories { get; set; } = new();

        [ObservableProperty] private bool _isSelectAllChecked = false;
        [ObservableProperty] private int _totalItemsCount = 0;
        [ObservableProperty] private int _selectedItemsCount = 0;

        public BarcodeManagementViewModel(
            BarcodeManagementRepository barcodeRepo,
            CategoryRepository categoryRepository)
        {
            _barcodeRepo = barcodeRepo;
            _categoryRepository = categoryRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --" });
            foreach (var cat in categories) Categories.Add(cat);

            SelectedCategory = Categories.FirstOrDefault();
            await LoadDataAsync();
        }

        // --- DATA LOADING & FILTERING ---
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                int? catId = SelectedCategory?.Id != 0 ? SelectedCategory?.Id : null;

                var rawData = await _barcodeRepo.GetBarcodeManagementListAsync(SearchText, catId, ShowActiveOnly);

                BarcodeItems = new ObservableCollection<POS.Core.Models.DTOs.BarcodeManagementDto>(rawData);
                TotalItemsCount = BarcodeItems.Count;
                IsSelectAllChecked = false;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load barcode data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnSelectedCategoryChanged(Category? value) => _ = LoadDataAsync();
        partial void OnShowActiveOnlyChanged(bool value) => _ = LoadDataAsync();

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault();
            ShowActiveOnly = true;
            _ = LoadDataAsync();
        }

        // --- BULK SELECTION ENGINE ---
        partial void OnIsSelectAllCheckedChanged(bool value)
        {
            foreach (var item in BarcodeItems)
            {
                item.IsSelected = value;
            }
            UpdateSelectedCount();
        }

        [RelayCommand]
        public void UpdateSelectedCount()
        {
            SelectedItemsCount = BarcodeItems.Count(i => i.IsSelected);
        }

        // --- BARCODE OVERRIDE ENGINE (Inline Edit) ---
        [RelayCommand]
        private async Task UpdateSingleBarcodeAsync(POS.Core.Models.DTOs.BarcodeManagementDto item)
        {
            if (item == null) return;

            try
            {
                await _barcodeRepo.UpdateSingleBarcodeAsync(item.VariantId, item.Barcode);
                MessageBox.Show($"Barcode saved successfully for {item.ItemCode}.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                _ = LoadDataAsync();
            }
        }

        // --- BARCODE GENERATION ENGINE ---
        [RelayCommand]
        private async Task AutoGenerateBarcodesAsync()
        {
            var selectedItemsWithoutBarcodes = BarcodeItems
                .Where(i => i.IsSelected && string.IsNullOrWhiteSpace(i.Barcode))
                .Select(i => i.VariantId)
                .ToList();

            if (!selectedItemsWithoutBarcodes.Any())
            {
                MessageBox.Show("Please select at least one item that does NOT currently have a barcode.", "No Eligible Items", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Generate system SKUs for {selectedItemsWithoutBarcodes.Count} items?", "Confirm Generation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _barcodeRepo.AutoGenerateBarcodesAsync(selectedItemsWithoutBarcodes);
                    MessageBox.Show("Barcodes generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate barcodes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}