using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Utilities;

namespace POS.BackOffice.UI.ViewModels
{
    public class MatrixPropertySelection
    {
        public AttributeGroup Group { get; set; } = null!;
        public AttributeValue Value { get; set; } = null!;
        public string DisplayText => $"{Group.GroupName}: {Value.ValueName}";
    }

    public class MatrixPropertyGroupSelection
    {
        public AttributeGroup Group { get; set; } = null!;
        public string GroupName => Group.GroupName;
        public ObservableCollection<MatrixPropertySelection> Values { get; } = new();
    }

    public sealed class ItemTypeOption
    {
        public ItemTypeOption(
            string code,
            string name,
            string description)
        {
            Code = code;
            Name = name;
            Description = description;
        }

        public string Code { get; }

        public string Name { get; }

        public string Description { get; }
    }

    public partial class ItemMasterViewModel : ViewModelBase
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly AttributeRepository _attributeRepository;
        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly SupplierRepository _supplierRepository;

        private readonly StoreSettingsRepository _storeSettingsRepository;
        // FIXED: Using your actual TaxRateRepository
        private readonly TaxRateRepository _taxRateRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isVatRegisteredStore;
        private bool _isLoadingItem;
        private bool _isClearing;
        private bool _isUpdatingSupplierSelection;
        private bool _loadedItemWasDeactivated;
        private bool _loadedItemHasHistory;
        private bool _isApplyingItemType;
        private bool _isCalculatingPricing; // Safety flag for bi-directional math
        private string _generatedVariantItemCode = string.Empty;

        private static readonly Random _random = new();

        private static readonly Regex CodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [ObservableProperty]
        private string _notificationMessage = string.Empty;

        [ObservableProperty]
        private int _totalLoadedItems;

        [ObservableProperty]
        private int _totalLoadedVariants;

        [ObservableProperty]
        private int _activeItems;

        [ObservableProperty]
        private int _deactivatedItems;

        [ObservableProperty]
        private ItemParent _currentItem = new()
        {
            ItemType = ItemTypeCodes.StockItem,
            HasBatchTracking = true,
            HasExpiryTracking = false,
            HasBatchExpiry = false,
            AllowCashierDiscount = true,
            IsTaxInclusive = true
        };

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        [ObservableProperty]
        private string _itemCodeInput = string.Empty;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private UnitOfMeasure? _selectedUom;

        [ObservableProperty]
        private AttributeGroup? _selectedPropertyKey;

        [ObservableProperty]
        private AttributeValue? _propertyValueInput;

        [ObservableProperty]
        private ItemTypeOption? _selectedItemType;

        [ObservableProperty]
        private TaxCategory? _selectedTaxCategory;

        public ObservableCollection<ItemTypeOption> ItemTypes { get; } = new()
        {
            new ItemTypeOption(
                ItemTypeCodes.StockItem,
                "Stock Item",
                "Tracks inventory and can be purchased, received, counted, adjusted, and sold."),

            new ItemTypeOption(
                ItemTypeCodes.Service,
                "Service",
                "Does not track stock or batches. It can have a UOM, VAT category, standard cost, retail price, and wholesale price and is available in Cashier.")
        };

        public ObservableCollection<MatrixPropertySelection> DynamicProperties { get; } = new();
        public ObservableCollection<MatrixPropertyGroupSelection> SelectedPropertyGroups { get; } = new();

        [ObservableProperty]
        private decimal _bulkCost = 0m;

        [ObservableProperty]
        private int _bulkReorderLevel = 0;

        [ObservableProperty]
        private decimal _bulkRetailMarkupPercent = 0m;

        [ObservableProperty]
        private decimal _bulkRetailPrice = 0m;

        [ObservableProperty]
        private decimal _bulkWholesaleMarkupPercent = 0m;

        [ObservableProperty]
        private decimal _bulkWholesalePrice = 0m;

        [ObservableProperty]
        private decimal _bulkMinimumPrice = 0m;

        [ObservableProperty]
        private decimal _bulkMaximumPrice = 0m;

        [ObservableProperty]
        private bool _bulkIsScaleItem = false;

        [ObservableProperty]
        private bool _bulkHasBatchTracking = true;

        [ObservableProperty]
        private bool _bulkHasExpiryTracking = false;

        [ObservableProperty]
        private bool _bulkHasBatchExpiry = false;

        [ObservableProperty]
        private bool _bulkIsSerialized = false;

        private int _currentPage = 1;
        private int _pageSize = 200;
        private bool _isFullyLoaded = false;

        [ObservableProperty]
        private bool _isLoadingMore;

        public ObservableCollection<ItemMasterSummaryDto> Items { get; } = new();

        [ObservableProperty]
        private string _masterSearchText = string.Empty;

        [ObservableProperty]
        private bool _includeDeactivatedItems = false;

        public ObservableCollection<string> ItemTypeFilters { get; } = new()
        {
            "All Items",
            "Stock Items",
            "Services"
        };

        [ObservableProperty]
        private string _selectedItemTypeFilter = "All Items";

        [ObservableProperty]
        private Category? _selectedCategoryFilter;

        [ObservableProperty]
        private SubCategory? _selectedSubCategoryFilter;

        public ObservableCollection<Category> DatabaseFilterCategories { get; } = new();
        public ObservableCollection<SubCategory> DatabaseFilterSubCategories { get; } = new();

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedDatabaseItem;

        public ObservableCollection<ItemVariant> GeneratedVariants { get; } = new();

        [ObservableProperty]
        private ItemVariant? _selectedVariantForSupplierEdit;

        [ObservableProperty]
        private bool _selectAllVariantsForSupplierAssignment = false;

        [ObservableProperty]
        private int _selectedSupplierAssignmentCount = 0;

        public ObservableCollection<Supplier> AvailableSuppliers { get; } = new();
        public ObservableCollection<ItemSupplier> SelectedVariantSuppliers { get; } = new();

        [ObservableProperty]
        private ItemSupplier? _selectedSupplierLinkForEdit;

        [ObservableProperty]
        private Supplier? _supplierToAdd;

        [ObservableProperty]
        private decimal _supplierCostInput = 0m;

        [ObservableProperty]
        private int _supplierMinimumOrderQuantityInput = 1;

        [ObservableProperty]
        private Supplier? _bulkSupplierToAssign;

        [ObservableProperty]
        private decimal _bulkSupplierCostInput = 0m;

        [ObservableProperty]
        private int _bulkSupplierMinimumOrderQuantityInput = 1;

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<SubCategory> SubCategories { get; } = new();
        public ObservableCollection<AttributeGroup> PropertyKeys { get; } = new();
        public ObservableCollection<AttributeValue> PropertyValues { get; } = new();
        public ObservableCollection<UnitOfMeasure> Uoms { get; } = new();
        public ObservableCollection<TaxCategory> TaxCategories { get; } = new();

        // Kept internally to resolve the currently effective Standard VAT code.
        public ObservableCollection<TaxRate> AvailableTaxes { get; } = new();

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _selectedMainTabIndex = 0;

        public bool IsExistingItem => CurrentItem.Id > 0;

        public bool HasItemHistory => IsExistingItem && _loadedItemHasHistory;

        public bool IsSetupEditable =>
            !IsBusy &&
            (!IsExistingItem || !_loadedItemHasHistory);

        public bool IsCurrentItemDeactivated =>
            IsExistingItem && _loadedItemWasDeactivated;

        public bool IsStockItem =>
            string.Equals(
                SelectedItemType?.Code ?? CurrentItem.ItemType,
                ItemTypeCodes.StockItem,
                StringComparison.Ordinal);

        public bool IsServiceItem =>
            string.Equals(
                SelectedItemType?.Code ?? CurrentItem.ItemType,
                ItemTypeCodes.Service,
                StringComparison.Ordinal);

        public bool IsSupplierManagementEnabled =>
            !IsBusy && IsStockItem;

        public bool IsTaxCategoryEditable =>
            !IsBusy && !IsCurrentItemDeactivated;

        public string ItemStatusText =>
            !IsExistingItem
                ? "New Item"
                : IsCurrentItemDeactivated
                    ? "Deactivated"
                    : HasItemHistory
                        ? "Active / History Locked"
                        : "Active / Unused";

        public string DeactivateReactivateButtonText =>
            IsCurrentItemDeactivated
                ? "REACTIVATE ITEM"
                : "DEACTIVATE ITEM";

        public string SelectedItemTypeDescription =>
            SelectedItemType?.Description ??
            "Select whether this record is a physical stock item or a non-stock service.";

        public string SelectedTaxCategoryText =>
            SelectedTaxCategory?.CategoryName ?? "Select a tax category";

        public string TaxCategoryHelpText
        {
            get
            {
                if (SelectedTaxCategory == null)
                    return "Select Standard VAT, Zero Rated, Exempt, or Out of Scope.";

                if (string.Equals(
                        SelectedTaxCategory.CategoryCode,
                        TaxCategoryCodes.Standard,
                        StringComparison.Ordinal))
                {
                    TaxRate? rate = GetCurrentStandardRate();

                    return rate == null
                        ? "No Standard VAT rate is effective today. Correct Tax Rate Management first."
                        : $"Current Standard VAT rate: {rate.RatePercent:N2}% from {rate.EffectiveFrom!.Value:yyyy-MM-dd}.";
                }

                return $"{SelectedTaxCategory.CategoryName} is a fixed 0% treatment. The category remains distinct for reporting.";
            }
        }

        public string StructureSafetyText =>
            !IsExistingItem
                ? "Structure can be edited until the item receives stock or transaction history."
                : HasItemHistory
                    ? "Item type, category, tracking, and variant structure are locked because history exists."
                    : "This item is unused. Its structure can still be corrected before transactions begin.";

        public string VariantBuilderHelpText =>
            IsServiceItem
                ? "Leave matrix values empty for one Standard service, or use variants for options such as A4/A3, colour/black-and-white, or service levels."
                : "Leave matrix values empty for one Standard stock item, or add property values to generate product variants.";

        public string BulkCostLabel =>
            IsServiceItem ? "Std. Cost" : "Cost Price";

        public string BulkRetailPriceLabel => "Retail Inc. VAT";

        public string BulkWholesalePriceLabel => "W/S Inc. VAT";

        public string BulkMinimumPriceLabel => "Min Inc. VAT";

        public string BulkMaximumPriceLabel => "Max Inc. VAT";

        public ItemMasterViewModel(
            ItemMasterRepository itemMasterRepository,
            CategoryRepository categoryRepository,
            SubCategoryRepository subCategoryRepository,
            AttributeRepository attributeRepository,
            UnitOfMeasureRepository uomRepository,
            SupplierRepository supplierRepository,
            StoreSettingsRepository storeSettingsRepository,
            TaxRateRepository taxRateRepository, // FIXED
            IMessageBoxService messageBoxService)
        {
            _itemMasterRepository = itemMasterRepository ?? throw new ArgumentNullException(nameof(itemMasterRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _subCategoryRepository = subCategoryRepository ?? throw new ArgumentNullException(nameof(subCategoryRepository));
            _storeSettingsRepository = storeSettingsRepository ?? throw new ArgumentNullException(nameof(storeSettingsRepository));
            _attributeRepository = attributeRepository ?? throw new ArgumentNullException(nameof(attributeRepository));
            _uomRepository = uomRepository ?? throw new ArgumentNullException(nameof(uomRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _taxRateRepository = taxRateRepository ?? throw new ArgumentNullException(nameof(taxRateRepository)); // FIXED
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        partial void OnCurrentItemChanged(ItemParent value)
        {
            RaiseItemStateProperties();
        }

        private void RaiseItemStateProperties()
        {
            OnPropertyChanged(nameof(IsExistingItem));
            OnPropertyChanged(nameof(HasItemHistory));
            OnPropertyChanged(nameof(IsSetupEditable));
            OnPropertyChanged(nameof(IsCurrentItemDeactivated));
            OnPropertyChanged(nameof(IsStockItem));
            OnPropertyChanged(nameof(IsServiceItem));
            OnPropertyChanged(nameof(IsSupplierManagementEnabled));
            OnPropertyChanged(nameof(IsTaxCategoryEditable));
            OnPropertyChanged(nameof(ItemStatusText));
            OnPropertyChanged(nameof(DeactivateReactivateButtonText));
            OnPropertyChanged(nameof(SelectedItemTypeDescription));
            OnPropertyChanged(nameof(SelectedTaxCategoryText));
            OnPropertyChanged(nameof(TaxCategoryHelpText));
            OnPropertyChanged(nameof(StructureSafetyText));
            OnPropertyChanged(nameof(VariantBuilderHelpText));
            OnPropertyChanged(nameof(BulkCostLabel));
            OnPropertyChanged(nameof(BulkRetailPriceLabel));
            OnPropertyChanged(nameof(BulkWholesalePriceLabel));
            OnPropertyChanged(nameof(BulkMinimumPriceLabel));
            OnPropertyChanged(nameof(BulkMaximumPriceLabel));

            DeleteUnusedItemCommand.NotifyCanExecuteChanged();
            DeactivateItemCommand.NotifyCanExecuteChanged();
            ReactivateItemCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            GenerateVariantsCommand.NotifyCanExecuteChanged();
            AddPropertyCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanInitialize))]
        private async Task InitializeAsync()
        {
            if (_isInitialized) return;
            IsBusy = true;

            try
            {
                await LoadLookupsAsync();
                SelectedCategoryFilter = DatabaseFilterCategories.FirstOrDefault();
                await UpdateDatabaseFilterSubCategoriesAsync(SelectedCategoryFilter?.Id);
                await LoadMasterGridInternalAsync();
                _isInitialized = true;
                StatusMessage = "Item Master page loaded.";
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                StatusMessage = "Failed to initialize Item Master.";
                _messageBoxService.ShowError($"Failed to initialize:\n\n{ex.Message}", "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadLookupsAsync()
        {
            Categories.Clear();
            Uoms.Clear();
            AvailableSuppliers.Clear();
            TaxCategories.Clear();
            AvailableTaxes.Clear();

            var categories = await _categoryRepository.GetAllAsync();
            
            DatabaseFilterCategories.Clear();
            DatabaseFilterCategories.Add(new Category { Id = 0, CategoryName = "All Categories" });

            foreach (var category in categories.Where(c => !c.IsDeactivated).OrderBy(c => c.CategoryName))
            {
                Categories.Add(category);
                DatabaseFilterCategories.Add(category);
            }

            var uoms = await _uomRepository.GetActiveAsync();
            foreach (var uom in uoms)
                Uoms.Add(uom);

            var suppliers = await _supplierRepository.GetActiveAsync();
            foreach (var supplier in suppliers.OrderBy(s => s.SupplierCode).ThenBy(s => s.SupplierName))
                AvailableSuppliers.Add(supplier);

            await _taxRateRepository.EnsureDefaultsAsync();

            var storeSettings = await _storeSettingsRepository.GetOrCreateDefaultAsync();
            _isVatRegisteredStore = !string.IsNullOrWhiteSpace(storeSettings.TaxNo);

            var taxCategories = await _taxRateRepository.GetApprovedCategoriesAsync();
            foreach (var taxCategory in taxCategories.Where(t => t.IsActive))
            {
                // Only show "Standard VAT" as an option if the store is VAT registered.
                if (!_isVatRegisteredStore &&
                    string.Equals(taxCategory.CategoryCode, TaxCategoryCodes.Standard, StringComparison.Ordinal))
                {
                    continue;
                }

                TaxCategories.Add(taxCategory);
            }

            var taxes = await _taxRateRepository.GetActiveAsync();
            foreach (var tax in taxes.OrderBy(t => t.DisplayOrder).ThenBy(t => t.TaxCode))
                AvailableTaxes.Add(tax);

            _isLoadingItem = true;
            try
            {
                SelectedUom = Uoms.FirstOrDefault();

                SelectedItemType =
                    ItemTypes.FirstOrDefault(t =>
                        string.Equals(t.Code, CurrentItem.ItemType, StringComparison.Ordinal))
                    ?? ItemTypes.FirstOrDefault(t => t.Code == ItemTypeCodes.StockItem);

                SelectedTaxCategory =
                    TaxCategories.FirstOrDefault(t => t.Id == CurrentItem.TaxCategoryId)
                    ?? ResolveUnambiguousLegacyTaxCategory(CurrentItem.TaxCode);

                if (SelectedTaxCategory == null)
                {
                    SelectedTaxCategory = TaxCategories.FirstOrDefault(t =>
                        t.CategoryCode == (_isVatRegisteredStore ? TaxCategoryCodes.Standard : TaxCategoryCodes.OutOfScope));
                }

                ApplyTaxCategoryToCurrentItem();
            }
            finally
            {
                _isLoadingItem = false;
            }

            RaiseItemStateProperties();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (_isClearing) return;

            if (value == null)
            {
                SubCategories.Clear();
                PropertyKeys.Clear();
                PropertyValues.Clear();
                return;
            }

            if (!_isLoadingItem && !IsSetupEditable)
            {
                StatusMessage = "Category is locked after the item is saved.";
                return;
            }

            CurrentItem.CategoryId = value.Id;

            if (!_isLoadingItem)
                _ = ApplyCategoryChangeAsync(value.Id);
        }

        private async Task ApplyCategoryChangeAsync(int categoryId)
        {
            try
            {
                IsBusy = true;
                SelectedSubCategory = null;
                ClearVariantBuilder();
                GeneratedVariants.Clear();
                _generatedVariantItemCode = string.Empty;
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;
                UpdateSupplierAssignmentSelectionCount();

                await LoadSubCategoriesAsync(categoryId);
                await LoadPropertyKeysForCategoryAsync(categoryId);

                StatusMessage = "Category changed. Sub-category is optional. Build variants again.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load category details.";
                _messageBoxService.ShowError($"Failed to load details:\n\n{ex.Message}", "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (_isLoadingItem || _isClearing)
                return;

            if (SelectedCategory == null || !IsSetupEditable)
                return;

            CurrentItem.SubCategoryId = value?.Id;
        }

        private async Task LoadSubCategoriesAsync(int categoryId)
        {
            SubCategories.Clear();
            var subCategories = await _subCategoryRepository.GetAllAsync();
            foreach (var subCategory in subCategories.Where(s => !s.IsDeactivated && s.CategoryId == categoryId).OrderBy(s => s.SubCategoryName))
                SubCategories.Add(subCategory);
        }

        private async Task LoadPropertyKeysForCategoryAsync(int categoryId)
        {
            PropertyKeys.Clear();
            PropertyValues.Clear();
            var groups = await _attributeRepository.GetAttributeGroupsForCategoryAsync(categoryId);
            foreach (var group in groups.Where(g => !g.IsDeactivated).OrderBy(g => g.DisplayOrder).ThenBy(g => g.GroupName))
                PropertyKeys.Add(group);
        }

        partial void OnSelectedPropertyKeyChanged(AttributeGroup? value)
        {
            AddPropertyCommand.NotifyCanExecuteChanged();
            if (value == null)
            {
                PropertyValues.Clear();
                PropertyValueInput = null;
                return;
            }
            _ = LoadPropertyValuesAsync(value.Id);
        }

        private async Task LoadPropertyValuesAsync(int groupId)
        {
            try
            {
                PropertyValues.Clear();
                var values = await _attributeRepository.GetAllValuesFilteredAsync(groupId, "");
                foreach (var value in values.Where(v => !v.IsDeactivated).OrderBy(v => v.DisplayOrder).ThenBy(v => v.ValueName))
                    PropertyValues.Add(value);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"Failed to load values:\n\n{ex.Message}", "Database Error");
            }
        }

        partial void OnPropertyValueInputChanged(AttributeValue? value)
        {
            AddPropertyCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedDatabaseItemChanged(ItemMasterSummaryDto? value)
        {
            if (_isClearing) return;

            if (value != null)
            {
                _ = LoadFullItemDetailsAsync(value.ParentId);
            }
            else
            {
                IsCodeReadOnly = false;
                RaiseItemStateProperties();
            }
        }

        [RelayCommand]
        private void EditItem(ItemMasterSummaryDto item)
        {
            if (item != null)
            {
                SelectedDatabaseItem = item;
                SelectedMainTabIndex = 1;
            }
        }

        [RelayCommand]
        private async Task DuplicateItemAsync(ItemMasterSummaryDto item)
        {
            if (item == null) return;

            // Load the full item into memory
            await LoadFullItemDetailsAsync(item.ParentId);

            // Strip identity to make it a new item
            CurrentItem.Id = 0;
            CurrentItem.ItemCode = string.Empty;
            ItemCodeInput = string.Empty;
            IsCodeReadOnly = false;

            _generatedVariantItemCode = string.Empty;

            foreach (var variant in GeneratedVariants)
            {
                variant.Id = 0;
                variant.ItemParentId = 0;
                variant.SkuCode = string.Empty;
                variant.Barcode = string.Empty;

                if (variant.PropertyMappings != null)
                {
                    foreach (var mapping in variant.PropertyMappings)
                    {
                        mapping.ItemVariantId = 0;
                    }
                }

                if (variant.ItemSuppliers != null)
                {
                    foreach (var supplierLink in variant.ItemSuppliers)
                    {
                        supplierLink.Id = 0;
                        supplierLink.ItemVariantId = 0;
                    }
                }
            }

            _loadedItemHasHistory = false;
            _loadedItemWasDeactivated = false;
            
            SelectedMainTabIndex = 1;
            StatusMessage = "Creating a duplicate. Please enter a new Item Code.";
            RaiseItemStateProperties();
        }

        private async Task LoadFullItemDetailsAsync(int parentId)
        {
            IsBusy = true;
            _isLoadingItem = true;

            try
            {
                var fullItem = await _itemMasterRepository.GetFullMatrixByIdAsync(parentId);
                if (fullItem == null)
                {
                    StatusMessage = "Selected item was not found.";
                    return;
                }

                _loadedItemHasHistory =
                    await _itemMasterRepository.ParentHasHistoryAsync(parentId);

                CurrentItem = fullItem;
                _loadedItemWasDeactivated = fullItem.IsDeactivated;

                SelectedItemType =
                    ItemTypes.FirstOrDefault(t =>
                        string.Equals(t.Code, fullItem.ItemType, StringComparison.Ordinal))
                    ?? ItemTypes.First(t => t.Code == ItemTypeCodes.StockItem);

                SelectedTaxCategory =
                    TaxCategories.FirstOrDefault(t => t.Id == fullItem.TaxCategoryId)
                    ?? ResolveUnambiguousLegacyTaxCategory(fullItem.TaxCode);

                IsCodeReadOnly = true;
                ItemCodeInput = fullItem.ItemCode;

                SelectedCategory = Categories.FirstOrDefault(c => c.Id == fullItem.CategoryId);
                await LoadSubCategoriesAsync(fullItem.CategoryId);
                SelectedSubCategory = fullItem.SubCategoryId.HasValue ? SubCategories.FirstOrDefault(s => s.Id == fullItem.SubCategoryId.Value) : null;
                await LoadPropertyKeysForCategoryAsync(fullItem.CategoryId);

                SelectedUom = Uoms.FirstOrDefault(u => u.Id == fullItem.UnitOfMeasureId) ??
                              Uoms.FirstOrDefault(u => string.Equals(u.UomCode, fullItem.BaseUom, StringComparison.OrdinalIgnoreCase));

                BulkIsScaleItem = fullItem.IsScaleItem;
                BulkHasBatchTracking = fullItem.HasBatchTracking;
                BulkHasExpiryTracking = fullItem.HasExpiryTracking || fullItem.HasBatchExpiry;
                BulkHasBatchExpiry = BulkHasExpiryTracking;
                BulkIsSerialized = fullItem.IsSerialized;

                GeneratedVariants.Clear();
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;

                foreach (var variant in fullItem.Variants.OrderBy(v => v.IsDeactivated).ThenBy(v => v.VariantDescription).ThenBy(v => v.SkuCode))
                {
                    variant.PropertyMappings ??= new List<ItemPropertyMapping>();
                    variant.ItemSuppliers ??= new List<ItemSupplier>();
                    variant.IsSelectedForSupplierAssignment = false;
                    ApplyParentDisplayNames(variant);
                    GeneratedVariants.Add(variant);
                }

                _generatedVariantItemCode = NormalizeCode(fullItem.ItemCode);
                ApplyServiceSafetyDefaults();
                RebuildBuilderSelectionFromVariants();
                UpdateSupplierAssignmentSelectionCount();

                int activeVariantCount = GeneratedVariants.Count(v => !v.IsDeactivated);

                StatusMessage = fullItem.IsDeactivated
                    ? "Loaded deactivated item."
                    : _loadedItemHasHistory
                        ? "Loaded active item. Structural fields are history locked."
                        : "Loaded active unused item. Structural corrections are still allowed.";

                RaiseItemStateProperties();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item.";
                _messageBoxService.ShowError($"Failed to load item:\n\n{ex.Message}", "Database Error");
            }
            finally
            {
                _isLoadingItem = false;
                IsBusy = false;
            }
        }

        private void RebuildBuilderSelectionFromVariants()
        {
            DynamicProperties.Clear();
            SelectedPropertyGroups.Clear();

            var mappings = GeneratedVariants
                .SelectMany(v => v.PropertyMappings)
                .Where(m => m.AttributeGroup != null && m.AttributeValue != null)
                .GroupBy(m => new { GroupId = m.AttributeGroupId, ValueId = m.AttributeValueId })
                .Select(g => g.First())
                .OrderBy(m => m.AttributeGroup.DisplayOrder)
                .ThenBy(m => m.AttributeGroup.GroupName)
                .ThenBy(m => m.AttributeValue.DisplayOrder)
                .ThenBy(m => m.AttributeValue.ValueName);

            foreach (var mapping in mappings)
            {
                AddSelectionToCollections(new MatrixPropertySelection
                {
                    Group = mapping.AttributeGroup,
                    Value = mapping.AttributeValue
                });
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddProperty))]
        private void AddProperty()
        {
            if (SelectedPropertyKey == null || PropertyValueInput == null) return;

            if (DynamicProperties.Any(p => p.Group.Id == SelectedPropertyKey.Id && p.Value.Id == PropertyValueInput.Id))
            {
                StatusMessage = "This property value is already selected.";
                return;
            }

            InvalidateGeneratedVariants(
                "Matrix properties changed. Generate variants again before saving.");
            AddSelectionToCollections(new MatrixPropertySelection { Group = SelectedPropertyKey, Value = PropertyValueInput });
            PropertyValueInput = null;
            StatusMessage = "Property value added. Generate variants before saving.";
        }

        private void AddSelectionToCollections(MatrixPropertySelection selection)
        {
            DynamicProperties.Add(selection);
            var groupSelection = SelectedPropertyGroups.FirstOrDefault(g => g.Group.Id == selection.Group.Id);

            if (groupSelection == null)
            {
                groupSelection = new MatrixPropertyGroupSelection { Group = selection.Group };
                SelectedPropertyGroups.Add(groupSelection);
            }
            groupSelection.Values.Add(selection);
        }

        [RelayCommand]
        private void RemoveProperty(MatrixPropertySelection? selection)
        {
            if (selection == null) return;
            if (!IsSetupEditable) return;

            InvalidateGeneratedVariants(
                "Matrix properties changed. Generate variants again before saving.");
            DynamicProperties.Remove(selection);
            var groupSelection = SelectedPropertyGroups.FirstOrDefault(g => g.Group.Id == selection.Group.Id);

            if (groupSelection != null)
            {
                var valueToRemove = groupSelection.Values.FirstOrDefault(v => v.Value.Id == selection.Value.Id);
                if (valueToRemove != null) groupSelection.Values.Remove(valueToRemove);
                if (!groupSelection.Values.Any()) SelectedPropertyGroups.Remove(groupSelection);
            }
            StatusMessage = "Property value removed. Generate variants before saving.";
        }

        private void ClearVariantBuilder()
        {
            DynamicProperties.Clear();
            SelectedPropertyGroups.Clear();
            PropertyValues.Clear();
            SelectedPropertyKey = null;
            PropertyValueInput = null;
        }

        [RelayCommand(CanExecute = nameof(CanGenerateVariants))]
        private async Task GenerateVariantsAsync()
        {
            string itemCode = BuildItemCode();
            if (!ValidateBeforeVariantGeneration(itemCode))
                return;

            if (GeneratedVariants.Any() || CurrentItem.Id > 0)
            {
                bool confirmed = _messageBoxService.ShowConfirmation(
                    "You already have generated variants for this item. Generating them again will overwrite the existing structure. Are you sure you want to proceed?",
                    "Confirm Generation");

                if (!confirmed)
                    return;
            }

            try
            {
                bool isStandardOnly = !DynamicProperties.Any();
                var existingSupplierLinks = CaptureSupplierLinksBySku();
                var generatedVariants = new List<ItemVariant>();
                var usedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var usedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (isStandardOnly)
                {
                    var standardVariant = new ItemVariant
                    {
                        SkuCode = itemCode,
                        VariantDescription = "Standard",
                        Barcode = await GenerateInternalBarcodeAsync(usedBarcodes),
                        AverageCost = BulkCost,
                        CostPrice = BulkCost,
                        RetailPrice = BulkRetailPrice,
                        WholesalePrice = BulkWholesalePrice,
                        MinimumPrice = BulkMinimumPrice,
                        MaximumPrice = BulkMaximumPrice,
                        ReorderLevel = IsServiceItem ? 0 : BulkReorderLevel,
                        ItemSuppliers = new List<ItemSupplier>(),
                        PropertyMappings = new List<ItemPropertyMapping>(),
                        IsSelectedForSupplierAssignment = false,
                        IsDeactivated = CurrentItem.IsDeactivated,
                        DeactivatedAt = CurrentItem.IsDeactivated ? DateTime.Now : null
                    };

                    ApplyParentDisplayNames(standardVariant);
                    RestoreSupplierLinksIfAvailable(standardVariant, existingSupplierLinks);
                    generatedVariants.Add(standardVariant);
                }
                else
                {
                    var groupedSelections = DynamicProperties
                        .GroupBy(p => p.Group.Id)
                        .Select(g => g.OrderBy(p => p.Value.DisplayOrder).ThenBy(p => p.Value.ValueName).ToList())
                        .ToList();

                    var combinations = GenerateCombinations(groupedSelections);

                    foreach (var combo in combinations)
                    {
                        string sku = BuildSkuForCombination(itemCode, combo, usedSkus);
                        string description = string.Join(" / ", combo.Select(c => c.Value.ValueName));

                        var variant = new ItemVariant
                        {
                            SkuCode = sku,
                            VariantDescription = description,
                            Barcode = await GenerateInternalBarcodeAsync(usedBarcodes),
                            AverageCost = BulkCost,
                            CostPrice = BulkCost,
                            RetailPrice = BulkRetailPrice,
                            WholesalePrice = BulkWholesalePrice,
                            MinimumPrice = BulkMinimumPrice,
                            MaximumPrice = BulkMaximumPrice,
                            ReorderLevel = IsServiceItem ? 0 : BulkReorderLevel,
                            ItemSuppliers = new List<ItemSupplier>(),
                            PropertyMappings = new List<ItemPropertyMapping>(),
                            IsSelectedForSupplierAssignment = false,
                            IsDeactivated = CurrentItem.IsDeactivated,
                            DeactivatedAt = CurrentItem.IsDeactivated ? DateTime.Now : null
                        };

                        foreach (var selection in combo)
                        {
                            variant.PropertyMappings.Add(new ItemPropertyMapping
                            {
                                AttributeGroupId = selection.Group.Id,
                                AttributeValueId = selection.Value.Id
                            });
                        }

                        ApplyParentDisplayNames(variant);
                        RestoreSupplierLinksIfAvailable(variant, existingSupplierLinks);
                        generatedVariants.Add(variant);
                    }
                }

                GeneratedVariants.Clear();
                foreach (ItemVariant variant in generatedVariants)
                    GeneratedVariants.Add(variant);

                _generatedVariantItemCode = NormalizeCode(itemCode);
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;
                UpdateSupplierAssignmentSelectionCount();
                StatusMessage = isStandardOnly
                    ? "1 standard variant generated."
                    : $"{generatedVariants.Count} variant(s) generated.";
                
                ShowTemporaryNotification("Variants successfully generated!");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Generate Item Master variants",
                    ex);

                StatusMessage = "Variant generation failed.";

                _messageBoxService.ShowError(
                    "Variants could not be generated. Existing variants were preserved. " +
                    "BackOffice will remain open. Technical details were saved in the " +
                    "local POS Logs folder.",
                    "Variant Generation Error");
            }
            finally
            {
                NotifyCommandStates();
            }
        }
        private Dictionary<string, List<ItemSupplier>> CaptureSupplierLinksBySku()
        {
            var result = new Dictionary<string, List<ItemSupplier>>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in GeneratedVariants)
            {
                if (string.IsNullOrWhiteSpace(variant.SkuCode)) continue;
                result[variant.SkuCode] = variant.ItemSuppliers?
                    .Select(s => new ItemSupplier
                    {
                        SupplierId = s.SupplierId,
                        Supplier = s.Supplier,
                        SupplierItemCode = string.Empty,
                        LastCostPrice = s.LastCostPrice,
                        IsPrimary = false,
                        MinimumOrderQuantity = s.MinimumOrderQuantity <= 0 ? 1 : s.MinimumOrderQuantity
                    }).ToList() ?? new List<ItemSupplier>();
            }
            return result;
        }

        private static void RestoreSupplierLinksIfAvailable(ItemVariant variant, Dictionary<string, List<ItemSupplier>> supplierLinksBySku)
        {
            if (!supplierLinksBySku.TryGetValue(variant.SkuCode, out var suppliers)) return;
            foreach (var supplier in suppliers) variant.ItemSuppliers.Add(supplier);
        }

        private static List<List<MatrixPropertySelection>> GenerateCombinations(List<List<MatrixPropertySelection>> groups, int depth = 0)
        {
            var result = new List<List<MatrixPropertySelection>>();
            if (depth == groups.Count)
            {
                result.Add(new List<MatrixPropertySelection>());
                return result;
            }
            var currentGroup = groups[depth];
            var nextCombinations = GenerateCombinations(groups, depth + 1);
            foreach (var selection in currentGroup)
            {
                foreach (var combo in nextCombinations)
                {
                    var newCombo = new List<MatrixPropertySelection> { selection };
                    newCombo.AddRange(combo);
                    result.Add(newCombo);
                }
            }
            return result;
        }

        private string BuildSkuForCombination(string itemCode, List<MatrixPropertySelection> combo, HashSet<string> usedSkus)
        {
            string suffix = string.Join("-", combo.Select(c => $"{SanitizeCodeSegment(c.Value.ValueName)}{c.Value.Id}"));
            string baseSku = $"{itemCode}-{suffix}";
            string sku = baseSku;
            int counter = 2;
            while (usedSkus.Contains(sku))
            {
                sku = $"{baseSku}-{counter}";
                counter++;
            }
            usedSkus.Add(sku);
            return sku;
        }

        private async Task<string> GenerateInternalBarcodeAsync(HashSet<string> usedBarcodes)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                string barcode = GenerateRandomDigits(12);
                if (usedBarcodes.Contains(barcode)) continue;
                if (!await _itemMasterRepository.IsBarcodeUniqueAsync(barcode)) continue;
                usedBarcodes.Add(barcode);
                return barcode;
            }
            throw new InvalidOperationException("Failed to generate a unique internal barcode.");
        }

        private static string GenerateRandomDigits(int length)
        {
            char[] digits = new char[length];
            for (int i = 0; i < length; i++) digits[i] = (char)('0' + _random.Next(0, 10));
            return new string(digits);
        }

        private static string SanitizeCodeSegment(string value)
        {
            string clean = new string((value ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(clean)) clean = "VAL";
            return clean.Length <= 4 ? clean : clean.Substring(0, 4);
        }

        // =========================================================
        // CORRECT BI-DIRECTIONAL PRICING LOGIC
        // =========================================================

        partial void OnBulkCostChanged(decimal value)
        {
            if (_isCalculatingPricing) return;
            _isCalculatingPricing = true;

            if (value > 0 && BulkRetailPrice > 0)
                BulkRetailMarkupPercent = Math.Round(((BulkRetailPrice - value) / value) * 100m, 2);

            if (value > 0 && BulkWholesalePrice > 0)
                BulkWholesaleMarkupPercent = Math.Round(((BulkWholesalePrice - value) / value) * 100m, 2);

            if (BulkSupplierCostInput <= 0) BulkSupplierCostInput = value;
            if (SupplierCostInput <= 0) SupplierCostInput = value;

            _isCalculatingPricing = false;
        }

        partial void OnBulkRetailMarkupPercentChanged(decimal value)
        {
            if (_isCalculatingPricing) return;
            _isCalculatingPricing = true;

            if (BulkCost > 0)
                BulkRetailPrice = Math.Round(BulkCost + (BulkCost * (value / 100m)), 2);

            _isCalculatingPricing = false;
        }

        partial void OnBulkRetailPriceChanged(decimal value)
        {
            if (_isCalculatingPricing) return;
            _isCalculatingPricing = true;

            if (BulkCost > 0)
                BulkRetailMarkupPercent = Math.Round(((value - BulkCost) / BulkCost) * 100m, 2);

            _isCalculatingPricing = false;
        }

        partial void OnBulkWholesaleMarkupPercentChanged(decimal value)
        {
            if (_isCalculatingPricing) return;
            _isCalculatingPricing = true;

            if (BulkCost > 0)
                BulkWholesalePrice = Math.Round(BulkCost + (BulkCost * (value / 100m)), 2);

            _isCalculatingPricing = false;
        }

        partial void OnBulkWholesalePriceChanged(decimal value)
        {
            if (_isCalculatingPricing) return;
            _isCalculatingPricing = true;

            if (BulkCost > 0)
                BulkWholesaleMarkupPercent = Math.Round(((value - BulkCost) / BulkCost) * 100m, 2);

            _isCalculatingPricing = false;
        }

        partial void OnBulkHasBatchTrackingChanged(bool value)
        {
            if (_isLoadingItem)
                return;

            if (IsServiceItem)
            {
                BulkHasBatchTracking = false;
                BulkHasExpiryTracking = false;
                BulkHasBatchExpiry = false;
                return;
            }

            if (HasItemHistory && value != CurrentItem.HasBatchTracking)
            {
                BulkHasBatchTracking = CurrentItem.HasBatchTracking;
                StatusMessage = "Batch tracking is locked because stock or transaction history exists.";
                return;
            }

            if (!value)
            {
                BulkHasExpiryTracking = false;
                BulkHasBatchExpiry = false;
            }

            NotifyCommandStates();
        }

        partial void OnBulkHasExpiryTrackingChanged(bool value)
        {
            if (_isLoadingItem)
                return;

            bool currentExpiryTracking =
                CurrentItem.HasExpiryTracking ||
                CurrentItem.HasBatchExpiry;

            if (IsServiceItem)
            {
                BulkHasExpiryTracking = false;
                BulkHasBatchExpiry = false;
                return;
            }

            if (HasItemHistory && value != currentExpiryTracking)
            {
                BulkHasExpiryTracking = currentExpiryTracking;
                BulkHasBatchExpiry = currentExpiryTracking;
                StatusMessage = "Expiry tracking is locked because history exists.";
                return;
            }

            if (value && !BulkHasBatchTracking)
            {
                BulkHasExpiryTracking = false;
                BulkHasBatchExpiry = false;
                return;
            }

            BulkHasBatchExpiry = value;
        }

        partial void OnBulkHasBatchExpiryChanged(bool value)
        {
            if (BulkHasExpiryTracking != value)
                BulkHasExpiryTracking = value;
        }

        partial void OnBulkIsScaleItemChanged(bool value)
        {
            if (_isLoadingItem)
                return;

            if (IsServiceItem)
            {
                BulkIsScaleItem = false;
                return;
            }

            if (HasItemHistory && value != CurrentItem.IsScaleItem)
            {
                BulkIsScaleItem = CurrentItem.IsScaleItem;
                StatusMessage = "Scale setting is locked because history exists.";
                return;
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void ApplyBulkDefaults()
        {
            if (!GeneratedVariants.Any()) return;

            bool confirmed = _messageBoxService.ShowConfirmation(
                "This will overwrite the pricing and settings for all generated variants. Are you sure you want to proceed?",
                "Confirm Bulk Apply");

            if (!confirmed)
                return;

            var variants = GeneratedVariants.ToList();
            foreach (var variant in variants)
            {
                variant.AverageCost = BulkCost;
                variant.CostPrice = BulkCost;
                variant.RetailPrice = BulkRetailPrice;
                variant.WholesalePrice = BulkWholesalePrice;
                variant.MinimumPrice = BulkMinimumPrice;
                variant.MaximumPrice = BulkMaximumPrice;
                variant.ReorderLevel = IsServiceItem ? 0 : BulkReorderLevel;

                if (IsServiceItem)
                {
                    variant.ItemSuppliers.Clear();
                }
                else
                {
                    // Also update the cost for any existing supplier links
                    foreach (var supplierLink in variant.ItemSuppliers)
                        supplierLink.LastCostPrice = BulkCost;
                }
 
                ApplyParentDisplayNames(variant);
            }
            RefreshGeneratedVariantGrid(variants);
            StatusMessage = "Bulk defaults applied.";
        }

        private void RefreshGeneratedVariantGrid(List<ItemVariant> variants)
        {
            var selectedSku = SelectedVariantForSupplierEdit?.SkuCode;
            GeneratedVariants.Clear();
            foreach (var variant in variants) GeneratedVariants.Add(variant);
            if (!string.IsNullOrWhiteSpace(selectedSku))
            {
                SelectedVariantForSupplierEdit = GeneratedVariants.FirstOrDefault(v => string.Equals(v.SkuCode, selectedSku, StringComparison.OrdinalIgnoreCase));
            }
            UpdateSupplierAssignmentSelectionCount();
        }

        partial void OnSelectAllVariantsForSupplierAssignmentChanged(bool value)
        {
            if (_isUpdatingSupplierSelection) return;
            var variants = GeneratedVariants.ToList();
            foreach (var variant in variants) variant.IsSelectedForSupplierAssignment = value;
            RefreshGeneratedVariantGrid(variants);
            UpdateSupplierAssignmentSelectionCount();
        }

        [RelayCommand]
        private void RefreshSupplierAssignmentSelection() => UpdateSupplierAssignmentSelectionCount();

        private void UpdateSupplierAssignmentSelectionCount()
        {
            int selectedCount = GeneratedVariants.Count(v => v.IsSelectedForSupplierAssignment);
            _isUpdatingSupplierSelection = true;
            try
            {
                SelectedSupplierAssignmentCount = selectedCount;
                SelectAllVariantsForSupplierAssignment = GeneratedVariants.Any() && selectedCount == GeneratedVariants.Count;
            }
            finally
            {
                _isUpdatingSupplierSelection = false;
            }
            AssignSupplierToSelectedVariantsCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanAssignSupplierToSelectedVariants))]
        private void AssignSupplierToSelectedVariants()
        {
            try
            {
                UpdateSupplierAssignmentSelectionCount();
                var selectedVariants = GeneratedVariants
                    .Where(v => v.IsSelectedForSupplierAssignment)
                    .ToList();

                if (!selectedVariants.Any())
                    return;

                // The dialog no longer needs a default cost. We assume its constructor
                // and UI have been updated to remove the cost field.
                var dialog = new AssignVariantSuppliersDialog(
                    AvailableSuppliers.ToList(),
                    1, // Default MOQ
                    selectedVariants.Count)
                {
                    Owner = GetDialogOwner()
                };

                if (dialog.ShowDialog() != true ||
                    dialog.SelectedSupplier == null)
                {
                    return;
                }

                int addedCount = 0;
                int updatedCount = 0;

                foreach (var variant in selectedVariants)
                {
                    // Use the variant's own CostPrice instead of a single cost from the dialog.
                    AddOrUpdateSupplierLinkForVariant(
                        variant,
                        dialog.SelectedSupplier,
                        variant.CostPrice,
                        dialog.MinimumOrderQuantity,
                        ref addedCount,
                        ref updatedCount);
                }

                if (SelectedVariantForSupplierEdit != null &&
                    selectedVariants.Any(v => string.Equals(
                        v.SkuCode,
                        SelectedVariantForSupplierEdit.SkuCode,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    RebuildSelectedVariantSuppliers();
                }

                StatusMessage =
                    $"Assigned: {addedCount}, Updated: {updatedCount}. Click SAVE.";

                NotifyCommandStates();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Assign suppliers to Item Master variants",
                    ex);

                _messageBoxService.ShowError(
                    "Supplier assignment could not be completed. Existing variant " +
                    "supplier links were preserved where possible. BackOffice will " +
                    "remain open. Technical details were saved in the local POS Logs folder.",
                    "Supplier Assignment Error");
            }
        }

        private void AddOrUpdateSupplierLinkForVariant(ItemVariant variant, Supplier supplier, decimal cost, int moq, ref int addedCount, ref int updatedCount)
        {
            variant.ItemSuppliers ??= new List<ItemSupplier>();
            var existing = variant.ItemSuppliers.FirstOrDefault(s => s.SupplierId == supplier.Id);
            if (existing == null)
            {
                variant.ItemSuppliers.Add(new ItemSupplier { SupplierId = supplier.Id, Supplier = supplier, ItemVariantId = variant.Id, LastCostPrice = cost, MinimumOrderQuantity = moq });
                addedCount++;
            }
            else
            {
                existing.Supplier = supplier;
                existing.LastCostPrice = cost;
                existing.MinimumOrderQuantity = moq;
                updatedCount++;
            }
        }

        private static Window? GetDialogOwner() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

        partial void OnSelectedVariantForSupplierEditChanged(ItemVariant? value)
        {
            SelectedVariantSuppliers.Clear();
            SelectedSupplierLinkForEdit = null;
            if (value == null)
            {
                SupplierToAdd = null;
                SupplierCostInput = 0m;
                SupplierMinimumOrderQuantityInput = 1;
                AddSupplierToVariantCommand.NotifyCanExecuteChanged();
                return;
            }
            value.ItemSuppliers ??= new List<ItemSupplier>();
            foreach (var supplier in value.ItemSuppliers) SelectedVariantSuppliers.Add(supplier);
            SupplierCostInput = value.CostPrice;
            SupplierMinimumOrderQuantityInput = 1;
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedSupplierLinkForEditChanged(ItemSupplier? value)
        {
            if (value == null) return;
            SupplierToAdd = AvailableSuppliers.FirstOrDefault(s => s.Id == value.SupplierId) ?? value.Supplier;
            SupplierCostInput = value.LastCostPrice;
            SupplierMinimumOrderQuantityInput = value.MinimumOrderQuantity <= 0 ? 1 : value.MinimumOrderQuantity;
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void ClearSupplierEdit()
        {
            SelectedSupplierLinkForEdit = null;
            SupplierToAdd = null;
            SupplierCostInput = SelectedVariantForSupplierEdit?.CostPrice ?? 0m;
            SupplierMinimumOrderQuantityInput = 1;
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierToAddChanged(Supplier? value) => AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        partial void OnSupplierCostInputChanged(decimal value) => AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        partial void OnSupplierMinimumOrderQuantityInputChanged(int value) => AddSupplierToVariantCommand.NotifyCanExecuteChanged();

        [RelayCommand(CanExecute = nameof(CanAddSupplierToVariant))]
        private void AddSupplierToVariant()
        {
            if (SelectedVariantForSupplierEdit == null || SupplierToAdd == null) return;

            SelectedVariantForSupplierEdit.ItemSuppliers ??= new List<ItemSupplier>();
            var existing = SelectedVariantForSupplierEdit.ItemSuppliers.FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);

            if (existing != null)
            {
                existing.Supplier = SupplierToAdd;
                existing.LastCostPrice = SupplierCostInput;
                existing.MinimumOrderQuantity = SupplierMinimumOrderQuantityInput;
            }
            else
            {
                SelectedVariantForSupplierEdit.ItemSuppliers.Add(new ItemSupplier
                {
                    SupplierId = SupplierToAdd.Id,
                    Supplier = SupplierToAdd,
                    ItemVariantId = SelectedVariantForSupplierEdit.Id,
                    LastCostPrice = SupplierCostInput,
                    MinimumOrderQuantity = SupplierMinimumOrderQuantityInput
                });
            }

            RebuildSelectedVariantSuppliers();
            SelectedSupplierLinkForEdit = SelectedVariantSuppliers.FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);
            StatusMessage = "Supplier link added/updated.";
        }

        [RelayCommand]
        private void RemoveSupplierFromVariant(ItemSupplier? itemSupplier)
        {
            if (itemSupplier == null || SelectedVariantForSupplierEdit == null) return;
            var suppliers = SelectedVariantForSupplierEdit.ItemSuppliers?.ToList() ?? new List<ItemSupplier>();
            var removeTarget = suppliers.FirstOrDefault(s => s.SupplierId == itemSupplier.SupplierId);
            if (removeTarget != null) suppliers.Remove(removeTarget);
            SelectedVariantForSupplierEdit.ItemSuppliers = suppliers;
            if (SelectedSupplierLinkForEdit?.SupplierId == itemSupplier.SupplierId) ClearSupplierEdit();
            RebuildSelectedVariantSuppliers();
        }

        private void RebuildSelectedVariantSuppliers()
        {
            SelectedVariantSuppliers.Clear();
            if (SelectedVariantForSupplierEdit?.ItemSuppliers == null) return;
            foreach (var supplier in SelectedVariantForSupplierEdit.ItemSuppliers.OrderBy(s => s.Supplier?.SupplierCode))
            {
                supplier.SupplierItemCode = string.Empty;
                supplier.IsPrimary = false;
                SelectedVariantSuppliers.Add(supplier);
            }
        }

        partial void OnBulkSupplierToAssignChanged(Supplier? value) => ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
        partial void OnBulkSupplierCostInputChanged(decimal value) => ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
        partial void OnBulkSupplierMinimumOrderQuantityInputChanged(int value) => ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();

        [RelayCommand(CanExecute = nameof(CanApplySupplierToAllVariants))]
        private void ApplySupplierToAllVariants()
        {
            if (!GeneratedVariants.Any() || BulkSupplierToAssign == null) return;

            foreach (var variant in GeneratedVariants)
            {
                variant.ItemSuppliers ??= new List<ItemSupplier>();
                var existing = variant.ItemSuppliers.FirstOrDefault(s => s.SupplierId == BulkSupplierToAssign.Id);
                if (existing == null)
                {
                    variant.ItemSuppliers.Add(new ItemSupplier
                    {
                        SupplierId = BulkSupplierToAssign.Id,
                        Supplier = BulkSupplierToAssign,
                        ItemVariantId = variant.Id,
                        LastCostPrice = BulkSupplierCostInput,
                        MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput
                    });
                }
                else
                {
                    existing.Supplier = BulkSupplierToAssign;
                    existing.LastCostPrice = BulkSupplierCostInput;
                    existing.MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput;
                }
            }

            if (SelectedVariantForSupplierEdit != null) RebuildSelectedVariantSuppliers();
            StatusMessage = "Bulk suppliers assigned.";
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string itemCode = BuildItemCode();
            ApplyParentActivationStateToGeneratedVariants();
            if (!ValidateBeforeSave(itemCode)) return;

            bool saveStartedAsNewItem = CurrentItem.Id == 0;
            // For new items, if the code is blank, we are relying on the repository
            // to auto-generate a unique code. We can therefore skip the client-side
            // uniqueness check which would otherwise fail.
            bool isAutoGeneratingCode = saveStartedAsNewItem && string.IsNullOrWhiteSpace(ItemCodeInput);

            IsBusy = true;
            try
            {
                ApplyParentDisplayNamesToAllVariants();

                // Only perform the client-side uniqueness check for manually entered full codes.
                // For auto-generated codes, the repository guarantees uniqueness.
                if (!isAutoGeneratingCode && !await _itemMasterRepository.IsItemCodeUniqueAsync(itemCode, CurrentItem.Id))
                {
                    _messageBoxService.ShowWarning($"Item code '{itemCode}' already exists.", "Duplicate");
                    return;
                }

                CurrentItem.ItemCode = itemCode;
                CurrentItem.CategoryId = SelectedCategory!.Id;
                CurrentItem.SubCategoryId = SelectedSubCategory?.Id;
                CurrentItem.UnitOfMeasureId = SelectedUom!.Id;
                CurrentItem.BaseUom = SelectedUom.UomCode;

                CurrentItem.ItemType = SelectedItemType!.Code;
                CurrentItem.TaxCategoryId = SelectedTaxCategory!.Id;
                CurrentItem.TaxCode = ResolveLegacyTaxCode();
                CurrentItem.IsTaxInclusive = true;

                CurrentItem.IsScaleItem =
                    IsStockItem && BulkIsScaleItem;

                CurrentItem.HasBatchTracking =
                    IsStockItem && BulkHasBatchTracking;

                CurrentItem.HasExpiryTracking =
                    IsStockItem &&
                    BulkHasBatchTracking &&
                    BulkHasExpiryTracking;

                CurrentItem.HasBatchExpiry =
                    CurrentItem.HasExpiryTracking;

                CurrentItem.IsSerialized =
                    IsStockItem && BulkIsSerialized;

                if (IsServiceItem)
                    CurrentItem.IsPurchaseLocked = true;

                CurrentItem.Category = null!;
                CurrentItem.SubCategory = null;
                CurrentItem.UnitOfMeasure = null!;
                CurrentItem.TaxCategory = null;
                CurrentItem.Variants = new List<ItemVariant>();

                foreach (var variant in GeneratedVariants)
                {
                    if (IsServiceItem)
                    {
                        variant.ReorderLevel = 0;
                        variant.ItemSuppliers.Clear();
                    }
                }

                var mappingsList = GeneratedVariants.SelectMany(v => v.PropertyMappings).ToList();
                await _itemMasterRepository.SaveFullMatrixAsync(CurrentItem, GeneratedVariants.ToList(), mappingsList);
                await LoadMasterGridInternalAsync();
                Clear();
                ShowTemporaryNotification("Item saved successfully!");
            }
            catch (InvalidOperationException ex)
            {
                if (saveStartedAsNewItem)
                    ResetTransientNewItemIdentities();

                StatusMessage =
                    "Validation failed. Correct the highlighted errors and try again.";

                string userMessage =
                    ItemMasterSaveFailureFormatter.GetUserMessage(ex);

                _messageBoxService.ShowError(
                    userMessage,
                    "Validation Error");
            }
            catch (Exception ex)
            {
                if (saveStartedAsNewItem)
                    ResetTransientNewItemIdentities();

                LocalLogService.WriteException(
                    "BackOffice",
                    "Save Item Master item",
                    ex);

                StatusMessage =
                    "Item save failed. No item data was committed; correct the form and try again.";

                string userMessage =
                    ItemMasterSaveFailureFormatter.GetUserMessage(ex);

                _messageBoxService.ShowError(
                    $"Failed to save item:\n\n{userMessage}\n\n" +
                    "No item data was committed. The form has been kept for correction. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyParentActivationStateToGeneratedVariants()
        {
            if (!GeneratedVariants.Any()) return;
            DateTime now = DateTime.Now;
            if (CurrentItem.Id == 0)
            {
                CurrentItem.IsDeactivated = false;
                CurrentItem.DeactivatedAt = null;
                return;
            }
            CurrentItem.IsDeactivated = _loadedItemWasDeactivated;
            if (CurrentItem.IsDeactivated)
            {
                CurrentItem.DeactivatedAt ??= now;
                foreach (var variant in GeneratedVariants)
                {
                    variant.IsDeactivated = true;
                    variant.DeactivatedAt ??= now;
                }
            }
            else
            {
                CurrentItem.DeactivatedAt = null;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadMasterGridAsync()
        {
            IsBusy = true;
            try
            {
                await LoadMasterGridInternalAsync();
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"Failed to load item database:\n\n{ex.Message}", "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadMasterGridInternalAsync()
        {
            Items.Clear();
            _currentPage = 1;
            _isFullyLoaded = false;

            string? itemType = SelectedItemTypeFilter switch
            {
                "Stock Items" => ItemTypeCodes.StockItem,
                "Services" => ItemTypeCodes.Service,
                _ => null
            };

            // 1. Fetch exact total counts for UI metrics without fetching the data payload
            var counts = await _itemMasterRepository.GetItemsSummaryCountsAsync(
                searchTerm: MasterSearchText,
                includeDeactivated: IncludeDeactivatedItems,
                itemType: itemType,
                categoryId: SelectedCategoryFilter?.Id > 0 ? SelectedCategoryFilter.Id : null,
                subCategoryId: SelectedSubCategoryFilter?.Id > 0 ? SelectedSubCategoryFilter.Id : null);

            TotalLoadedItems = counts.TotalItems;
            TotalLoadedVariants = counts.TotalVariants;
            ActiveItems = counts.ActiveItems;
            DeactivatedItems = counts.DeactivatedItems;

            // 2. Fetch the first batch (PageSize)
            await LoadMoreItemsInternalAsync();

            StatusMessage =
                $"Total Database Items: {TotalLoadedItems}. Filter: {SelectedItemTypeFilter}.";
        }

        [RelayCommand]
        private async Task LoadMoreItemsAsync()
        {
            if (_isFullyLoaded || IsLoadingMore || IsBusy) return;

            IsLoadingMore = true;
            try
            {
                await LoadMoreItemsInternalAsync();
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"Failed to load more items:\n\n{ex.Message}", "Database Error");
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task LoadMoreItemsInternalAsync()
        {
            if (_isFullyLoaded) return;

            string? itemType = SelectedItemTypeFilter switch
            {
                "Stock Items" => ItemTypeCodes.StockItem,
                "Services" => ItemTypeCodes.Service,
                _ => null
            };

            var data = await _itemMasterRepository.GetSummariesPagedAsync(
                searchTerm: MasterSearchText,
                includeDeactivated: IncludeDeactivatedItems,
                itemType: itemType,
                categoryId: SelectedCategoryFilter?.Id > 0 ? SelectedCategoryFilter.Id : null,
                subCategoryId: SelectedSubCategoryFilter?.Id > 0 ? SelectedSubCategoryFilter.Id : null,
                pageNumber: _currentPage,
                pageSize: _pageSize);

            if (data.Count < _pageSize)
            {
                _isFullyLoaded = true;
            }

            foreach (var item in data)
            {
                Items.Add(item);
            }

            if (data.Any())
            {
                _currentPage++;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchItemsAsync() => await LoadMasterGridAsync();

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshDatabaseAsync()
        {
            MasterSearchText = string.Empty;
            await LoadMasterGridAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearFiltersAsync()
        {
            _isInitialized = false;
            MasterSearchText = string.Empty;
            SelectedItemTypeFilter = "All Items";
            SelectedCategoryFilter = DatabaseFilterCategories.FirstOrDefault();
            await UpdateDatabaseFilterSubCategoriesAsync(SelectedCategoryFilter?.Id);
            IncludeDeactivatedItems = false;
            _isInitialized = true;
            await LoadMasterGridAsync();
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedMainTabIndex = 1;
            _isClearing = true;
            _loadedItemWasDeactivated = false;
            _loadedItemHasHistory = false;

            CurrentItem = new ItemParent
            {
                ItemType = ItemTypeCodes.StockItem,
                HasBatchTracking = true,
                HasExpiryTracking = false,
                HasBatchExpiry = false,
                AllowCashierDiscount = true,
                IsTaxInclusive = true,
                IsPurchaseLocked = false
            };

            SelectedItemType =
                ItemTypes.FirstOrDefault(t =>
                    t.Code == ItemTypeCodes.StockItem);

            SelectedTaxCategory =
                TaxCategories.FirstOrDefault(t =>
                    t.CategoryCode == (_isVatRegisteredStore ? TaxCategoryCodes.Standard : TaxCategoryCodes.OutOfScope))
                ?? TaxCategories.FirstOrDefault();

            ApplyTaxCategoryToCurrentItem();

            ItemCodeInput = string.Empty;
            IsCodeReadOnly = false;
            SelectedCategory = null;
            SelectedSubCategory = null;
            SubCategories.Clear();
            SelectedUom = Uoms.FirstOrDefault();

            BulkCost = 0m;
            BulkReorderLevel = 0;
            BulkRetailMarkupPercent = 0m;
            BulkRetailPrice = 0m;
            BulkWholesaleMarkupPercent = 0m;
            BulkWholesalePrice = 0m;
            BulkMinimumPrice = 0m;
            BulkMaximumPrice = 0m;

            BulkIsScaleItem = false;
            BulkHasBatchTracking = true;
            BulkHasExpiryTracking = false;
            BulkHasBatchExpiry = false;
            BulkIsSerialized = false;

            ClearVariantBuilder();
            PropertyKeys.Clear();
            GeneratedVariants.Clear();
            _generatedVariantItemCode = string.Empty;
            SelectedVariantSuppliers.Clear();
            SelectedVariantForSupplierEdit = null;
            UpdateSupplierAssignmentSelectionCount();

            SupplierToAdd = null;
            SupplierCostInput = 0m;
            SupplierMinimumOrderQuantityInput = 1;
            BulkSupplierToAssign = null;
            BulkSupplierCostInput = 0m;
            BulkSupplierMinimumOrderQuantityInput = 1;
            SelectedDatabaseItem = null;

            _isClearing = false;
            StatusMessage = "Ready for new item.";
            RaiseItemStateProperties();
            NotifyCommandStates();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteUnusedItem))]
        private async Task DeleteUnusedItemAsync()
        {
            int parentId = GetCurrentParentId();
            if (parentId <= 0)
                return;

            IsBusy = true;

            try
            {
                var deleteCheck = await _itemMasterRepository
                    .CanHardDeleteMatrixAsync(parentId);

                if (!deleteCheck.CanDelete)
                {
                    _messageBoxService.ShowWarning(
                        "This item cannot be deleted.",
                        "Delete Blocked");
                    return;
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Check Item Master hard-delete eligibility",
                    ex);

                _messageBoxService.ShowError(
                    "The item delete check could not be completed. " +
                    "BackOffice will remain open. Technical details were saved in " +
                    "the local POS Logs folder.",
                    "Delete Check Failed");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            if (!_messageBoxService.ShowConfirmation(
                    $"Permanently delete unused item '{GetCurrentItemName()}'?",
                    "Confirm Delete",
                    MessageBoxImage.Warning))
            {
                return;
            }

            IsBusy = true;

            try
            {
                await _itemMasterRepository.HardDeleteMatrixAsync(parentId);
                await LoadMasterGridInternalAsync();
                Clear();
                _messageBoxService.ShowInformation("Unused item deleted.", "Deleted");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Delete unused Item Master item",
                    ex);

                _messageBoxService.ShowError(
                    "The unused item could not be deleted. BackOffice will remain open. " +
                    "Refresh the item list before trying again.",
                    "Delete Failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeactivateItem))]
        private async Task DeactivateItemAsync()
        {
            int parentId = GetCurrentParentId();
            if (parentId <= 0)
                return;

            if (!_messageBoxService.ShowConfirmation(
                    $"Deactivate item '{GetCurrentItemName()}'?",
                    "Confirm Deactivation",
                    MessageBoxImage.Warning))
            {
                return;
            }

            IsBusy = true;

            try
            {
                await _itemMasterRepository.DeactivateMatrixAsync(parentId);
                await LoadMasterGridInternalAsync();
                Clear();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Deactivate Item Master item",
                    ex);

                _messageBoxService.ShowError(
                    "The item could not be deactivated. BackOffice will remain open. " +
                    "Refresh the item list before trying again.",
                    "Deactivation Failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanReactivateItem))]
        private async Task ReactivateItemAsync()
        {
            int parentId = GetCurrentParentId();
            if (parentId <= 0)
                return;

            if (!_messageBoxService.ShowConfirmation(
                    $"Reactivate item '{GetCurrentItemName()}'?",
                    "Confirm Reactivation",
                    MessageBoxImage.Question))
            {
                return;
            }

            IsBusy = true;

            try
            {
                await _itemMasterRepository.ReactivateMatrixAsync(parentId);
                await LoadMasterGridInternalAsync();
                Clear();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Reactivate Item Master item",
                    ex);

                _messageBoxService.ShowError(
                    "The item could not be reactivated. BackOffice will remain open. " +
                    "Refresh the item list before trying again.",
                    "Reactivation Failed");
            }
            finally
            {
                IsBusy = false;
            }
        }
        [RelayCommand(CanExecute = nameof(CanDeactivateItem))]
        private async Task DeleteAsync() => await DeactivateItemAsync();

        private int GetCurrentParentId() => CurrentItem.Id > 0 ? CurrentItem.Id : SelectedDatabaseItem?.ParentId ?? 0;
        private string GetCurrentItemName() => !string.IsNullOrWhiteSpace(CurrentItem.ItemName) ? CurrentItem.ItemName.Trim() : SelectedDatabaseItem?.ItemName ?? "selected item";

        partial void OnMasterSearchTextChanged(string value) { if (_isInitialized) StatusMessage = "Type search text and click SEARCH."; }
        partial void OnIncludeDeactivatedItemsChanged(bool value) { if (_isInitialized && !IsBusy) _ = LoadMasterGridAsync(); }
        partial void OnSelectedItemTypeFilterChanged(string value) { if (_isInitialized && !IsBusy) _ = LoadMasterGridAsync(); }

        partial void OnSelectedCategoryFilterChanged(Category? value)
        {
            if (_isInitialized)
            {
                _ = UpdateDatabaseFilterSubCategoriesAsync(value?.Id);
                if (!IsBusy) _ = LoadMasterGridAsync();
            }
        }

        partial void OnSelectedSubCategoryFilterChanged(SubCategory? value)
        {
            if (_isInitialized && !IsBusy) _ = LoadMasterGridAsync();
        }

        private async Task UpdateDatabaseFilterSubCategoriesAsync(int? categoryId)
        {
            DatabaseFilterSubCategories.Clear();
            DatabaseFilterSubCategories.Add(new SubCategory { Id = 0, SubCategoryName = "All Subcategories" });

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                var subCategories = await _subCategoryRepository.GetAllAsync();
                foreach (var subCategory in subCategories.Where(s => !s.IsDeactivated && s.CategoryId == categoryId.Value).OrderBy(s => s.SubCategoryName))
                {
                    DatabaseFilterSubCategories.Add(subCategory);
                }
            }
            
            SelectedSubCategoryFilter = DatabaseFilterSubCategories.FirstOrDefault();
        }

        partial void OnItemCodeInputChanged(string value) =>
            HandleItemCodeInputChanged();

        partial void OnSelectedUomChanged(UnitOfMeasure? value) =>
            SaveCommand.NotifyCanExecuteChanged();

        partial void OnSelectedItemTypeChanged(ItemTypeOption? value)
        {
            if (_isApplyingItemType || value == null)
                return;

            if (_isLoadingItem)
            {
                CurrentItem.ItemType = value.Code;
                RaiseItemStateProperties();
                return;
            }

            _isApplyingItemType = true;

            try
            {
                if (!_isLoadingItem &&
                    HasItemHistory &&
                    !string.Equals(
                        value.Code,
                        CurrentItem.ItemType,
                        StringComparison.Ordinal))
                {
                    SelectedItemType =
                        ItemTypes.FirstOrDefault(t =>
                            string.Equals(
                                t.Code,
                                CurrentItem.ItemType,
                                StringComparison.Ordinal));

                    StatusMessage =
                        "Item type is locked because stock or transaction history exists.";

                    return;
                }

                CurrentItem.ItemType = value.Code;

                if (string.Equals(
                        value.Code,
                        ItemTypeCodes.Service,
                        StringComparison.Ordinal))
                {
                    ApplyServiceSafetyDefaults();
                }
                else if (!HasItemHistory)
                {
                    CurrentItem.IsPurchaseLocked = false;
                    BulkHasBatchTracking = true;
                }

                StatusMessage =
                    string.Equals(value.Code, ItemTypeCodes.Service, StringComparison.Ordinal)
                        ? "Service selected. Stock, batch, GRN, and supplier-inventory controls are disabled."
                        : "Stock Item selected. Choose the required stock-tracking method.";
            }
            finally
            {
                _isApplyingItemType = false;
                RaiseItemStateProperties();
                NotifyCommandStates();
            }
        }

        private void ApplyServiceSafetyDefaults()
        {
            if (!IsServiceItem)
                return;

            BulkHasBatchTracking = false;
            BulkHasExpiryTracking = false;
            BulkHasBatchExpiry = false;
            BulkIsScaleItem = false;
            BulkIsSerialized = false;
            BulkReorderLevel = 0;

            CurrentItem.HasBatchTracking = false;
            CurrentItem.HasExpiryTracking = false;
            CurrentItem.HasBatchExpiry = false;
            CurrentItem.IsScaleItem = false;
            CurrentItem.IsSerialized = false;
            CurrentItem.IsPurchaseLocked = true;

            foreach (var variant in GeneratedVariants)
            {
                variant.ReorderLevel = 0;
                variant.ItemSuppliers.Clear();
                variant.IsSelectedForSupplierAssignment = false;
            }

            SelectedVariantSuppliers.Clear();
            SelectedSupplierAssignmentCount = 0;
            SelectAllVariantsForSupplierAssignment = false;
        }

        partial void OnSelectedTaxCategoryChanged(TaxCategory? value)
        {
            ApplyTaxCategoryToCurrentItem();
            OnPropertyChanged(nameof(SelectedTaxCategoryText));
            OnPropertyChanged(nameof(TaxCategoryHelpText));
            SaveCommand.NotifyCanExecuteChanged();
        }

        private void ApplyTaxCategoryToCurrentItem()
        {
            CurrentItem.TaxCategoryId = SelectedTaxCategory?.Id;
            CurrentItem.TaxCode = ResolveLegacyTaxCode();
            CurrentItem.IsTaxInclusive = true;
        }

        private string ResolveLegacyTaxCode()
        {
            if (SelectedTaxCategory == null)
                return string.Empty;

            if (string.Equals(
                    SelectedTaxCategory.CategoryCode,
                    TaxCategoryCodes.Standard,
                    StringComparison.Ordinal))
            {
                return GetCurrentStandardRate()?.TaxCode ?? "VAT-STD";
            }

            return "TAX-FREE";
        }

        private TaxCategory? ResolveUnambiguousLegacyTaxCategory(
            string? taxCode)
        {
            string code = NormalizeCode(taxCode);

            if (code.StartsWith("VAT-STD", StringComparison.Ordinal))
            {
                return TaxCategories.FirstOrDefault(t =>
                    t.CategoryCode == TaxCategoryCodes.Standard);
            }

            return null;
        }

        private TaxRate? GetCurrentStandardRate()
        {
            DateTime today = DateTime.Today;

            return AvailableTaxes
                .Where(t =>
                    t.IsActive &&
                    t.TaxCategory != null &&
                    t.TaxCategory.CategoryCode == TaxCategoryCodes.Standard &&
                    t.EffectiveFrom.HasValue &&
                    t.EffectiveFrom.Value.Date <= today &&
                    (!t.EffectiveTo.HasValue ||
                     t.EffectiveTo.Value.Date >= today))
                .OrderByDescending(t => t.EffectiveFrom)
                .FirstOrDefault();
        }

        partial void OnIsBusyChanged(bool value)
        {
            RaiseItemStateProperties();
            NotifyCommandStates();
        }

        private void NotifyCommandStates()
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadMasterGridCommand.NotifyCanExecuteChanged();
            SearchItemsCommand.NotifyCanExecuteChanged();
            RefreshDatabaseCommand.NotifyCanExecuteChanged();
            AddPropertyCommand.NotifyCanExecuteChanged();
            GenerateVariantsCommand.NotifyCanExecuteChanged();
            RefreshSupplierAssignmentSelectionCommand.NotifyCanExecuteChanged();
            AssignSupplierToSelectedVariantsCommand.NotifyCanExecuteChanged();
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
            ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteUnusedItemCommand.NotifyCanExecuteChanged();
            DeactivateItemCommand.NotifyCanExecuteChanged();
            ReactivateItemCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        private bool CanInitialize() => !IsBusy && !_isInitialized;
        private bool CanRunCommand() => !IsBusy;
        private bool CanAddProperty() =>
            !IsBusy &&
            IsSetupEditable &&
            SelectedPropertyKey != null &&
            PropertyValueInput != null;

        private bool CanGenerateVariants() =>
            !IsBusy && IsSetupEditable;

        private bool CanAssignSupplierToSelectedVariants() =>
            !IsBusy &&
            IsStockItem &&
            GeneratedVariants.Any(v => v.IsSelectedForSupplierAssignment);

        private bool CanAddSupplierToVariant() =>
            !IsBusy &&
            IsStockItem &&
            SelectedVariantForSupplierEdit != null &&
            SupplierToAdd != null &&
            SupplierCostInput >= 0 &&
            SupplierMinimumOrderQuantityInput > 0;

        private bool CanApplySupplierToAllVariants() =>
            !IsBusy &&
            IsStockItem &&
            GeneratedVariants.Any() &&
            BulkSupplierToAssign != null &&
            BulkSupplierCostInput >= 0 &&
            BulkSupplierMinimumOrderQuantityInput > 0;
        private bool CanSave() => !IsBusy;
        private bool CanDeleteUnusedItem() => !IsBusy && IsExistingItem;
        private bool CanDeactivateItem() => !IsBusy && IsExistingItem && !_loadedItemWasDeactivated;
        private bool CanReactivateItem() => !IsBusy && IsExistingItem && _loadedItemWasDeactivated;
        private bool CanDelete() => CanDeactivateItem();

        private string BuildItemCode() => NormalizeCode(ItemCodeInput);

        private bool ValidateBeforeVariantGeneration(string itemCode)
        {
            if (SelectedItemType == null ||
                !ItemTypeCodes.IsValid(SelectedItemType.Code))
            {
                _messageBoxService.ShowWarning(
                    "Select Stock Item or Service.",
                    "Validation Error");
                return false;
            }

            if (SelectedCategory == null)
            {
                _messageBoxService.ShowWarning(
                    "Select a category.",
                    "Validation Error");
                return false;
            }

            if (SelectedUom == null)
            {
                _messageBoxService.ShowWarning(
                    "Select a Unit of Measure.",
                    "Validation Error");
                return false;
            }

            bool isAutoGenerating = !IsExistingItem && string.IsNullOrWhiteSpace(itemCode);

            if (!isAutoGenerating)
            {
                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    _messageBoxService.ShowWarning(
                        "Enter the item code.",
                        "Validation Error");
                    return false;
                }

                if (!CodeRegex.IsMatch(itemCode))
                {
                    _messageBoxService.ShowWarning(
                        "Item code may contain only letters, numbers, underscore, and hyphen.",
                        "Validation Error");
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(CurrentItem.ItemName))
            {
                _messageBoxService.ShowWarning(
                    "Enter the item name.",
                    "Validation Error");
                return false;
            }

            return true;
        }

        private bool ValidateBeforeSave(string itemCode)
        {
            if (!ValidateBeforeVariantGeneration(itemCode))
                return false;

            if (SelectedTaxCategory == null)
            {
                _messageBoxService.ShowWarning(
                    "Select Standard VAT, Zero Rated, Exempt, or Out of Scope.",
                    "Validation Error");
                return false;
            }

            if (string.Equals(
                    SelectedTaxCategory.CategoryCode,
                    TaxCategoryCodes.Standard,
                    StringComparison.Ordinal) &&
                GetCurrentStandardRate() == null)
            {
                _messageBoxService.ShowWarning(
                    "No Standard VAT rate is effective today. Correct Tax Rate Management first.",
                    "Tax Setup Required");
                return false;
            }

            if (!GeneratedVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "Generate at least one variant. Leave matrix values empty to create one Standard variant.",
                    "Validation Error");
                return false;
            }

            if (!IsExistingItem)
            {
                bool isAutoGenerating = string.IsNullOrWhiteSpace(itemCode);
                
                if (!isAutoGenerating)
                {
                    string misalignmentMessage =
                        ItemVariantIdentityPolicy.BuildMisalignmentMessage(
                            itemCode,
                            GeneratedVariants);

                    if (!string.IsNullOrWhiteSpace(misalignmentMessage))
                    {
                        _messageBoxService.ShowWarning(
                            misalignmentMessage,
                            "Generate Variants Again");
                        return false;
                    }
                }
            }

            return true;
        }

        private void HandleItemCodeInputChanged()
        {
            GenerateVariantsCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();

            if (_isLoadingItem || _isClearing || IsExistingItem)
                return;

            string currentItemCode = BuildItemCode();

            if (string.Equals(
                    NormalizeCode(currentItemCode),
                    _generatedVariantItemCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            InvalidateGeneratedVariants(
                "Item code changed. Generated variants were cleared. Generate variants again before saving.");
        }

        private void InvalidateGeneratedVariants(string reason)
        {
            if (!GeneratedVariants.Any())
                return;

            GeneratedVariants.Clear();
            _generatedVariantItemCode = string.Empty;
            SelectedVariantSuppliers.Clear();
            SelectedVariantForSupplierEdit = null;
            SelectedSupplierLinkForEdit = null;
            SelectAllVariantsForSupplierAssignment = false;
            UpdateSupplierAssignmentSelectionCount();
            StatusMessage = reason;
            NotifyCommandStates();
        }

        private void ResetTransientNewItemIdentities()
        {
            CurrentItem.Id = 0;

            foreach (ItemVariant variant in GeneratedVariants)
            {
                variant.Id = 0;
                variant.ItemParentId = 0;

                foreach (ItemPropertyMapping mapping in variant.PropertyMappings)
                    mapping.ItemVariantId = 0;

                foreach (ItemSupplier supplier in variant.ItemSuppliers)
                {
                    supplier.Id = 0;
                    supplier.ItemVariantId = 0;
                }
            }

            RaiseItemStateProperties();
            NotifyCommandStates();
        }

        private void ApplyParentDisplayNamesToAllVariants()
        {
            foreach (var variant in GeneratedVariants) ApplyParentDisplayNames(variant);
        }

        private void ApplyParentDisplayNames(ItemVariant variant)
        {
            variant.ParentItemName = NormalizeText(CurrentItem.ItemName);
            variant.ParentPrintName = NormalizeText(CurrentItem.PrintName);
        }

        private static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

        private async void ShowTemporaryNotification(string message)
        {
            NotificationMessage = message;
            await Task.Delay(4000);
            if (NotificationMessage == message)
                NotificationMessage = string.Empty;
        }
    }
}