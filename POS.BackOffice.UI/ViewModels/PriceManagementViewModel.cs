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

        public bool HasUnsavedChanges => SelectedBatch?.IsDirty == true;
        public bool CanShowBatchEditor => SelectedItem?.CanManageBatchOverrides == true;
        public bool HasSelectedItem => SelectedItem != null;
        public bool HasSelectedBatch => SelectedBatch != null;
        public bool CanUseMasterPrice => SelectedBatch?.HasSellingPriceOverride == true;
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
            OnPropertyChanged(nameof(CanUseMasterPrice));
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

        public async Task ApplyMasterPriceChangeAsync(MasterPriceQuickChangeResult result)
        {
            if (SelectedItem == null || SelectedItem.ItemVariantId != result.ItemVariantId)
            {
                ShowSelectionRequired("The selected item changed. Reopen Quick Change Master Price.");
                return;
            }

            var pricing = new PriceManagementSummaryDto
            {
                ItemVariantId = SelectedItem.ItemVariantId,
                ItemParentId = SelectedItem.ItemParentId,
                ItemCode = SelectedItem.ItemCode,
                SkuCode = SelectedItem.SkuCode,
                Barcode = SelectedItem.Barcode,
                Description = SelectedItem.Description,
                VariantAttributes = SelectedItem.VariantAttributes,
                ItemType = SelectedItem.ItemType,
                MinimumPrice = result.MinimumPrice,
                RetailPrice = result.RetailPrice,
                WholesalePrice = result.WholesalePrice,
                MaximumPrice = result.MaximumPrice
            };

            IsBusy = true;
            try
            {
                int variantId = result.ItemVariantId;
                await _repository.UpdateMasterPricingAsync(pricing, CurrentUsername());
                StatusMessage = "Master prices saved.";
                await LoadDataCoreAsync(variantId);
                MessageBox.Show(
                    "Master prices saved successfully. The change is available in Price Change History.",
                    "Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Master prices were not saved:\n\n{ex.Message}",
                    "Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveBatchOverrideAsync()
        {
            if (SelectedItem == null || SelectedBatch == null)
            {
                ShowSelectionRequired("Select a physical batch first.");
                return;
            }
            if (!SelectedBatch.HasBatchPriceChanged)
            {
                MessageBox.Show(
                    "The selected batch prices have not changed.",
                    "Batch Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var errors = SelectedBatch.ValidateForSave();
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Batch Price Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string costWarning = string.IsNullOrWhiteSpace(SelectedBatch.ProposedCostWarning)
                ? string.Empty
                : $"\n\n{SelectedBatch.ProposedCostWarning}";

            MessageBoxResult confirmation = MessageBox.Show(
                $"{SelectedBatch.OverrideActionText} for batch {SelectedBatch.BatchNo}?\n\n" +
                $"Current source: {SelectedBatch.PriceSourceText}\n" +
                $"Batch Cost: {SelectedBatch.CostPrice:N2}\n" +
                $"Average Cost: {SelectedBatch.AverageCost:N2}\n\n" +
                $"Current Retail: {SelectedBatch.EffectiveRetailPrice:N2}\n" +
                $"New Retail: {SelectedBatch.OverrideRetailPrice:N2}\n" +
                $"Retail profit / margin: {SelectedBatch.ProposedRetailProfit:N2} / {SelectedBatch.ProposedRetailMarginPercentage:N2}%\n\n" +
                $"Current Wholesale: {SelectedBatch.EffectiveWholesalePrice:N2}\n" +
                $"New Wholesale: {SelectedBatch.OverrideWholesalePrice:N2}\n" +
                $"Wholesale profit / margin: {SelectedBatch.ProposedWholesaleProfit:N2} / {SelectedBatch.ProposedWholesaleMarginPercentage:N2}%" +
                costWarning,
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
        private async Task UseMasterPriceAsync()
        {
            if (SelectedItem == null || SelectedBatch == null || !SelectedBatch.HasSellingPriceOverride)
            {
                ShowSelectionRequired("Select a batch that currently has an override.");
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Return batch {SelectedBatch.BatchNo} to the current master price?\n\n" +
                $"Current batch override:\nRetail: {SelectedBatch.EffectiveRetailPrice:N2}\nWholesale: {SelectedBatch.EffectiveWholesalePrice:N2}\n\n" +
                $"Current master price:\nRetail: {SelectedBatch.MasterRetailPrice:N2}\nWholesale: {SelectedBatch.MasterWholesalePrice:N2}\n\n" +
                "This does not remove the batch or its history.",
                "Use Master Price",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
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
                StatusMessage = "Batch now uses the master price.";
                await LoadDataCoreAsync(variantId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The batch was not returned to the master price:\n\n{ex.Message}", "Pricing", MessageBoxButton.OK, MessageBoxImage.Error);
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
