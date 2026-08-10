using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
    public partial class BatchPriceRowViewModel : ObservableObject
    {
        public PriceManagementBatchRowDto Dto { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        private decimal? _newRetailPrice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        private decimal? _newWholesalePrice;

        public bool IsDirty =>
            (NewRetailPrice.HasValue && NewRetailPrice.Value != Dto.EffectiveRetailPrice) ||
            (NewWholesalePrice.HasValue && NewWholesalePrice.Value != Dto.EffectiveWholesalePrice);

        public BatchPriceRowViewModel(PriceManagementBatchRowDto dto)
        {
            Dto = dto;
        }

        /// <summary>
        /// Marks the current proposed prices as accepted, updating the DTO's effective prices
        /// and resetting the editable fields.
        /// </summary>
        public void AcceptChanges()
        {
            Dto.EffectiveRetailPrice = NewRetailPrice ?? Dto.EffectiveRetailPrice;
            Dto.EffectiveWholesalePrice = NewWholesalePrice ?? Dto.EffectiveWholesalePrice;
            NewRetailPrice = null;
            NewWholesalePrice = null;
        }
    }

    public partial class PriceManagementViewModel : ObservableObject
    {
        private readonly PriceManagementRepository _repository;
        private readonly AuthService _authService;
        private readonly DispatcherTimer _searchDebounceTimer;
        private int _loadVersion;

        public ObservableCollection<PriceManagementItemSummaryDto> PricingItems { get; } = new();
        public ObservableCollection<BatchPriceRowViewModel> ActiveBatches { get; } = new();
        public ObservableCollection<string> CategoryFilters { get; } = new(new[] { "All" });

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedCategoryFilter = "All";
        [ObservableProperty] private PriceManagementItemSummaryDto? _selectedItem;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isBatchLoading;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private int _totalItems;

        public bool HasUnsavedChanges => ActiveBatches.Any(i => i.IsDirty);
        public bool IsPricingListEmpty => !IsBusy && PricingItems.Count == 0;
        public bool ShowBatchEmptyState => !IsBatchLoading && ActiveBatches.Count == 0;
        public bool HasSelectedItem => SelectedItem != null;

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
        partial void OnSelectedCategoryFilterChanged(string value) => QueueReload();

        partial void OnSelectedItemChanged(PriceManagementItemSummaryDto? value)
        {
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            ActiveBatches.Clear();
            NotifyEmptyStateProperties();
            _ = LoadBatchesForSelectedItemAsync();
        }

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsPricingListEmpty));
        partial void OnIsBatchLoadingChanged(bool value) => NotifyEmptyStateProperties();

        private void NotifyEmptyStateProperties()
        {
            OnPropertyChanged(nameof(IsPricingListEmpty));
            OnPropertyChanged(nameof(ShowBatchEmptyState));
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
                    "You have unsaved price changes that will be lost. Continue and discard changes?",
                    "Unsaved Pricing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            IsBusy = true;
            StatusMessage = "Loading item summaries...";
            try
            {
                int? selectedId = preserveVariantId ?? SelectedItem?.ItemVariantId;
                var rows = await _repository.GetItemSummariesAsync(
                    searchText: SearchText,
                    categoryFilter: SelectedCategoryFilter);

                if (version != _loadVersion)
                    return;

                PricingItems.Clear();
                foreach (var row in rows)
                    PricingItems.Add(row);

                TotalItems = PricingItems.Count;
                SelectedItem = selectedId.HasValue ? PricingItems.FirstOrDefault(i => i.ItemVariantId == selectedId) : null;
                StatusMessage = $"Loaded {TotalItems} item(s) with active stock.";
                NotifyEmptyStateProperties();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item summaries.";
                MessageBox.Show(
                    $"Failed to load item summaries:\n\n{ex.Message}",
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
            if (variantId <= 0)
                return;

            IsBatchLoading = true;
            try
            {
                var rows = await _repository.GetBatchesForItemVariantAsync(variantId);
                if (SelectedItem?.ItemVariantId != variantId)
                    return; // Selection changed while loading

                ActiveBatches.Clear();
                foreach (var row in rows)
                    ActiveBatches.Add(new BatchPriceRowViewModel(row));

                NotifyEmptyStateProperties();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load batches for the selected item:\n\n{ex.Message}", "Pricing", MessageBoxButton.OK, MessageBoxImage.Error);
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
            SelectedCategoryFilter = "All";
            await LoadDataCoreAsync();
        }

        public async Task ApplyMasterPriceChangeAsync(MasterPriceQuickChangeResult result)
        {
            // This is now handled by the new simplified grid view.
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAllChangesAsync()
        {
            var dirtyRows = ActiveBatches.Where(i => i.IsDirty).ToList();
            if (!dirtyRows.Any())
            {
                MessageBox.Show("No price changes have been made.", "Pricing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmation = MessageBox.Show(
                $"You are about to save {dirtyRows.Count} batch price override(s). Continue?",
                "Confirm Save",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Saving batch price overrides...";
            int successCount = 0;
            int errorCount = 0;

            try
            {
                string user = CurrentUsername();
                int? variantIdToPreserve = SelectedItem?.ItemVariantId;
                foreach (var row in dirtyRows)
                {
                    try
                    {
                        await _repository.SetBatchPriceOverrideAsync(
                            row.Dto.ItemVariantId,
                            row.Dto.ItemBatchId,
                            row.NewRetailPrice ?? row.Dto.EffectiveRetailPrice,
                            row.NewWholesalePrice ?? row.Dto.EffectiveWholesalePrice,
                            user);
                        row.AcceptChanges(); // Mark the row as clean after successful save
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        LocalLogService.WriteException("Pricing", $"Failed to save batch {row.Dto.BatchNo}", ex);
                    }
                }

                StatusMessage = $"Saved {successCount} override(s). {errorCount} failed.";
                MessageBox.Show(
                    $"Successfully saved {successCount} batch price override(s).\n" +
                    (errorCount > 0 ? $"{errorCount} row(s) failed to save. See logs for details." : ""),
                    "Save Complete",
                    MessageBoxButton.OK,
                    errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                await LoadDataCoreAsync(variantIdToPreserve);
            }
            catch (Exception ex)
            {
                StatusMessage = "An unexpected error occurred during save.";
                MessageBox.Show($"An unexpected error occurred:\n\n{ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string CurrentUsername()
        {
            string username = (_authService.CurrentUser?.Username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Your authenticated user session is unavailable. Sign in again before saving pricing changes.");
            return username;
        }
    }
}
