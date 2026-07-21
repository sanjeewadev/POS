using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PriceManagementViewModel : ObservableObject
    {
        private readonly PriceManagementRepository _repository;
        private readonly DispatcherTimer _searchDebounceTimer;

        private int _loadVersion = 0;

        // =========================================================
        // FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedMarginFilter = "All";

        public ObservableCollection<string> MarginFilters { get; } = new(new[]
        {
            "All",
            "Low Margin Alerts (< 20%)",
            "Healthy Margins"
        });

        [ObservableProperty]
        private string _selectedItemTypeFilter = "All";

        public ObservableCollection<string> ItemTypeFilters { get; } = new(new[]
        {
            "All",
            "Stock Items",
            "Services"
        });

        [ObservableProperty]
        private string _selectedTrackingFilter = "All";

        public ObservableCollection<string> TrackingFilters { get; } = new(new[]
        {
            "All",
            "Average Cost",
            "Batch",
            "Batch + Expiry",
            "Service / No Stock"
        });

        // Kept only so old XAML bindings will not fail if an older view is still loaded.
        [ObservableProperty]
        private string _selectedExpiryFilter = "All";

        public ObservableCollection<string> ExpiryFilters { get; } = new(new[]
        {
            "All",
            "Expiring Soon"
        });

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<PriceManagementSummaryDto> PricingItems { get; } = new();

        // Kept for compatibility. The simplified page no longer edits batch rows separately.
        public ObservableCollection<PriceManagementBatchDto> ActiveBatches { get; } = new();

        // =========================================================
        // SELECTION
        // =========================================================

        [ObservableProperty]
        private PriceManagementSummaryDto? _selectedItem;

        // =========================================================
        // SAVE OPTIONS
        // =========================================================

        [ObservableProperty]
        private string _changeReason = string.Empty;

        [ObservableProperty]
        private bool _applySellingPriceToCurrentStock = true;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private bool _isBatchPanelVisible = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _totalItems = 0;

        [ObservableProperty]
        private int _dirtyItemCount = 0;

        [ObservableProperty]
        private int _dirtyBatchCount = 0;

        [ObservableProperty]
        private int _stockItemCount = 0;

        [ObservableProperty]
        private int _serviceItemCount = 0;

        [ObservableProperty]
        private int _averageCostItemCount = 0;

        [ObservableProperty]
        private int _batchTrackedItemCount = 0;

        [ObservableProperty]
        private int _batchExpiryItemCount = 0;

        public bool HasUnsavedChanges =>
            PricingItems.Any(i => i.IsDirty);

        public PriceManagementViewModel(PriceManagementRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };

            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                await LoadDataAsync();
            };

            _ = LoadDataAsync();
        }

        // =========================================================
        // FILTER CHANGE EVENTS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            QueueReload();
        }

        partial void OnSelectedMarginFilterChanged(string value)
        {
            QueueReload();
        }

        partial void OnSelectedItemTypeFilterChanged(string value)
        {
            QueueReload();
        }

        partial void OnSelectedTrackingFilterChanged(string value)
        {
            QueueReload();
        }

        partial void OnSelectedExpiryFilterChanged(string value)
        {
            // Old filter kept for compatibility. It does not control the simplified grid.
        }

        private void QueueReload()
        {
            if (IsBusy)
                return;

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        // =========================================================
        // LOAD MAIN GRID
        // =========================================================

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            int version = ++_loadVersion;

            if (HasUnsavedChanges)
            {
                var confirm = MessageBox.Show(
                    "There are unsaved price changes. Reloading will discard them.\n\nContinue?",
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
            StatusMessage = "Loading pricing data...";

            try
            {
                var rows = await _repository.GetPricingSummariesAsync(
                    SelectedMarginFilter,
                    SelectedTrackingFilter,
                    SelectedItemTypeFilter,
                    SearchText);

                if (version != _loadVersion)
                    return;

                UnsubscribePricingItemEvents();

                PricingItems.Clear();
                ActiveBatches.Clear();

                foreach (var row in rows)
                {
                    row.PropertyChanged += PricingItem_PropertyChanged;
                    PricingItems.Add(row);
                }

                SelectedItem = null;

                TotalItems = PricingItems.Count;
                StockItemCount = PricingItems.Count(i => !i.IsService);
                ServiceItemCount = PricingItems.Count(i => i.IsService);
                AverageCostItemCount = PricingItems.Count(i => !i.IsService && !i.HasBatchTracking);
                BatchTrackedItemCount = PricingItems.Count(i => i.HasBatchTracking);
                BatchExpiryItemCount = PricingItems.Count(i => i.HasBatchTracking && i.HasExpiryTracking);

                RefreshDirtyCounters();

                StatusMessage = $"Loaded {TotalItems} pricing item(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load pricing data.";

                MessageBox.Show(
                    $"Failed to load pricing data:\n\n{ex.Message}",
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
            SelectedMarginFilter = "All";
            SelectedItemTypeFilter = "All";
            SelectedTrackingFilter = "All";
            SelectedExpiryFilter = "All";

            await LoadDataAsync();
        }

        // =========================================================
        // SAVE
        // =========================================================

        [RelayCommand]
        private async Task SaveAllDirtyAsync()
        {
            var dirtyRows = PricingItems
                .Where(i => i.IsDirty)
                .ToList();

            if (!dirtyRows.Any())
            {
                MessageBox.Show(
                    "There are no price changes to save.",
                    "No Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string reason = (ChangeReason ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show(
                    "Enter a price change reason before saving.",
                    "Reason Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var validationErrors = dirtyRows
                .SelectMany(i => i.ValidateForSave())
                .ToList();

            if (validationErrors.Any())
            {
                MessageBox.Show(
                    "Cannot save pricing because validation failed:\n\n" +
                    string.Join("\n", validationErrors),
                    "Price Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            int negativeMarginCount = dirtyRows.Count(i => i.IsNegativeMargin);
            int lowMarginCount = dirtyRows.Count(i => i.IsLowMargin);

            if (negativeMarginCount > 0 || lowMarginCount > 0)
            {
                var warning = MessageBox.Show(
                    $"Some changed prices have margin warnings.\n\n" +
                    $"Negative margin rows: {negativeMarginCount}\n" +
                    $"Low margin rows: {lowMarginCount}\n\n" +
                    "Save anyway?",
                    "Margin Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warning != MessageBoxResult.Yes)
                    return;
            }

            int serviceChangeCount = dirtyRows.Count(i => i.IsService);

            string stockSyncText = ApplySellingPriceToCurrentStock
                ? "Current active stock selling prices will also be updated for Stock Items only. Service rows always update master prices only."
                : "Only master prices will be updated.";

            var confirm = MessageBox.Show(
                $"Save changed price rows?\n\n" +
                $"Changed items: {dirtyRows.Count}\n" +
                $"Changed services: {serviceChangeCount}\n\n" +
                $"{stockSyncText}",
                "Save Price Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Saving changed price rows...";

            try
            {
                foreach (var item in dirtyRows)
                {
                    await _repository.UpdatePricingAsync(
                        item,
                        "Admin",
                        reason,
                        ApplySellingPriceToCurrentStock);

                    item.AcceptChanges();
                }

                RefreshDirtyCounters();

                StatusMessage = $"Saved {dirtyRows.Count} price change(s).";

                MessageBox.Show(
                    "Price changes saved successfully.",
                    "Save Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save price changes failed.";

                MessageBox.Show(
                    $"Failed to save price changes:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Compatibility command. Old XAML may still call this.
        [RelayCommand]
        private async Task SaveAdjustmentsAsync()
        {
            if (SelectedItem == null)
            {
                MessageBox.Show(
                    "Select an item first.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!SelectedItem.IsDirty)
            {
                MessageBox.Show(
                    "The selected item has no price changes.",
                    "No Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            await SaveAllDirtyAsync();
        }

        // Compatibility command. The simplified page does not use the batch panel.
        [RelayCommand]
        private void CloseBatchPanel()
        {
            ActiveBatches.Clear();
            IsBatchPanelVisible = false;
        }

        // =========================================================
        // EVENTS / DIRTY TRACKING
        // =========================================================

        private void PricingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PriceManagementSummaryDto.IsDirty))
                RefreshDirtyCounters();
        }

        private void RefreshDirtyCounters()
        {
            DirtyItemCount = PricingItems.Count(i => i.IsDirty);
            DirtyBatchCount = 0;

            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void UnsubscribePricingItemEvents()
        {
            foreach (var item in PricingItems)
                item.PropertyChanged -= PricingItem_PropertyChanged;
        }
    }
}
