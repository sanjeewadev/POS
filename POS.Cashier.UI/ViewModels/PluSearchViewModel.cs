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
        private string _statusText = "Type item name, item code, SKU, or barcode and press Enter.";

        [ObservableProperty]
        private string _statusColorHex = "#555555";

        [ObservableProperty]
        private bool _canAddVariant = false;

        public ObservableCollection<ParentSeekDto> ParentResults { get; } = new();

        public ObservableCollection<VariantSeekDto> VariantResults { get; } = new();

        [ObservableProperty]
        private ParentSeekDto? _selectedParent;

        [ObservableProperty]
        private VariantSeekDto? _selectedVariant;

        public event Action<bool>? ActionCompleted;

        public PluSearchViewModel(ItemMasterRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        partial void OnSelectedParentChanged(ParentSeekDto? value)
        {
            SelectedVariant = null;
            CanAddVariant = false;

            if (value != null)
            {
                _ = LoadVariantsAsync(value.ParentId);
            }
            else
            {
                VariantResults.Clear();
            }
        }

        partial void OnSelectedVariantChanged(VariantSeekDto? value)
        {
            if (value == null)
            {
                CanAddVariant = false;
                return;
            }

            if (value.StockOnHand <= 0)
            {
                CanAddVariant = false;
                StatusText = "OUT OF STOCK. This variant cannot be added.";
                StatusColorHex = "#D97706";
                return;
            }

            CanAddVariant = true;
            StatusText = $"Selected: {value.VariantDescription}. Ready to add.";
            StatusColorHex = "#10B981";
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return;

            try
            {
                ParentResults.Clear();
                VariantResults.Clear();

                SelectedParent = null;
                SelectedVariant = null;
                CanAddVariant = false;

                var results = await _itemRepository.SearchSeekParentsAsync(SearchText.Trim());

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
        }

        private async Task LoadVariantsAsync(int parentId)
        {
            try
            {
                VariantResults.Clear();
                SelectedVariant = null;
                CanAddVariant = false;

                var variants = await _itemRepository.GetSeekVariantsAsync(parentId);

                foreach (var variant in variants)
                    VariantResults.Add(variant);

                if (VariantResults.Count == 0)
                {
                    StatusText = "No sellable variants found for this item.";
                    StatusColorHex = "#D97706";
                    return;
                }

                StatusText = $"Loaded {VariantResults.Count} variant(s). Select exact item.";
                StatusColorHex = "#003366";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load variants: {ex.Message}";
                StatusColorHex = "#DC3545";
            }
        }

        [RelayCommand]
        public void AddToCart()
        {
            if (SelectedVariant == null)
                return;

            if (SelectedVariant.StockOnHand <= 0)
            {
                StatusText = "Cannot add out-of-stock item.";
                StatusColorHex = "#DC3545";
                CanAddVariant = false;
                return;
            }

            WeakReferenceMessenger.Default.Send(
                new AddToCartMessage(
                    new AddToCartRequest(
                        itemVariantId: SelectedVariant.VariantId,
                        skuCode: SelectedVariant.SkuCode,
                        barcode: SelectedVariant.Barcode,
                        quantity: 1)));

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        public void Close()
        {
            ActionCompleted?.Invoke(false);
        }
    }
}