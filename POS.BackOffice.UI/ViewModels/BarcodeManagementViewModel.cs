using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BarcodeManagementViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly IBarcodePrintService _printService;

        // --- ZONE 1: FILTERS ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private bool _showActiveOnly = true;

        // --- ZONE 2: PRINT SETTINGS ---
        [ObservableProperty] private LabelSettings _printConfig = new();
        [ObservableProperty] private string _selectedPrinter = string.Empty;
        public ObservableCollection<string> AvailablePrinters { get; set; } = new();

        // --- ZONE 3: DATA COLLECTIONS & STATE ---
        [ObservableProperty] private ObservableCollection<BarcodeManagementDto> _barcodeItems = new();
        public ObservableCollection<Category> Categories { get; set; } = new();

        [ObservableProperty] private bool _isSelectAllChecked = false;
        [ObservableProperty] private int _totalItemsCount = 0;
        [ObservableProperty] private int _selectedItemsCount = 0;

        // Note: For the UI "Print Quantity" default value. We store it on the ViewModel for bulk apply, 
        // but for a real app, you might want a 'PrintQty' property on the DTO itself. 
        // For this architecture, we will default to 1 label per selected item.
        [ObservableProperty] private int _defaultPrintQuantity = 1;

        public BarcodeManagementViewModel(
            ItemMasterRepository itemRepository,
            CategoryRepository categoryRepository,
            IBarcodePrintService printService)
        {
            _itemRepository = itemRepository;
            _categoryRepository = categoryRepository;
            _printService = printService;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Load Printers
            var printers = _printService.GetInstalledPrinters();
            foreach (var p in printers) AvailablePrinters.Add(p);

            if (AvailablePrinters.Any())
            {
                SelectedPrinter = AvailablePrinters.FirstOrDefault(p => p.Contains("Label") || p.Contains("Zebra") || p.Contains("Xprinter"))
                                  ?? AvailablePrinters.First();
                PrintConfig.PrinterName = SelectedPrinter;
            }

            // Load Categories
            var categories = await _categoryRepository.GetAllAsync();
            Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --" });
            foreach (var cat in categories) Categories.Add(cat);

            await LoadDataAsync();
        }

        // --- DATA LOADING & FILTERING ---
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                int? catId = SelectedCategory?.Id != 0 ? SelectedCategory?.Id : null;

                // Fetch data using the Phase 1 Repository method
                var rawData = await _itemRepository.GetBarcodeManagementListAsync(SearchText, catId, ShowActiveOnly);

                // Reassign to trigger UI update smoothly
                BarcodeItems = new ObservableCollection<BarcodeManagementDto>(rawData);

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
            // Trigger refresh to ensure UI CheckBoxes update if virtualized
            var temp = BarcodeItems.ToList();
            BarcodeItems = new ObservableCollection<BarcodeManagementDto>(temp);
            UpdateSelectedCount();
        }

        [RelayCommand]
        public void UpdateSelectedCount()
        {
            SelectedItemsCount = BarcodeItems.Count(i => i.IsSelected);
        }

        // --- BARCODE OVERRIDE ENGINE (Inline Edit) ---
        [RelayCommand]
        private async Task UpdateSingleBarcodeAsync(BarcodeManagementDto item)
        {
            if (item == null) return;

            try
            {
                await _itemRepository.UpdateBarcodeAsync(item.VariantId, item.Barcode);
                // We don't reload the whole grid to keep the user's scroll position, just show a tiny success indicator if desired.
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                // Reload to revert the bad text the user typed
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
                    await _itemRepository.AutoGenerateBarcodesAsync(selectedItemsWithoutBarcodes);
                    MessageBox.Show("Barcodes generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDataAsync(); // Reload to show the new barcodes
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate barcodes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- PRINTING ENGINE ---
        partial void OnSelectedPrinterChanged(string value)
        {
            PrintConfig.PrinterName = value;
        }

        [RelayCommand]
        private async Task PrintSelectedLabelsAsync()
        {
            var selectedItems = BarcodeItems.Where(i => i.IsSelected).ToList();

            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select at least one item to print.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var invalidItems = selectedItems.Where(i => string.IsNullOrWhiteSpace(i.Barcode)).ToList();
            if (invalidItems.Any())
            {
                MessageBox.Show($"Cannot print labels for {invalidItems.Count} item(s) because they do not have barcodes. Please generate or enter barcodes first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Map the UI DTOs to the Print Service DTOs
                var printJobs = selectedItems.Select(i => new BarcodePrintJobItem
                {
                    Barcode = i.Barcode,
                    ItemCode = i.ItemCode,
                    ItemName = string.IsNullOrWhiteSpace(i.VariantDescription) ? i.ItemName : $"{i.ItemName} - {i.VariantDescription}",
                    Price = 0m, // Note: In Hybrid Batch Architecture, this is the Global Retail Price, not the Batch Price. You would need to add a GlobalPrice to BarcodeManagementDto if you want to print prices here.
                    PrintQuantity = DefaultPrintQuantity
                }).ToList();

                MessageBox.Show("Sending to printer...", "Processing", MessageBoxButton.OK, MessageBoxImage.Information);

                await _printService.PrintLabelsAsync(printJobs, PrintConfig);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printing failed: {ex.Message}", "Hardware Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}