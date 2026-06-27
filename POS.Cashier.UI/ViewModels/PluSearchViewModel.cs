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

        // ==========================================
        // UI BINDING PROPERTIES
        // ==========================================
        [ObservableProperty] private string _searchText = string.Empty;

        [ObservableProperty] private string _statusText = "Type an item name, SKU, or Barcode and hit Enter to search...";
        [ObservableProperty] private string _statusColorHex = "#555555"; // Gray

        // Controls if the "ADD TO CART" button is clickable
        [ObservableProperty] private bool _canAddVariant = false;

        // Represents Table 1 (The Master Catalog)
        public ObservableCollection<ParentSeekDto> ParentResults { get; } = new();

        // Represents Table 2 (The Specific Sizes/Colors in Stock)
        public ObservableCollection<VariantSeekDto> VariantResults { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddVariant))]
        private ParentSeekDto? _selectedParent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddVariant))]
        private VariantSeekDto? _selectedVariant;

        // The View listens to this so it knows when to close the window
        public event Action<bool>? ActionCompleted;

        public PluSearchViewModel(ItemMasterRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        // ==========================================
        // TABLE TRIGGERS
        // ==========================================

        // When the Cashier clicks a row in Table 1, instantly populate Table 2!
        partial void OnSelectedParentChanged(ParentSeekDto? value)
        {
            if (value != null)
            {
                _ = LoadVariantsAsync(value.ParentId);
            }
            else
            {
                VariantResults.Clear();
            }
        }

        // Validate that a specific size/color is selected before allowing Add To Cart
        partial void OnSelectedVariantChanged(VariantSeekDto? value)
        {
            if (value != null)
            {
                CanAddVariant = true;

                if (value.StockOnHand <= 0)
                {
                    StatusText = "⚠️ WARNING: This item is currently OUT OF STOCK.";
                    StatusColorHex = "#D97706"; // Orange warning
                }
                else
                {
                    StatusText = $"Selected: {value.VariantDescription}. Ready to add!";
                    StatusColorHex = "#10B981"; // Green Success
                }
            }
            else
            {
                CanAddVariant = false;
            }
        }

        // ==========================================
        // COMMANDS & EXECUTION
        // ==========================================
        [RelayCommand]
        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            try
            {
                ParentResults.Clear();
                VariantResults.Clear();
                SelectedParent = null;

                var results = await _itemRepository.SearchSeekParentsAsync(SearchText);

                foreach (var parent in results)
                {
                    ParentResults.Add(parent);
                }

                StatusText = $"Found {ParentResults.Count} matching items.";
                StatusColorHex = "#003366"; // Blue info
            }
            catch (Exception ex)
            {
                StatusText = "Database Search Failed.";
                StatusColorHex = "#DC3545"; // Red Error
            }
        }

        private async Task LoadVariantsAsync(int parentId)
        {
            try
            {
                VariantResults.Clear();
                var variants = await _itemRepository.GetSeekVariantsAsync(parentId);

                foreach (var v in variants)
                {
                    VariantResults.Add(v);
                }
            }
            catch
            {
                StatusText = "Failed to load variants.";
                StatusColorHex = "#DC3545"; // Red Error
            }
        }

        [RelayCommand]
        public void AddToCart()
        {
            if (SelectedVariant == null) return;

            // THE SILENT BROADCASTER:
            // This shouts out the SKU code across the entire app. 
            // The Sales Screen catches it instantly and does all the math!
            WeakReferenceMessenger.Default.Send(new AddToCartMessage(SelectedVariant.SkuCode));

            // Tell the View to close the dialog successfully
            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        public void Close()
        {
            ActionCompleted?.Invoke(false);
        }
    }
}