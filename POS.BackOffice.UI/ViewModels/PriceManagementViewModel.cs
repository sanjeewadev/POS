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

        private PriceManagementSummaryDto? _activeBatchItem;
        private bool _isRollingBackSelection = false;
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

        public ObservableCollection<PriceManagementBatchDto> ActiveBatches { get; } = new();

        // =========================================================
        // SELECTION
        // =========================================================

        [ObservableProperty]
        private PriceManagementSummaryDto? _selectedItem;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBatchPanelVisible = false;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _totalItems = 0;

        [ObservableProperty]
        private int _dirtyItemCount = 0;

        [ObservableProperty]
        private int _dirtyBatchCount = 0;

        public bool HasUnsavedChanges =>
            PricingItems.Any(i => i.IsDirty) || ActiveBatches.Any(b => b.IsDirty);

        public PriceManagementViewModel(PriceManagementRepository repository)
        {
            _repository = repository;

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

        partial void OnSelectedExpiryFilterChanged(string value)
        {
            QueueReload();
        }

        private void QueueReload()
        {
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
                    SelectedExpiryFilter,
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
                _activeBatchItem = null;
                IsBatchPanelVisible = false;

                TotalItems = PricingItems.Count;
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
            SelectedExpiryFilter = "All";

            await LoadDataAsync();
        }

        // =========================================================
        // SELECTION / BATCH INSPECTOR
        // =========================================================

        partial void OnSelectedItemChanged(PriceManagementSummaryDto? value)
        {
            if (_isRollingBackSelection)
                return;

            if (value == null)
                return;

            if (_activeBatchItem != null &&
                _activeBatchItem.ItemVariantId != value.ItemVariantId &&
                ActiveBatches.Any(b => b.IsDirty))
            {
                var confirm = MessageBox.Show(
                    "The current batch panel has unsaved changes. Changing item will discard those batch changes.\n\nContinue?",
                    "Unsaved Batch Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    _isRollingBackSelection = true;
                    SelectedItem = _activeBatchItem;
                    _isRollingBackSelection = false;
                    return;
                }
            }

            _ = OpenBatchInspectorAsync(value);
        }

        private async Task OpenBatchInspectorAsync(PriceManagementSummaryDto item)
        {
            if (item.ItemVariantId <= 0)
                return;

            IsBusy = true;
            StatusMessage = $"Loading batches for {item.ItemCode}...";

            try
            {
                UnsubscribeBatchEvents();
                ActiveBatches.Clear();

                var batches = await _repository.GetActiveBatchPriceRowsAsync(item.ItemVariantId);

                if (SelectedItem == null || SelectedItem.ItemVariantId != item.ItemVariantId)
                    return;

                foreach (var batch in batches)
                {
                    batch.PropertyChanged += Batch_PropertyChanged;
                    ActiveBatches.Add(batch);
                }

                _activeBatchItem = item;
                IsBatchPanelVisible = true;

                RefreshDirtyCounters();

                StatusMessage = batches.Any()
                    ? $"Loaded {batches.Count} active batch(es) for {item.ItemCode}."
                    : $"No active batches found for {item.ItemCode}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load batch pricing data.";

                MessageBox.Show(
                    $"Failed to load batch breakdown:\n\n{ex.Message}",
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
        private void CloseBatchPanel()
        {
            if (ActiveBatches.Any(b => b.IsDirty))
            {
                var confirm = MessageBox.Show(
                    "There are unsaved batch price changes. Close the batch panel and discard them?",
                    "Unsaved Batch Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            UnsubscribeBatchEvents();

            ActiveBatches.Clear();
            IsBatchPanelVisible = false;
            _activeBatchItem = null;

            _isRollingBackSelection = true;
            SelectedItem = null;
            _isRollingBackSelection = false;

            RefreshDirtyCounters();

            StatusMessage = "Batch panel closed.";
        }

        // =========================================================
        // SAVE CURRENT SELECTED ITEM
        // =========================================================

        [RelayCommand]
        private async Task SaveAdjustmentsAsync()
        {
            if (SelectedItem == null)
            {
                MessageBox.Show(
                    "Please select an item before saving pricing adjustments.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var validationErrors = SelectedItem.ValidateForSave();

            foreach (var batch in ActiveBatches)
                validationErrors.AddRange(batch.ValidateForSave());

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

            if (SelectedItem.IsNegativeMargin)
            {
                var warning = MessageBox.Show(
                    $"Retail price for '{SelectedItem.ItemCode}' is lower than cost.\n\n" +
                    $"Margin: {SelectedItem.GrossMarginPercentage:N2}%\n\n" +
                    "Save anyway?",
                    "Negative Margin Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warning != MessageBoxResult.Yes)
                    return;
            }
            else if (SelectedItem.IsLowMargin)
            {
                var warning = MessageBox.Show(
                    $"Retail margin for '{SelectedItem.ItemCode}' is below 20%.\n\n" +
                    $"Margin: {SelectedItem.GrossMarginPercentage:N2}%\n\n" +
                    "Save anyway?",
                    "Low Margin Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warning != MessageBoxResult.Yes)
                    return;
            }

            IsBusy = true;
            StatusMessage = $"Saving pricing for {SelectedItem.ItemCode}...";

            try
            {
                await _repository.UpdatePricingAsync(
                    SelectedItem,
                    ActiveBatches.ToList(),
                    "Admin");

                foreach (var batch in ActiveBatches)
                    batch.AcceptChanges();

                SelectedItem.AcceptChanges();

                RefreshDirtyCounters();

                StatusMessage = $"Pricing updated for {SelectedItem.ItemCode}.";

                MessageBox.Show(
                    $"Pricing updated successfully for {SelectedItem.ItemCode}.",
                    "Save Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to save pricing.";

                MessageBox.Show(
                    $"Failed to save pricing:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // SAVE ALL DIRTY MASTER ROWS
        // =========================================================

        [RelayCommand]
        private async Task SaveAllDirtyAsync()
        {
            var dirtyRows = PricingItems
                .Where(i => i.IsDirty)
                .ToList();

            if (!dirtyRows.Any() && !ActiveBatches.Any(b => b.IsDirty))
            {
                MessageBox.Show(
                    "There are no price changes to save.",
                    "No Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Save all changed master price rows?\n\nChanged items: {dirtyRows.Count}\nChanged active batches: {ActiveBatches.Count(b => b.IsDirty)}",
                "Save All Price Changes",
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
                    var errors = item.ValidateForSave();

                    if (errors.Any())
                    {
                        throw new InvalidOperationException(
                            $"Validation failed for item '{item.ItemCode}':\n" +
                            string.Join("\n", errors));
                    }

                    if (SelectedItem != null &&
                        item.ItemVariantId == SelectedItem.ItemVariantId)
                    {
                        await _repository.UpdatePricingAsync(
                            item,
                            ActiveBatches.ToList(),
                            "Admin");
                    }
                    else
                    {
                        await _repository.UpdatePricingAsync(
                            item,
                            new System.Collections.Generic.List<PriceManagementBatchDto>(),
                            "Admin");
                    }

                    item.AcceptChanges();
                }

                foreach (var batch in ActiveBatches)
                    batch.AcceptChanges();

                RefreshDirtyCounters();

                StatusMessage = "All changed price rows saved.";

                MessageBox.Show(
                    "All changed prices were saved successfully.",
                    "Save Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save all price changes failed.";

                MessageBox.Show(
                    $"Failed to save all price changes:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // EVENTS / DIRTY TRACKING
        // =========================================================

        private void PricingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PriceManagementSummaryDto.IsDirty))
                RefreshDirtyCounters();
        }

        private void Batch_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PriceManagementBatchDto.IsDirty))
                RefreshDirtyCounters();
        }

        private void RefreshDirtyCounters()
        {
            DirtyItemCount = PricingItems.Count(i => i.IsDirty);
            DirtyBatchCount = ActiveBatches.Count(b => b.IsDirty);

            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void UnsubscribePricingItemEvents()
        {
            foreach (var item in PricingItems)
                item.PropertyChanged -= PricingItem_PropertyChanged;
        }

        private void UnsubscribeBatchEvents()
        {
            foreach (var batch in ActiveBatches)
                batch.PropertyChanged -= Batch_PropertyChanged;
        }
    }
}