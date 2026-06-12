using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    // A temporary class to prevent UI errors until you build a real UOM database table
    public class TempUom { public string UomCode { get; set; } = string.Empty; }

    public partial class ItemMasterViewModel : ObservableObject // Or ViewModelBase if you prefer
    {
        private readonly ItemRepository _itemRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly SupplierRepository _supplierRepository;

        private Item? _editingItem; // Keeps track of whether we are updating or creating

        // ==========================================
        // 1. CORE IDENTITY FIELDS
        // ==========================================
        [ObservableProperty] private string _barcode = string.Empty;
        [ObservableProperty] private string _itemCode = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _receiptDescription = string.Empty;
        [ObservableProperty] private string _sinhalaDescription = string.Empty;
        [ObservableProperty] private string _tamilDescription = string.Empty;

        // ==========================================
        // 2. CLASSIFICATION FIELDS & DROPDOWNS
        // ==========================================
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private SubCategory? _selectedSubCategory;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private TempUom? _selectedUom;

        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<SubCategory> _subCategories = new();
        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private ObservableCollection<TempUom> _unitsOfMeasure = new();

        // ==========================================
        // 3. PRICING STRATEGY FIELDS
        // ==========================================
        [ObservableProperty] private decimal _costPrice;
        [ObservableProperty] private decimal _retailPrice;
        [ObservableProperty] private decimal _wholesalePrice;

        // ==========================================
        // 4. WAREHOUSE & RULES FIELDS
        // ==========================================
        [ObservableProperty] private int _reorderLevel;
        [ObservableProperty] private int _reorderQty;
        [ObservableProperty] private string _binLocation = string.Empty;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _allowDiscounts = true;
        [ObservableProperty] private bool _lockPriceAtPos = true;

        // ==========================================
        // 5. SIDEBAR DATA
        // ==========================================
        [ObservableProperty] private ObservableCollection<Item> _recentInventoryItems = new();
        [ObservableProperty] private Item? _selectedRecentItem;


        // Constructor injects all our lightning-fast micro-connection repositories
        public ItemMasterViewModel(
            ItemRepository itemRepository,
            CategoryRepository categoryRepository,
            SubCategoryRepository subCategoryRepository,
            SupplierRepository supplierRepository)
        {
            _itemRepository = itemRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _supplierRepository = supplierRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 1. Load Dropdowns (Using the One-Shot speed trick to prevent UI Freezes!)
            var cats = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(cats.Where(c => c.IsActive));

            var subCats = await _subCategoryRepository.GetAllAsync();
            SubCategories = new ObservableCollection<SubCategory>(subCats.Where(s => s.IsActive));

            var supps = await _supplierRepository.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(supps.Where(s => s.IsActive));

            // Load Temporary UOMs
            UnitsOfMeasure = new ObservableCollection<TempUom>
            {
                new TempUom { UomCode = "PCS" },
                new TempUom { UomCode = "KG" },
                new TempUom { UomCode = "BOX" },
                new TempUom { UomCode = "LTR" }
            };

            // 2. Load the Sidebar Data
            await LoadRecentItemsAsync();
        }

        private async Task LoadRecentItemsAsync()
        {
            var allItems = await _itemRepository.GetAllAsync();
            // Show only the 50 newest items in the sidebar to keep it incredibly fast
            var recent = allItems.OrderByDescending(i => i.Id).Take(50).ToList();
            RecentInventoryItems = new ObservableCollection<Item>(recent);
        }

        // ==========================================
        // ACTION COMMANDS
        // ==========================================

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(Barcode) || string.IsNullOrWhiteSpace(ItemCode) || string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("Barcode, Item Code, and Description are required.", "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedCategory == null || SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Category and a Supplier.", "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Map the Form to the Database Model
                var item = _editingItem ?? new Item();

                item.Barcode = this.Barcode;
                item.ItemCode = this.ItemCode;
                item.Description = this.Description;
                item.ReceiptDescription = string.IsNullOrWhiteSpace(this.ReceiptDescription) ? this.Description : this.ReceiptDescription; // Fallback
                item.SinhalaDescription = this.SinhalaDescription;
                item.TamilDescription = this.TamilDescription;

                item.CategoryId = SelectedCategory.Id;
                item.SubCategoryId = SelectedSubCategory?.Id;
                item.SupplierId = SelectedSupplier.Id;
                item.UomId = 1; // Hardcoded until you add a real UOM table

                item.CostPrice = this.CostPrice;
                item.RetailPrice = this.RetailPrice;
                item.WholesalePrice = this.WholesalePrice;

                item.ReorderLevel = this.ReorderLevel;
                item.ReorderQty = this.ReorderQty;
                item.BinLocation = this.BinLocation;
                item.IsActive = this.IsActive;
                item.AllowDiscounts = this.AllowDiscounts;
                item.LockPriceAtPos = this.LockPriceAtPos;

                // 3. Save to Database
                if (_editingItem == null)
                {
                    await _itemRepository.AddAsync(item);
                }
                else
                {
                    await _itemRepository.UpdateAsync(item);
                }

                // 4. Refresh Sidebar & Clear Form
                await LoadRecentItemsAsync();
                Clear();
                MessageBox.Show("Item saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            _editingItem = null;

            Barcode = string.Empty;
            ItemCode = string.Empty;
            Description = string.Empty;
            ReceiptDescription = string.Empty;
            SinhalaDescription = string.Empty;
            TamilDescription = string.Empty;

            SelectedCategory = null;
            SelectedSubCategory = null;
            SelectedSupplier = null;
            SelectedUom = null;

            CostPrice = 0;
            RetailPrice = 0;
            WholesalePrice = 0;

            ReorderLevel = 0;
            ReorderQty = 0;
            BinLocation = string.Empty;

            IsActive = true;
            AllowDiscounts = true;
            LockPriceAtPos = true;

            SelectedRecentItem = null; // Unselect the grid
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (_editingItem == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete {Description}?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _itemRepository.DeleteAsync(_editingItem.Id);
                await LoadRecentItemsAsync();
                Clear();
            }
        }

        [RelayCommand]
        private void GenerateBarcode()
        {
            // Creates a fast, unique 12-digit sequence based on the current time
            Barcode = "ITM" + DateTime.Now.ToString("yyMMddHHmmss");
        }

        [RelayCommand]
        private void NavigateToBarcodePrinter()
        {
            MessageBox.Show("Barcode Printer Module will be integrated here.", "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==========================================
        // AUTO-TRIGGERS (CommunityToolkit Magic)
        // ==========================================

        // This fires automatically the exact millisecond the user clicks a row in the Sidebar grid
        partial void OnSelectedRecentItemChanged(Item? value)
        {
            if (value != null)
            {
                _editingItem = value;

                // Load database values back into the text boxes!
                Barcode = value.Barcode;
                ItemCode = value.ItemCode;
                Description = value.Description;
                ReceiptDescription = value.ReceiptDescription;
                SinhalaDescription = value.SinhalaDescription ?? string.Empty;
                TamilDescription = value.TamilDescription ?? string.Empty;

                CostPrice = value.CostPrice;
                RetailPrice = value.RetailPrice;
                WholesalePrice = value.WholesalePrice;

                ReorderLevel = value.ReorderLevel;
                ReorderQty = value.ReorderQty;
                BinLocation = value.BinLocation ?? string.Empty;

                IsActive = value.IsActive;
                AllowDiscounts = value.AllowDiscounts;
                LockPriceAtPos = value.LockPriceAtPos;

                // Find and select the correct items in the dropdowns
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
                SelectedSubCategory = SubCategories.FirstOrDefault(s => s.Id == value.SubCategoryId);
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == value.SupplierId);
                // UOM is mocked for now
                SelectedUom = UnitsOfMeasure.FirstOrDefault();
            }
        }
    }
}