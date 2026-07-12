using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class PluSearchViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusText = "Type item name, item code, SKU, barcode, or GRN batch barcode and press Enter.";

        [ObservableProperty]
        private string _statusColorHex = "#555555";

        [ObservableProperty]
        private bool _canAddVariant = false;

        [ObservableProperty]
        private bool _canAddBatch = false;

        [ObservableProperty]
        private bool _isBatchSelectionVisible = false;

        [ObservableProperty]
        private bool _isBusy = false;

        public ObservableCollection<ParentSeekDto> ParentResults { get; } = new();

        public ObservableCollection<VariantSeekDto> VariantResults { get; } = new();

        public ObservableCollection<BatchSeekDto> BatchResults { get; } = new();

        [ObservableProperty]
        private ParentSeekDto? _selectedParent;

        [ObservableProperty]
        private VariantSeekDto? _selectedVariant;

        [ObservableProperty]
        private BatchSeekDto? _selectedBatch;

        public event Action<bool>? ActionCompleted;

        public PluSearchViewModel(ItemMasterRepository itemRepository)
        {
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
        }

        partial void OnSelectedParentChanged(ParentSeekDto? value)
        {
            SelectedVariant = null;
            SelectedBatch = null;

            VariantResults.Clear();
            BatchResults.Clear();

            CanAddVariant = false;
            CanAddBatch = false;
            IsBatchSelectionVisible = false;

            if (value != null)
            {
                _ = LoadVariantsAsync(value.ParentId);
            }
        }

        partial void OnSelectedVariantChanged(VariantSeekDto? value)
        {
            SelectedBatch = null;
            BatchResults.Clear();

            CanAddVariant = false;
            CanAddBatch = false;
            IsBatchSelectionVisible = false;

            if (value == null)
            {
                StatusText = "Select an item.";
                StatusColorHex = "#003366";
                return;
            }

            if (value.IsService)
            {
                CanAddVariant = true;
                StatusText = $"Service selected: {value.VariantDescription}. Press Add or Enter.";
                StatusColorHex = "#10B981";
                return;
            }

            if (value.StockOnHand <= 0m)
            {
                StatusText = "OUT OF STOCK. This variant cannot be added.";
                StatusColorHex = "#D97706";
                return;
            }

            if (value.HasBatchTracking)
            {
                IsBatchSelectionVisible = true;
                StatusText = "Batch item selected. Select exact GRN batch below.";
                StatusColorHex = "#003366";

                _ = LoadBatchesForSelectedVariantAsync(value.VariantId);
                return;
            }

            CanAddVariant = true;
            StatusText = $"Average-cost item selected: {value.VariantDescription}. Press Add or Enter.";
            StatusColorHex = "#10B981";
        }

        partial void OnSelectedBatchChanged(BatchSeekDto? value)
        {
            CanAddBatch = false;

            if (value == null)
            {
                if (SelectedVariant?.HasBatchTracking == true)
                {
                    StatusText = "Select exact GRN batch.";
                    StatusColorHex = "#003366";
                }

                return;
            }

            if (!value.IsSelectable)
            {
                StatusText = "Selected batch cannot be sold.";
                StatusColorHex = "#DC3545";
                return;
            }

            CanAddBatch = true;

            if (value.IsNearExpiry)
            {
                StatusText = $"Near expiry batch selected: {value.ExpiryDisplayText}. Press Add Batch.";
                StatusColorHex = "#D97706";
                return;
            }

            StatusText = $"Batch selected: {value.BatchDisplayText} / {value.BarcodeDisplayText}. Press Add Batch.";
            StatusColorHex = "#10B981";
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            string term = (SearchText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
            {
                StatusText = "Enter item name, item code, SKU, barcode, or GRN batch barcode.";
                StatusColorHex = "#D97706";
                return;
            }

            try
            {
                IsBusy = true;

                ClearResults();

                // Fast path:
                // If cashier types/scans exact GRN batch barcode inside Product Seek,
                // send exact batch directly and close.
                var exactBatch = await _itemRepository.GetSellableBatchByInternalBarcodeAsync(term);

                if (exactBatch != null)
                {
                    WeakReferenceMessenger.Default.Send(
                        new AddToCartMessage(
                            itemVariantId: exactBatch.ItemVariantId,
                            itemBatchId: exactBatch.ItemBatchId,
                            skuCode: string.Empty,
                            barcode: term,
                            quantity: 1m));

                    ActionCompleted?.Invoke(true);
                    return;
                }

                var results = await _itemRepository.SearchSeekParentsAsync(term);

                foreach (var parent in results)
                    ParentResults.Add(parent);

                if (ParentResults.Count == 0)
                {
                    StatusText = "No matching items found.";
                    StatusColorHex = "#D97706";
                    return;
                }

                StatusText = $"Found {ParentResults.Count} matching item(s). Select one.";
                StatusColorHex = "#003366";
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

        private async Task LoadVariantsAsync(int parentId)
        {
            try
            {
                IsBusy = true;

                VariantResults.Clear();
                BatchResults.Clear();

                SelectedVariant = null;
                SelectedBatch = null;

                CanAddVariant = false;
                CanAddBatch = false;
                IsBatchSelectionVisible = false;

                var variants = await _itemRepository.GetSeekVariantsAsync(parentId);

                foreach (var variant in variants)
                    VariantResults.Add(variant);

                if (VariantResults.Count == 0)
                {
                    StatusText = "No sellable variants found for this item.";
                    StatusColorHex = "#D97706";
                    return;
                }

                StatusText = $"Loaded {VariantResults.Count} sellable variant(s). Select exact item.";
                StatusColorHex = "#003366";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load variants: {ex.Message}";
                StatusColorHex = "#DC3545";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadBatchesForSelectedVariantAsync(int variantId)
        {
            if (variantId <= 0)
                return;

            try
            {
                IsBusy = true;

                BatchResults.Clear();
                SelectedBatch = null;
                CanAddBatch = false;

                var batches = await _itemRepository.GetSeekBatchesByVariantIdAsync(variantId);

                foreach (var batch in batches)
                    BatchResults.Add(batch);

                if (BatchResults.Count == 0)
                {
                    StatusText = "No sellable GRN batch stock found. Check GRN barcode labels or stock.";
                    StatusColorHex = "#D97706";
                    return;
                }

                SelectedBatch = BatchResults[0];

                StatusText = $"Loaded {BatchResults.Count} GRN batch(es). Select exact batch.";
                StatusColorHex = "#003366";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load GRN batches: {ex.Message}";
                StatusColorHex = "#DC3545";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AddToCart()
        {
            if (SelectedVariant == null)
            {
                StatusText = "Select an item first.";
                StatusColorHex = "#D97706";
                return;
            }

            if (!SelectedVariant.IsService &&
                SelectedVariant.StockOnHand <= 0m)
            {
                StatusText = "Cannot add out-of-stock item.";
                StatusColorHex = "#DC3545";
                CanAddVariant = false;
                return;
            }

            if (!SelectedVariant.IsService &&
                SelectedVariant.HasBatchTracking)
            {
                IsBatchSelectionVisible = true;
                CanAddVariant = false;

                StatusText = "Batch-tracked item. Select exact GRN batch below.";
                StatusColorHex = "#D97706";

                if (BatchResults.Count == 0)
                    _ = LoadBatchesForSelectedVariantAsync(SelectedVariant.VariantId);

                return;
            }

            WeakReferenceMessenger.Default.Send(
                new AddToCartMessage(
                    itemVariantId: SelectedVariant.VariantId,
                    skuCode: SelectedVariant.SkuCode,
                    barcode: SelectedVariant.Barcode,
                    quantity: 1m));

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        public void AddSelectedBatchToCart()
        {
            if (SelectedBatch == null)
            {
                StatusText = "Select exact GRN batch first.";
                StatusColorHex = "#D97706";
                return;
            }

            AddBatchToCart(SelectedBatch);
        }

        public void AddBatchToCart(BatchSeekDto? batch)
        {
            if (batch == null)
                return;

            if (!batch.IsSelectable)
            {
                StatusText = "Selected batch cannot be sold.";
                StatusColorHex = "#DC3545";
                return;
            }

            WeakReferenceMessenger.Default.Send(
                new AddToCartMessage(
                    itemVariantId: batch.ItemVariantId,
                    itemBatchId: batch.ItemBatchId,
                    skuCode: string.Empty,
                    barcode: batch.InternalBatchBarcode,
                    quantity: 1m));

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        public void Clear()
        {
            SearchText = string.Empty;
            ClearResults();

            StatusText = "Type item name, item code, SKU, barcode, or GRN batch barcode and press Enter.";
            StatusColorHex = "#555555";
        }

        [RelayCommand]
        public void Close()
        {
            ActionCompleted?.Invoke(false);
        }

        private void ClearResults()
        {
            ParentResults.Clear();
            VariantResults.Clear();
            BatchResults.Clear();

            SelectedParent = null;
            SelectedVariant = null;
            SelectedBatch = null;

            CanAddVariant = false;
            CanAddBatch = false;
            IsBatchSelectionVisible = false;
        }
    }
}