using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PriceManagementViewModel : ObservableObject
    {
        private readonly PriceManagementRepository _repository;

        // ==============================================================================
        // --- FILTERS & SEARCH STATES ---
        // ==============================================================================
        [ObservableProperty] private string _searchText = string.Empty;

        [ObservableProperty] private string _selectedMarginFilter = "All";
        public ObservableCollection<string> MarginFilters { get; } = new(new[]
        {
            "All",
            "Low Margin Alerts (< 20%)",
            "Healthy Margins"
        });

        [ObservableProperty] private string _selectedExpiryFilter = "All";
        public ObservableCollection<string> ExpiryFilters { get; } = new(new[]
        {
            "All",
            "Expiring Soon"
        });

        // ==============================================================================
        // --- DATA COLLECTIONS ---
        // ==============================================================================
        // The main grid data
        public ObservableCollection<PriceManagementSummaryDto> PricingItems { get; } = new();

        // The batch breakdown for the selected item
        public ObservableCollection<ItemBatch> ActiveBatches { get; } = new();

        // ==============================================================================
        // --- SELECTION & UI STATES ---
        // ==============================================================================
        [ObservableProperty] private PriceManagementSummaryDto? _selectedItem;

        // Controls the visibility of the Right-Hand Batch Inspector Panel
        [ObservableProperty] private bool _isBatchPanelVisible = false;

        public PriceManagementViewModel(PriceManagementRepository repository)
        {
            _repository = repository;
            _ = LoadDataAsync();
        }

        // ==============================================================================
        // --- FILTER TRIGGERS (Auto-Reloads Grid when changed) ---
        // ==============================================================================
        partial void OnSearchTextChanged(string value) => _ = LoadDataAsync();
        partial void OnSelectedMarginFilterChanged(string value) => _ = LoadDataAsync();
        partial void OnSelectedExpiryFilterChanged(string value) => _ = LoadDataAsync();

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            PricingItems.Clear();
            CloseBatchPanel(); // Reset view when refreshing data

            try
            {
                var data = await _repository.GetPricingSummariesAsync(SelectedMarginFilter, SelectedExpiryFilter, SearchText);

                foreach (var item in data)
                {
                    PricingItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load pricing data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // ==============================================================================
        // --- MASTER-DETAIL ENGINE ---
        // ==============================================================================

        // Triggered automatically when the user clicks a row in the main DataGrid
        partial void OnSelectedItemChanged(PriceManagementSummaryDto? value)
        {
            if (value != null)
            {
                _ = OpenBatchInspectorAsync(value.ItemVariantId);
            }
        }

        private async Task OpenBatchInspectorAsync(int variantId)
        {
            ActiveBatches.Clear();

            try
            {
                var batches = await _repository.GetActiveBatchesAsync(variantId);
                foreach (var batch in batches)
                {
                    ActiveBatches.Add(batch);
                }

                // Slide the panel open
                IsBatchPanelVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load batch breakdown: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseBatchPanel()
        {
            IsBatchPanelVisible = false;
            SelectedItem = null;
            ActiveBatches.Clear();
        }

        // ==============================================================================
        // --- ATOMIC SAVE EXECUTION ---
        // ==============================================================================
        [RelayCommand]
        private async Task SaveAdjustmentsAsync()
        {
            if (SelectedItem == null)
            {
                MessageBox.Show("Please select an item to save its pricing adjustments.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Margin Safety Check
            if (SelectedItem.GrossMarginPercentage < 0)
            {
                var warning = MessageBox.Show(
                    $"Warning: The retail price for '{SelectedItem.ItemCode}' is lower than its cost, resulting in a negative margin ({SelectedItem.GrossMarginPercentage}%).\n\nAre you sure you want to save this pricing strategy?",
                    "Negative Margin Alert", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (warning == MessageBoxResult.No) return;
            }

            try
            {
                // Send the master prices and any batch-specific markdowns to the repo safely
                await _repository.UpdatePricingAsync(SelectedItem, ActiveBatches.ToList());

                MessageBox.Show($"Pricing updated successfully for {SelectedItem.ItemCode}.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Keep the panel open so the user can see their saved work, but optionally you could call CloseBatchPanel() here.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save pricing: {ex.InnerException?.Message ?? ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}