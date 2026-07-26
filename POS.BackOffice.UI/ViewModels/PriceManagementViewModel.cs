using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PriceManagementViewModel : ObservableObject
    {
        private readonly PriceManagementRepository _repository;
        private readonly AuthService _authService;
        private readonly DispatcherTimer _searchDebounceTimer;
        private int _loadVersion;

        public ObservableCollection<PriceManagementSummaryDto> PricingItems { get; } = new();
        public ObservableCollection<PriceManagementBatchDto> ActiveBatches { get; } = new();
        public ObservableCollection<string> ItemTypeFilters { get; } = new(new[] { "All", "Stock Items", "Services" });
        public ObservableCollection<string> TrackingFilters { get; } = new(new[] { "All", "Average Cost", "Batch", "Batch + Expiry", "Service / No Stock" });
        public ObservableCollection<string> CategoryFilters { get; } = new(new[] { "All" });

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedItemTypeFilter = "All";
        [ObservableProperty] private string _selectedTrackingFilter = "All";
        [ObservableProperty] private string _selectedCategoryFilter = "All";
        [ObservableProperty] private PriceManagementSummaryDto? _selectedItem;
        [ObservableProperty] private PriceManagementBatchDto? _selectedBatch;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isBatchLoading;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private int _totalItems;
        [ObservableProperty] private int _stockItemCount;
        [ObservableProperty] private int _serviceItemCount;
        [ObservableProperty] private int _batchOverrideCount;

        public bool HasUnsavedChanges => SelectedItem?.IsDirty == true || SelectedBatch?.IsDirty == true;
        public bool CanShowBatchEditor => SelectedItem?.CanManageBatchOverrides == true;
        public bool HasSelectedItem => SelectedItem != null;
        public bool HasSelectedBatch => SelectedBatch != null;
        public bool CanRemoveSelectedOverride => SelectedBatch?.HasSellingPriceOverride == true;
        public bool IsPricingListEmpty => !IsBusy && PricingItems.Count == 0;
        public bool ShowBatchEmptyState => !IsBatchLoading && ActiveBatches.Count == 0;
        public string BatchPanelMessage => SelectedItem?.BatchEditorMessage ?? "Select an item to view pricing details.";
        public string BatchEmptyMessage
        {
            get
            {
                if (SelectedItem == null)
                    return "Select a batch-tracked stock item to view physical batches.";
                if (!CanShowBatchEditor)
                    return "Batch overrides are available only for batch-tracked stock items.";
                return "No active physical batches with available stock. Post a GRN and refresh.";
            }
        }

        public PriceManagementViewModel(
            PriceManagementRepository repository,
            AuthService authService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                await LoadDataCoreAsync();
            };
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var categories = await _repository.GetCategoryNamesAsync();
                CategoryFilters.Clear();
                CategoryFilters.Add("All");
                foreach (string category in categories)
                    CategoryFilters.Add(category);
            }
            catch
            {
                CategoryFilters.Clear();
                CategoryFilters.Add("All");
            }

            await LoadDataCoreAsync();
        }

        partial void OnSearchTextChanged(string value) => QueueReload();
        partial void OnSelectedItemTypeFilterChanged(string value) => QueueReload();
        partial void OnSelectedTrackingFilterChanged(string value) => QueueReload();
        partial void OnSelectedCategoryFilterChanged(string value) => QueueReload();

        partial void OnSelectedItemChanged(PriceManagementSummaryDto? value)
        {
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(CanShowBatchEditor));
            OnPropertyChanged(nameof(BatchPanelMessage));
            OnPropertyChanged(nameof(BatchEmptyMessage));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SelectedBatch = null;
            ActiveBatches.Clear();
            NotifyEmptyStateProperties();
            _ = LoadBatchesForSelectedItemAsync();
        }

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsPricingListEmpty));
        partial void OnIsBatchLoadingChanged(bool value) => NotifyEmptyStateProperties();

        partial void OnSelectedBatchChanged(PriceManagementBatchDto? value)
        {
            value?.ResetEditor();
            OnPropertyChanged(nameof(HasSelectedBatch));
            OnPropertyChanged(nameof(CanRemoveSelectedOverride));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void NotifyEmptyStateProperties()
        {
            OnPropertyChanged(nameof(IsPricingListEmpty));
            OnPropertyChanged(nameof(ShowBatchEmptyState));
            OnPropertyChanged(nameof(BatchEmptyMessage));
        }

        private void QueueReload()
        {
            if (IsBusy)
                return;
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        [RelayCommand]
        private async Task LoadDataAsync() => await LoadDataCoreAsync();

        private async Task LoadDataCoreAsync(int? preserveVariantId = null)
        {
            int version = ++_loadVersion;
            if (HasUnsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Unsaved price edits will be discarded. Continue?",
                    "Unsaved Pricing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            IsBusy = true;
            StatusMessage = "Loading pricing data...";
            try
            {
                int? selectedId = preserveVariantId ?? SelectedItem?.ItemVariantId;
                var rows = await _repository.GetPricingSummariesAsync(
                    trackingFilter: SelectedTrackingFilter,
                    itemTypeFilter: SelectedItemTypeFilter,
                    searchText: SearchText,
                    categoryFilter: SelectedCategoryFilter);
                if (version != _loadVersion)
                    return;

                PricingItems.Clear();
                foreach (PriceManagementSummaryDto row in rows)
                    PricingItems.Add(row);

                TotalItems = PricingItems.Count;
                StockItemCount = PricingItems.Count(item => !item.IsService);
                ServiceItemCount = PricingItems.Count(item => item.IsService);
                BatchOverrideCount = PricingItems.Sum(item => item.BatchOverrideCount);
                SelectedItem = selectedId.HasValue
                    ? PricingItems.FirstOrDefault(item => item.ItemVariantId == selectedId.Value)
                    : null;
                StatusMessage = $"Loaded {TotalItems} pricing item(s).";
                NotifySummaryProperties();
                NotifyEmptyStateProperties();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load pricing data.";
                MessageBox.Show(
                    $"Failed to load pricing data:\n\n{ex.Message}",
                    "Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadBatchesForSelectedItemAsync()
        {
            int variantId = SelectedItem?.ItemVariantId ?? 0;
            if (variantId <= 0 || SelectedItem?.CanManageBatchOverrides != true)
                return;

            IsBatchLoading = true;
            try
            {
                var rows = await _repository.GetActiveBatchPriceRowsAsync(variantId);
                if (SelectedItem?.ItemVariantId != variantId)
                    return;
                ActiveBatches.Clear();
                foreach (PriceManagementBatchDto row in rows.Where(row => row.IsOverrideEligible))
                    ActiveBatches.Add(row);
                SelectedBatch = ActiveBatches.FirstOrDefault();
                NotifyEmptyStateProperties();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load batch prices:\n\n{ex.Message}",
                    "Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBatchLoading = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedItemTypeFilter = "All";
            SelectedTrackingFilter = "All";
            SelectedCategoryFilter = "All";
            await LoadDataCoreAsync();
        }

        [RelayCommand]
        private async Task SaveMasterPricesAsync()
        {
            if (SelectedItem == null)
            {
                ShowSelectionRequired("Select an item or service first.");
                return;
            }
            if (!SelectedItem.HasMasterPriceChanged)
            {
                MessageBox.Show("The selected master prices have not changed.", "Pricing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var errors = SelectedItem.ValidateForSave();
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Price Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Save master prices for {SelectedItem.DisplayDescription}?\n\n" +
                $"Minimum: {SelectedItem.OriginalMinimumPrice:N2} → {SelectedItem.MinimumPrice:N2}\n" +
                $"Retail: {SelectedItem.OriginalRetailPrice:N2} → {SelectedItem.RetailPrice:N2}\n" +
                $"Wholesale: {SelectedItem.OriginalWholesalePrice:N2} → {SelectedItem.WholesalePrice:N2}\n" +
                $"Maximum: {SelectedItem.OriginalMaximumPrice:N2} → {SelectedItem.MaximumPrice:N2}",
                "Save Master Prices",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            try
            {
                int variantId = SelectedItem.ItemVariantId;
                await _repository.UpdateMasterPricingAsync(SelectedItem, CurrentUsername());
                StatusMessage = "Master prices saved.";
                await LoadDataCoreAsync(variantId);
                MessageBox.Show("Master prices saved successfully.", "Pricing", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Master prices were not saved:\n\n{ex.Message}", "Pricing", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ResetMasterPrices()
        {
            SelectedItem?.ResetChanges();
            NotifySummaryProperties();
        }

        [RelayCommand]
        private async Task SaveBatchOverrideAsync()
        {
            if (SelectedItem == null || SelectedBatch == null)
            {
                ShowSelectionRequired("Select a physical batch first.");
                return;
            }
            var errors = SelectedBatch.ValidateForSave();
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Batch Price Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"{SelectedBatch.OverrideActionText} for batch {SelectedBatch.BatchNo}?\n\n" +
                $"Current source: {SelectedBatch.PriceSourceText}\n" +
                $"Current Retail: {SelectedBatch.EffectiveRetailPrice:N2}\n" +
                $"New Retail: {SelectedBatch.OverrideRetailPrice:N2}\n" +
                $"Current Wholesale: {SelectedBatch.EffectiveWholesalePrice:N2}\n" +
                $"New Wholesale: {SelectedBatch.OverrideWholesalePrice:N2}",
                "Batch Price Override",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            try
            {
                int variantId = SelectedItem.ItemVariantId;
                await _repository.SetBatchPriceOverrideAsync(
                    variantId,
                    SelectedBatch.ItemBatchId,
                    SelectedBatch.OverrideRetailPrice,
                    SelectedBatch.OverrideWholesalePrice,
                    CurrentUsername());
                SelectedBatch.AcceptChanges();
                StatusMessage = "Batch price override saved.";
                await LoadDataCoreAsync(variantId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Batch price override was not saved:\n\n{ex.Message}", "Pricing", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RemoveBatchOverrideAsync()
        {
            if (SelectedItem == null || SelectedBatch == null || !SelectedBatch.HasSellingPriceOverride)
            {
                ShowSelectionRequired("Select a batch that currently has an override.");
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Remove the selling-price override from batch {SelectedBatch.BatchNo}?\n\n" +
                "The batch will immediately use the current master Retail and Wholesale prices.",
                "Remove Batch Override",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            try
            {
                int variantId = SelectedItem.ItemVariantId;
                await _repository.RemoveBatchPriceOverrideAsync(
                    variantId,
                    SelectedBatch.ItemBatchId,
                    CurrentUsername());
                SelectedBatch.AcceptChanges();
                StatusMessage = "Batch price override removed.";
                await LoadDataCoreAsync(variantId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Batch price override was not removed:\n\n{ex.Message}", "Pricing", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ResetBatchOverride() => SelectedBatch?.ResetEditor();

        private string CurrentUsername()
        {
            string username = (_authService.CurrentUser?.Username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Your authenticated user session is unavailable. Sign in again before saving pricing changes.");
            return username;
        }

        private static void ShowSelectionRequired(string message) =>
            MessageBox.Show(message, "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void NotifySummaryProperties()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
