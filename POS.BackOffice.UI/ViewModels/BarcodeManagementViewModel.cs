using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BarcodeManagementViewModel : ObservableObject
    {
        private readonly BarcodeManagementRepository _barcodeRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly DispatcherTimer _searchDebounceTimer;

        private bool _isBulkSelectionChanging = false;
        private bool _isUpdatingSelectAllFromItems = false;

        // =========================================================
        // FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private bool _showActiveOnly = true;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<BarcodeManagementDto> BarcodeItems { get; } = new();

        public ObservableCollection<Category> Categories { get; } = new();

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private bool _isSelectAllChecked = false;

        [ObservableProperty]
        private int _totalItemsCount = 0;

        [ObservableProperty]
        private int _selectedItemsCount = 0;

        [ObservableProperty]
        private int _dirtyItemsCount = 0;

        public bool HasUnsavedBarcodeChanges => BarcodeItems.Any(i => i.IsDirty);

        public BarcodeManagementViewModel(
            BarcodeManagementRepository barcodeRepository,
            CategoryRepository categoryRepository)
        {
            _barcodeRepository = barcodeRepository;
            _categoryRepository = categoryRepository;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };

            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                await LoadDataAsync();
            };

            _ = InitializeAsync();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        private async Task InitializeAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading barcode management page...";

            try
            {
                Categories.Clear();

                Categories.Add(new Category
                {
                    Id = 0,
                    CategoryName = "-- ALL CATEGORIES --"
                });

                var categories = await _categoryRepository.GetAllAsync();

                foreach (var category in categories.OrderBy(c => c.CategoryName))
                    Categories.Add(category);

                SelectedCategory = Categories.FirstOrDefault();

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize barcode management page.";

                MessageBox.Show(
                    $"Failed to initialize barcode management page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // FILTER CHANGE EVENTS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            QueueSearchReload();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            _ = LoadDataAsync();
        }

        partial void OnShowActiveOnlyChanged(bool value)
        {
            _ = LoadDataAsync();
        }

        private void QueueSearchReload()
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        // =========================================================
        // LOAD DATA
        // =========================================================

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (HasUnsavedBarcodeChanges)
            {
                var confirm = MessageBox.Show(
                    "There are unsaved barcode changes. Reloading will discard them.\n\nContinue?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    StatusMessage = "Reload cancelled because there are unsaved changes.";
                    return;
                }
            }

            IsBusy = true;
            StatusMessage = "Loading barcode data...";

            try
            {
                UnsubscribeItemEvents();

                BarcodeItems.Clear();

                int? categoryId = SelectedCategory != null && SelectedCategory.Id > 0
                    ? SelectedCategory.Id
                    : null;

                var rows = await _barcodeRepository.GetBarcodeManagementListAsync(
                    SearchText,
                    categoryId,
                    ShowActiveOnly);

                foreach (var row in rows)
                {
                    row.PropertyChanged += BarcodeItem_PropertyChanged;
                    BarcodeItems.Add(row);
                }

                TotalItemsCount = BarcodeItems.Count;

                _isUpdatingSelectAllFromItems = true;
                IsSelectAllChecked = false;
                _isUpdatingSelectAllFromItems = false;

                RefreshCounters();

                StatusMessage = $"Loaded {TotalItemsCount} barcode item(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load barcode data.";

                MessageBox.Show(
                    $"Failed to load barcode data:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault();
            ShowActiveOnly = true;

            await LoadDataAsync();
        }

        // =========================================================
        // SELECTION
        // =========================================================

        partial void OnIsSelectAllCheckedChanged(bool value)
        {
            if (_isUpdatingSelectAllFromItems)
                return;

            _isBulkSelectionChanging = true;

            foreach (var item in BarcodeItems)
                item.IsSelected = value;

            _isBulkSelectionChanging = false;

            RefreshCounters();
        }

        [RelayCommand]
        public void UpdateSelectedCount()
        {
            RefreshCounters();
        }

        private void RefreshCounters()
        {
            SelectedItemsCount = BarcodeItems.Count(i => i.IsSelected);
            DirtyItemsCount = BarcodeItems.Count(i => i.IsDirty);

            _isUpdatingSelectAllFromItems = true;
            IsSelectAllChecked = BarcodeItems.Any() && SelectedItemsCount == BarcodeItems.Count;
            _isUpdatingSelectAllFromItems = false;

            OnPropertyChanged(nameof(HasUnsavedBarcodeChanges));
        }

        // =========================================================
        // SAVE SINGLE BARCODE
        // =========================================================

        [RelayCommand]
        private async Task UpdateSingleBarcodeAsync(BarcodeManagementDto? item)
        {
            if (item == null)
                return;

            if (!item.IsActive)
            {
                MessageBox.Show(
                    "Cannot update barcode for an inactive item.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = $"Saving barcode for {item.ItemCode}...";

            try
            {
                await _barcodeRepository.UpdateSingleBarcodeAsync(
                    item.VariantId,
                    item.Barcode);

                item.AcceptChanges();

                RefreshCounters();

                StatusMessage = $"Barcode saved for {item.ItemCode}.";

                MessageBox.Show(
                    $"Barcode saved successfully for {item.ItemCode}.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                item.RejectChanges();

                StatusMessage = "Barcode save failed.";

                MessageBox.Show(
                    ex.Message,
                    "Barcode Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // SAVE ALL DIRTY MANUAL EDITS
        // =========================================================

        [RelayCommand]
        private async Task SaveAllDirtyBarcodesAsync()
        {
            var dirtyItems = BarcodeItems
                .Where(i => i.IsDirty)
                .ToList();

            if (!dirtyItems.Any())
            {
                MessageBox.Show(
                    "There are no barcode changes to save.",
                    "No Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Save {dirtyItems.Count} changed barcode(s)?",
                "Save Barcode Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Saving changed barcodes...";

            try
            {
                foreach (var item in dirtyItems)
                {
                    if (!item.IsActive)
                        throw new InvalidOperationException($"Cannot update inactive item: {item.ItemCode}");

                    await _barcodeRepository.UpdateSingleBarcodeAsync(
                        item.VariantId,
                        item.Barcode);

                    item.AcceptChanges();
                }

                RefreshCounters();

                StatusMessage = "All changed barcodes were saved.";

                MessageBox.Show(
                    "All changed barcodes were saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Saving changed barcodes failed.";

                MessageBox.Show(
                    $"Failed to save changed barcodes:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await LoadDataAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // AUTO-GENERATE INTERNAL EAN-13 BARCODES
        // =========================================================

        [RelayCommand]
        private async Task AutoGenerateBarcodesAsync()
        {
            var selectedEligibleItems = BarcodeItems
                .Where(i =>
                    i.IsSelected &&
                    i.IsActive &&
                    string.IsNullOrWhiteSpace(i.Barcode))
                .ToList();

            if (!selectedEligibleItems.Any())
            {
                MessageBox.Show(
                    "Please select at least one active item that does not currently have a barcode.",
                    "No Eligible Items",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Generate valid internal EAN-13 barcodes for {selectedEligibleItems.Count} selected item(s)?",
                "Confirm Barcode Generation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Generating internal EAN-13 barcodes...";

            try
            {
                var variantIds = selectedEligibleItems
                    .Select(i => i.VariantId)
                    .ToList();

                var result = await _barcodeRepository.AutoGenerateBarcodesAsync(variantIds);

                StatusMessage = result.Message;

                MessageBox.Show(
                    result.Message,
                    "Barcode Generation Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Barcode generation failed.";

                MessageBox.Show(
                    $"Failed to generate barcodes:\n\n{ex.Message}",
                    "Barcode Generation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // EVENTS
        // =========================================================

        private void BarcodeItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isBulkSelectionChanging)
                return;

            if (e.PropertyName == nameof(BarcodeManagementDto.IsSelected) ||
                e.PropertyName == nameof(BarcodeManagementDto.IsDirty))
            {
                RefreshCounters();
            }
        }

        private void UnsubscribeItemEvents()
        {
            foreach (var item in BarcodeItems)
                item.PropertyChanged -= BarcodeItem_PropertyChanged;
        }
    }
}