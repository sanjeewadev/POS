using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.Services;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    // NOTE: This ViewModel has been completely redesigned for the new professional seek window.
    // The old three-grid logic has been replaced with a single, powerful search.
    public partial class PluSearchViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly ICashierBatchSelectionService _batchSelectionService;

        [ObservableProperty]
        private string _searchTerm = string.Empty;

        [ObservableProperty]
        private string _selectedCategory = "ALL CATEGORIES";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Type to search and press Enter.";

        [ObservableProperty]
        private string _statusColorHex = "#555555";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmSelectionCommand))]
        private ProductSeekResultDto? _selectedProduct;

        [ObservableProperty]
        private bool _isWholesaleMode;

        public string PricingModeText => IsWholesaleMode ? "WHOLESALE" : "RETAIL";

        public ObservableCollection<ProductSeekResultDto> SearchResults { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        // Used ONLY to close the window (Esc or Close button)
        public event Action<ProductSeekResult?>? ActionCompleted;

        // CONTINUOUS SCANNING: Used to send items to the cart in the background
        public event Action<ProductSeekResult>? ItemSelected;

        public PluSearchViewModel(
            ItemMasterRepository itemRepository,
            CategoryRepository categoryRepository,
            ICashierBatchSelectionService batchSelectionService)
        {
            _itemRepository = itemRepository;
            _categoryRepository = categoryRepository;
            _batchSelectionService = batchSelectionService;
        }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var activeCategories = await _categoryRepository.GetActiveAsync();
                Categories.Add("ALL CATEGORIES");
                foreach (var cat in activeCategories)
                {
                    Categories.Add(cat.CategoryName);
                }
            }
            catch (Exception)
            {
                StatusText = "Could not load categories.";
                StatusColorHex = "#DC3545";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ConfigurePricingMode(bool isWholesaleMode)
        {
            IsWholesaleMode = isWholesaleMode;
            OnPropertyChanged(nameof(PricingModeText));
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusText = "Searching...";
                StatusColorHex = "#555555";
                SearchResults.Clear();
                SelectedProduct = null;

                string term = (SearchTerm ?? string.Empty).Trim();

                // Fast path for exact GRN batch barcode
                if (!string.IsNullOrWhiteSpace(term))
                {
                    var exactBatch = await _itemRepository.GetSellableBatchByInternalBarcodeAsync(term);
                    if (exactBatch != null)
                    {
                        CompleteSelectionAndReset(new ProductSeekResult { VariantId = exactBatch.ItemVariantId, BatchId = exactBatch.ItemBatchId });
                        return;
                    }
                }

                var results = await _itemRepository.SearchSellableProductsAsync(term, SelectedCategory);
                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }

                if (SearchResults.Any())
                {
                    SelectedProduct = SearchResults.First();
                    StatusText = $"Found {SearchResults.Count} item(s). Select one and press Enter.";
                    StatusColorHex = "#003366";
                }
                else
                {
                    StatusText = "No matching items found.";
                    StatusColorHex = "#D97706";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Search failed: {ex.Message}";
                StatusColorHex = "#DC3545";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanConfirmSelection() => SelectedProduct != null && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanConfirmSelection))]
        private async Task ConfirmSelectionAsync()
        {
            if (!CanConfirmSelection() || SelectedProduct == null) return;

            IsBusy = true;
            StatusText = "Processing selection...";
            try
            {
                await HandleProductSelection(SelectedProduct);
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                StatusColorHex = "#DC3545";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleProductSelection(ProductSeekResultDto product)
        {
            // Case 1: Service item
            if (product.IsService)
            {
                CompleteSelectionAndReset(new ProductSeekResult { VariantId = product.VariantId });
                return;
            }

            // Case 2: Non-batch-tracked stock item
            if (!product.HasBatchTracking)
            {
                if (product.StockOnHand <= 0)
                {
                    StatusText = "This item is out of stock.";
                    StatusColorHex = "#D97706";
                    return;
                }
                var generalBatch = await _itemRepository.GetGeneralSellableBatchForVariantAsync(product.VariantId);
                if (generalBatch == null)
                {
                    StatusText = "Could not find the stock bucket for this item.";
                    StatusColorHex = "#DC3545";
                    return;
                }

                CompleteSelectionAndReset(new ProductSeekResult { VariantId = product.VariantId, BatchId = generalBatch.ItemBatchId });
                return;
            }

            // Case 3: Batch-tracked stock item
            var batches = await _itemRepository.GetSellableBatchesByVariantIdAsync(product.VariantId);

            if (batches.Count == 0)
            {
                StatusText = "This item has batch tracking but no sellable batches were found.";
                StatusColorHex = "#D97706";
                return;
            }

            if (batches.Count == 1)
            {
                CompleteSelectionAndReset(new ProductSeekResult { VariantId = product.VariantId, BatchId = batches[0].ItemBatchId });
                return;
            }

            // Multiple batches, show dialog
            var fullItem = await _itemRepository.GetSellableItemByVariantIdAsync(product.VariantId);
            if (fullItem == null)
            {
                StatusText = "Could not load item details for batch selection.";
                StatusColorHex = "#DC3545";
                return;
            }

            var request = new CashierBatchSelectionRequest
            {
                Item = fullItem,
                Batches = batches.ToList(),
                IsWholesaleMode = IsWholesaleMode,
                RequestedQuantity = 1m
            };

            var selectionResult = await _batchSelectionService.SelectBatchAsync(request);
            if (selectionResult != null)
            {
                CompleteSelectionAndReset(new ProductSeekResult { VariantId = selectionResult.ItemVariantId, BatchId = selectionResult.ItemBatchId });
            }
            else
            {
                StatusText = "Batch selection cancelled.";
                StatusColorHex = "#555555";
            }
        }

        // CONTINUOUS SCANNING HELPER: Adds the item and instantly prepares the window for the next search
        private void CompleteSelectionAndReset(ProductSeekResult result)
        {
            // Fire the item to the cart in the background
            ItemSelected?.Invoke(result);

            // Clear the search UI
            SearchTerm = string.Empty;
            SearchResults.Clear();
            SelectedProduct = null;

            StatusText = "Item added! Ready for next search.";
            StatusColorHex = "#10B981";
        }

        [RelayCommand]
        public void Clear()
        {
            SearchTerm = string.Empty;
            SearchResults.Clear();
            SelectedProduct = null;
            StatusText = "Type to search and press Enter.";
            StatusColorHex = "#555555";
        }

        [RelayCommand]
        public void Close()
        {
            // Sending null explicitly tells the view code-behind to close the window
            ActionCompleted?.Invoke(null);
        }
    }
}