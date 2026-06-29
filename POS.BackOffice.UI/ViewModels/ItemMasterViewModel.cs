using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

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

    public partial class ItemMasterViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly AttributeRepository _attributeRepository;
        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly SupplierRepository _supplierRepository;

        private bool _isLoadingItem = false;
        private bool _isClearing = false;

        private static readonly Random _random = new Random();

        private static readonly Regex CodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        // =========================================================
        // ZONE 1: PARENT IDENTITY & CLASSIFICATION
        // =========================================================

        [ObservableProperty]
        private ItemParent _currentItem = new();

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        [ObservableProperty]
        private string _itemPrefix = string.Empty;

        [ObservableProperty]
        private string _itemSuffix = string.Empty;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private UnitOfMeasure? _selectedUom;

        [ObservableProperty]
        private string _selectedTaxCode = string.Empty;

        // =========================================================
        // ZONE 2: VARIANT BUILDER
        // =========================================================

        [ObservableProperty]
        private AttributeGroup? _selectedPropertyKey;

        [ObservableProperty]
        private AttributeValue? _propertyValueInput;

        public ObservableCollection<MatrixPropertySelection> DynamicProperties { get; } = new();

        public ObservableCollection<MatrixPropertyGroupSelection> SelectedPropertyGroups { get; } = new();

        // =========================================================
        // ZONE 3: BULK PRICING & TRACKING DEFAULTS
        // =========================================================

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

        // Correct new flag:
        // Default ON because your cashier will sell from selected batches.
        [ObservableProperty]
        private bool _bulkHasBatchTracking = true;

        // Correct new flag:
        // Expiry is separate from batch.
        [ObservableProperty]
        private bool _bulkHasExpiryTracking = false;

        // Legacy compatibility only.
        // Old XAML/repositories used this combined "Batch / Expiry" flag.
        [ObservableProperty]
        private bool _bulkHasBatchExpiry = false;

        // Kept for future serial workflow, but the new UI should not show it yet.
        [ObservableProperty]
        private bool _bulkIsSerialized = false;

        // =========================================================
        // ZONE 4: ITEM DATABASE GRID
        // =========================================================

        public ObservableCollection<ItemMasterSummaryDto> Items { get; } = new();

        [ObservableProperty]
        private string _masterSearchText = string.Empty;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedDatabaseItem;

        // =========================================================
        // ZONE 5: GENERATED VARIANTS
        // =========================================================

        public ObservableCollection<ItemVariant> GeneratedVariants { get; } = new();

        [ObservableProperty]
        private ItemVariant? _selectedVariantForSupplierEdit;

        // =========================================================
        // ZONE 6: SINGLE VARIANT SUPPLIER MANAGEMENT
        // =========================================================

        public ObservableCollection<Supplier> AvailableSuppliers { get; } = new();

        public ObservableCollection<ItemSupplier> SelectedVariantSuppliers { get; } = new();

        [ObservableProperty]
        private Supplier? _supplierToAdd;

        [ObservableProperty]
        private string _supplierItemCodeInput = string.Empty;

        [ObservableProperty]
        private decimal _supplierCostInput = 0m;

        [ObservableProperty]
        private bool _supplierIsPrimaryInput = false;

        [ObservableProperty]
        private int _supplierMinimumOrderQuantityInput = 1;

        // =========================================================
        // ZONE 7: BULK SUPPLIER ASSIGNMENT
        // =========================================================

        [ObservableProperty]
        private Supplier? _bulkSupplierToAssign;

        [ObservableProperty]
        private string _bulkSupplierItemCodePrefix = string.Empty;

        [ObservableProperty]
        private decimal _bulkSupplierCostInput = 0m;

        [ObservableProperty]
        private int _bulkSupplierMinimumOrderQuantityInput = 1;

        [ObservableProperty]
        private bool _bulkSupplierIsPrimaryInput = true;

        // =========================================================
        // LOOKUPS
        // =========================================================

        public ObservableCollection<Category> Categories { get; } = new();

        public ObservableCollection<SubCategory> SubCategories { get; } = new();

        public ObservableCollection<AttributeGroup> PropertyKeys { get; } = new();

        public ObservableCollection<AttributeValue> PropertyValues { get; } = new();

        public ObservableCollection<UnitOfMeasure> Uoms { get; } = new();

        public ObservableCollection<string> TaxCodes { get; } = new(new[]
        {
            "TAX-FREE",
            "VAT-18",
            "VAT-5"
        });

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ItemMasterViewModel(
            ItemMasterRepository itemMasterRepository,
            CategoryRepository categoryRepository,
            SubCategoryRepository subCategoryRepository,
            AttributeRepository attributeRepository,
            UnitOfMeasureRepository uomRepository,
            SupplierRepository supplierRepository)
        {
            _itemMasterRepository = itemMasterRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _attributeRepository = attributeRepository;
            _uomRepository = uomRepository;
            _supplierRepository = supplierRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                await LoadLookupsAsync();
                await LoadMasterGridInternalAsync();

                StatusMessage = "Item Master page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize Item Master.";

                MessageBox.Show(
                    $"Failed to initialize Item Master:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            var categories = await _categoryRepository.GetAllAsync();

            foreach (var category in categories.Where(c => !c.IsDeactivated).OrderBy(c => c.CategoryName))
                Categories.Add(category);

            var uoms = await _uomRepository.GetActiveAsync();

            foreach (var uom in uoms)
                Uoms.Add(uom);

            var suppliers = await _supplierRepository.GetAllAsync();

            foreach (var supplier in suppliers.Where(s => !s.IsDeactivated).OrderBy(s => s.SupplierName))
                AvailableSuppliers.Add(supplier);

            SelectedTaxCode = TaxCodes.FirstOrDefault() ?? string.Empty;
            SelectedUom = Uoms.FirstOrDefault();
        }

        // =========================================================
        // CATEGORY / SUBCATEGORY / PROPERTY LOADING
        // =========================================================

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (_isClearing)
                return;

            if (value == null)
            {
                SubCategories.Clear();
                PropertyKeys.Clear();
                PropertyValues.Clear();
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
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;

                await LoadSubCategoriesAsync(categoryId);
                await LoadPropertyKeysForCategoryAsync(categoryId);

                StatusMessage = "Category changed. Select a sub-category and build variants again.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load category details.";

                MessageBox.Show(
                    $"Failed to load category details:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            if (value != null && SelectedCategory != null && !IsCodeReadOnly)
            {
                CurrentItem.SubCategoryId = value.Id;
                ItemPrefix = BuildItemCodePrefix(SelectedCategory, value);
            }
        }

        private static string BuildItemCodePrefix(Category category, SubCategory subCategory)
        {
            string categoryCode = (category.CategoryCode ?? string.Empty).Trim().ToUpperInvariant();
            string subCategoryCode = (subCategory.SubCategoryCode ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(categoryCode) || string.IsNullOrWhiteSpace(subCategoryCode))
                return string.Empty;

            string categoryPrefix = categoryCode + "-";

            if (subCategoryCode.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return subCategoryCode.EndsWith("-")
                    ? subCategoryCode
                    : subCategoryCode + "-";
            }

            return $"{categoryCode}-{subCategoryCode}-";
        }

        private async Task LoadSubCategoriesAsync(int categoryId)
        {
            SubCategories.Clear();

            var subCategories = await _subCategoryRepository.GetAllAsync();

            foreach (var subCategory in subCategories
                         .Where(s => !s.IsDeactivated && s.CategoryId == categoryId)
                         .OrderBy(s => s.SubCategoryName))
            {
                SubCategories.Add(subCategory);
            }
        }

        private async Task LoadPropertyKeysForCategoryAsync(int categoryId)
        {
            PropertyKeys.Clear();
            PropertyValues.Clear();

            var groups = await _attributeRepository.GetAttributeGroupsForCategoryAsync(categoryId);

            foreach (var group in groups)
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

                foreach (var value in values.Where(v => !v.IsDeactivated))
                    PropertyValues.Add(value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load property values:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        partial void OnPropertyValueInputChanged(AttributeValue? value)
        {
            AddPropertyCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // DATABASE ITEM SELECTION / LOADING
        // =========================================================

        partial void OnSelectedDatabaseItemChanged(ItemMasterSummaryDto? value)
        {
            if (_isClearing)
                return;

            if (value != null)
                _ = LoadFullItemDetailsAsync(value.ParentId);
            else
                IsCodeReadOnly = false;
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

                CurrentItem = fullItem;

                IsCodeReadOnly = true;
                ItemPrefix = fullItem.ItemCode;
                ItemSuffix = string.Empty;

                SelectedCategory = Categories.FirstOrDefault(c => c.Id == fullItem.CategoryId);

                await LoadSubCategoriesAsync(fullItem.CategoryId);

                SelectedSubCategory = fullItem.SubCategoryId.HasValue
                    ? SubCategories.FirstOrDefault(s => s.Id == fullItem.SubCategoryId.Value)
                    : null;

                await LoadPropertyKeysForCategoryAsync(fullItem.CategoryId);

                SelectedUom =
                    Uoms.FirstOrDefault(u => u.Id == fullItem.UnitOfMeasureId) ??
                    Uoms.FirstOrDefault(u => string.Equals(u.UomCode, fullItem.BaseUom, StringComparison.OrdinalIgnoreCase));

                SelectedTaxCode = fullItem.TaxCode;

                BulkIsScaleItem = fullItem.IsScaleItem;

                // New separated flags.
                BulkHasBatchTracking = fullItem.HasBatchTracking;

                // Legacy fallback:
                // old records may only have HasBatchExpiry.
                BulkHasExpiryTracking = fullItem.HasExpiryTracking || fullItem.HasBatchExpiry;
                BulkHasBatchExpiry = BulkHasExpiryTracking;

                BulkIsSerialized = fullItem.IsSerialized;

                GeneratedVariants.Clear();
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;

                foreach (var variant in fullItem.Variants
                             .Where(v => !v.IsDeactivated)
                             .OrderBy(v => v.VariantDescription)
                             .ThenBy(v => v.SkuCode))
                {
                    variant.PropertyMappings ??= new List<ItemPropertyMapping>();
                    variant.ItemSuppliers ??= new List<ItemSupplier>();

                    GeneratedVariants.Add(variant);
                }

                RebuildBuilderSelectionFromVariants();

                StatusMessage = $"Loaded item: {fullItem.ItemCode}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item.";

                MessageBox.Show(
                    $"Failed to load item:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                .GroupBy(m => new
                {
                    GroupId = m.AttributeGroupId,
                    ValueId = m.AttributeValueId
                })
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

        // =========================================================
        // MATRIX PROPERTY SELECTION
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanAddProperty))]
        private void AddProperty()
        {
            if (SelectedPropertyKey == null || PropertyValueInput == null)
                return;

            bool duplicateExists = DynamicProperties.Any(p =>
                p.Group.Id == SelectedPropertyKey.Id &&
                p.Value.Id == PropertyValueInput.Id);

            if (duplicateExists)
            {
                StatusMessage = "This property value is already selected.";
                return;
            }

            var selection = new MatrixPropertySelection
            {
                Group = SelectedPropertyKey,
                Value = PropertyValueInput
            };

            AddSelectionToCollections(selection);

            PropertyValueInput = null;

            StatusMessage = "Property value added to matrix builder.";
        }

        private void AddSelectionToCollections(MatrixPropertySelection selection)
        {
            DynamicProperties.Add(selection);

            var groupSelection = SelectedPropertyGroups
                .FirstOrDefault(g => g.Group.Id == selection.Group.Id);

            if (groupSelection == null)
            {
                groupSelection = new MatrixPropertyGroupSelection
                {
                    Group = selection.Group
                };

                SelectedPropertyGroups.Add(groupSelection);
            }

            groupSelection.Values.Add(selection);
        }

        [RelayCommand]
        private void RemoveProperty(MatrixPropertySelection? selection)
        {
            if (selection == null)
                return;

            DynamicProperties.Remove(selection);

            var groupSelection = SelectedPropertyGroups
                .FirstOrDefault(g => g.Group.Id == selection.Group.Id);

            if (groupSelection != null)
            {
                var valueToRemove = groupSelection.Values
                    .FirstOrDefault(v => v.Value.Id == selection.Value.Id);

                if (valueToRemove != null)
                    groupSelection.Values.Remove(valueToRemove);

                if (!groupSelection.Values.Any())
                    SelectedPropertyGroups.Remove(groupSelection);
            }

            StatusMessage = "Property value removed from matrix builder.";
        }

        private void ClearVariantBuilder()
        {
            DynamicProperties.Clear();
            SelectedPropertyGroups.Clear();
            PropertyValues.Clear();
            SelectedPropertyKey = null;
            PropertyValueInput = null;
        }

        // =========================================================
        // VARIANT GENERATION
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanGenerateVariants))]
        private async Task GenerateVariantsAsync()
        {
            string itemCode = BuildItemCode();

            if (!ValidateBeforeVariantGeneration(itemCode))
                return;

            var existingSupplierLinks = CaptureSupplierLinksBySku();

            GeneratedVariants.Clear();
            SelectedVariantSuppliers.Clear();
            SelectedVariantForSupplierEdit = null;

            var usedBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!DynamicProperties.Any())
            {
                var standardVariant = new ItemVariant
                {
                    SkuCode = itemCode,
                    VariantDescription = "Standard",
                    Barcode = await GenerateInternalBarcodeAsync(usedBarcodes),
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>(),
                    PropertyMappings = new List<ItemPropertyMapping>()
                };

                RestoreSupplierLinksIfAvailable(standardVariant, existingSupplierLinks);
                GeneratedVariants.Add(standardVariant);

                StatusMessage = "1 standard variant generated.";
                NotifyCommandStates();
                return;
            }

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
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>(),
                    PropertyMappings = new List<ItemPropertyMapping>()
                };

                foreach (var selection in combo)
                {
                    variant.PropertyMappings.Add(new ItemPropertyMapping
                    {
                        AttributeGroupId = selection.Group.Id,
                        AttributeValueId = selection.Value.Id
                    });
                }

                RestoreSupplierLinksIfAvailable(variant, existingSupplierLinks);
                GeneratedVariants.Add(variant);
            }

            StatusMessage = $"{GeneratedVariants.Count} variant(s) generated.";
            NotifyCommandStates();
        }

        private Dictionary<string, List<ItemSupplier>> CaptureSupplierLinksBySku()
        {
            var result = new Dictionary<string, List<ItemSupplier>>(StringComparer.OrdinalIgnoreCase);

            foreach (var variant in GeneratedVariants)
            {
                if (string.IsNullOrWhiteSpace(variant.SkuCode))
                    continue;

                result[variant.SkuCode] = variant.ItemSuppliers?
                    .Select(s => new ItemSupplier
                    {
                        SupplierId = s.SupplierId,
                        Supplier = s.Supplier,
                        SupplierItemCode = s.SupplierItemCode,
                        LastCostPrice = s.LastCostPrice,
                        IsPrimary = s.IsPrimary,
                        MinimumOrderQuantity = s.MinimumOrderQuantity <= 0 ? 1 : s.MinimumOrderQuantity
                    })
                    .ToList() ?? new List<ItemSupplier>();
            }

            return result;
        }

        private static void RestoreSupplierLinksIfAvailable(
            ItemVariant variant,
            Dictionary<string, List<ItemSupplier>> supplierLinksBySku)
        {
            if (!supplierLinksBySku.TryGetValue(variant.SkuCode, out var suppliers))
                return;

            foreach (var supplier in suppliers)
                variant.ItemSuppliers.Add(supplier);
        }

        private static List<List<MatrixPropertySelection>> GenerateCombinations(
            List<List<MatrixPropertySelection>> groups,
            int depth = 0)
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

        private string BuildSkuForCombination(
            string itemCode,
            List<MatrixPropertySelection> combo,
            HashSet<string> usedSkus)
        {
            string suffix = string.Join("-",
                combo.Select(c =>
                    $"{SanitizeCodeSegment(c.Value.ValueName)}{c.Value.Id}"));

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

                if (usedBarcodes.Contains(barcode))
                    continue;

                bool isUnique = await _itemMasterRepository.IsBarcodeUniqueAsync(barcode);

                if (!isUnique)
                    continue;

                usedBarcodes.Add(barcode);
                return barcode;
            }

            throw new InvalidOperationException("Failed to generate a unique internal barcode. Try again.");
        }

        private static string GenerateRandomDigits(int length)
        {
            char[] digits = new char[length];

            for (int i = 0; i < length; i++)
                digits[i] = (char)('0' + _random.Next(0, 10));

            return new string(digits);
        }

        private static string SanitizeCodeSegment(string value)
        {
            string clean = new string((value ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

            if (string.IsNullOrWhiteSpace(clean))
                clean = "VAL";

            return clean.Length <= 4 ? clean : clean.Substring(0, 4);
        }

        // =========================================================
        // BULK PRICE CALCULATION
        // =========================================================

        partial void OnBulkCostChanged(decimal value)
        {
            CalculateBulkPricesFromMarkup();

            if (BulkSupplierCostInput <= 0)
                BulkSupplierCostInput = value;

            if (SupplierCostInput <= 0)
                SupplierCostInput = value;
        }

        partial void OnBulkRetailMarkupPercentChanged(decimal value)
        {
            CalculateBulkPricesFromMarkup();
        }

        partial void OnBulkWholesaleMarkupPercentChanged(decimal value)
        {
            CalculateBulkPricesFromMarkup();
        }

        partial void OnBulkHasExpiryTrackingChanged(bool value)
        {
            // Legacy compatibility: old code still reads HasBatchExpiry.
            BulkHasBatchExpiry = value;
        }

        partial void OnBulkHasBatchExpiryChanged(bool value)
        {
            if (BulkHasExpiryTracking != value)
                BulkHasExpiryTracking = value;
        }

        private void CalculateBulkPricesFromMarkup()
        {
            if (BulkCost < 0)
                return;

            BulkRetailPrice = Math.Round(BulkCost + (BulkCost * (BulkRetailMarkupPercent / 100m)), 2);
            BulkWholesalePrice = Math.Round(BulkCost + (BulkCost * (BulkWholesaleMarkupPercent / 100m)), 2);
        }

        [RelayCommand]
        private void ApplyBulkDefaults()
        {
            if (!GeneratedVariants.Any())
            {
                MessageBox.Show(
                    "Generate variants first before applying bulk defaults.",
                    "No Variants",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var variants = GeneratedVariants.ToList();

            foreach (var variant in variants)
            {
                variant.CostPrice = BulkCost;
                variant.RetailPrice = BulkRetailPrice;
                variant.WholesalePrice = BulkWholesalePrice;
                variant.MinimumPrice = BulkMinimumPrice;
                variant.MaximumPrice = BulkMaximumPrice;
                variant.ReorderLevel = BulkReorderLevel;
            }

            RefreshGeneratedVariantGrid(variants);

            StatusMessage = "Bulk defaults applied to all variants.";
        }

        private void RefreshGeneratedVariantGrid(List<ItemVariant> variants)
        {
            var selectedSku = SelectedVariantForSupplierEdit?.SkuCode;

            GeneratedVariants.Clear();

            foreach (var variant in variants)
                GeneratedVariants.Add(variant);

            if (!string.IsNullOrWhiteSpace(selectedSku))
            {
                SelectedVariantForSupplierEdit = GeneratedVariants
                    .FirstOrDefault(v => string.Equals(v.SkuCode, selectedSku, StringComparison.OrdinalIgnoreCase));
            }
        }

        // =========================================================
        // SINGLE VARIANT SUPPLIER MANAGEMENT
        // =========================================================

        partial void OnSelectedVariantForSupplierEditChanged(ItemVariant? value)
        {
            SelectedVariantSuppliers.Clear();

            if (value == null)
            {
                SupplierCostInput = 0m;
                SupplierIsPrimaryInput = false;
                SupplierMinimumOrderQuantityInput = 1;
                AddSupplierToVariantCommand.NotifyCanExecuteChanged();
                return;
            }

            value.ItemSuppliers ??= new List<ItemSupplier>();

            foreach (var supplier in value.ItemSuppliers)
                SelectedVariantSuppliers.Add(supplier);

            SupplierCostInput = value.CostPrice;
            SupplierIsPrimaryInput = !SelectedVariantSuppliers.Any();
            SupplierMinimumOrderQuantityInput = 1;

            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierToAddChanged(Supplier? value)
        {
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierCostInputChanged(decimal value)
        {
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSupplierMinimumOrderQuantityInputChanged(int value)
        {
            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanAddSupplierToVariant))]
        private void AddSupplierToVariant()
        {
            if (SelectedVariantForSupplierEdit == null)
            {
                MessageBox.Show(
                    "Please select a variant first.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SupplierToAdd == null)
            {
                MessageBox.Show(
                    "Please select a supplier.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SupplierCostInput < 0)
            {
                MessageBox.Show(
                    "Supplier cost cannot be negative.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SupplierMinimumOrderQuantityInput <= 0)
            {
                MessageBox.Show(
                    "Minimum order quantity must be greater than zero.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedVariantForSupplierEdit.ItemSuppliers ??= new List<ItemSupplier>();

            if (SelectedVariantForSupplierEdit.ItemSuppliers.Any(s => s.SupplierId == SupplierToAdd.Id))
            {
                MessageBox.Show(
                    "This supplier is already attached to this variant.",
                    "Duplicate Supplier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            bool makePrimary = SupplierIsPrimaryInput || !SelectedVariantForSupplierEdit.ItemSuppliers.Any();

            if (makePrimary)
            {
                foreach (var existing in SelectedVariantForSupplierEdit.ItemSuppliers)
                    existing.IsPrimary = false;
            }

            var newSupplierLink = new ItemSupplier
            {
                SupplierId = SupplierToAdd.Id,
                Supplier = SupplierToAdd,
                ItemVariantId = SelectedVariantForSupplierEdit.Id,
                SupplierItemCode = NormalizeText(SupplierItemCodeInput),
                LastCostPrice = SupplierCostInput,
                IsPrimary = makePrimary,
                MinimumOrderQuantity = SupplierMinimumOrderQuantityInput
            };

            SelectedVariantForSupplierEdit.ItemSuppliers.Add(newSupplierLink);
            SelectedVariantSuppliers.Add(newSupplierLink);

            SupplierToAdd = null;
            SupplierItemCodeInput = string.Empty;
            SupplierCostInput = SelectedVariantForSupplierEdit.CostPrice;
            SupplierIsPrimaryInput = false;
            SupplierMinimumOrderQuantityInput = 1;

            StatusMessage = "Supplier added to selected variant.";
        }

        [RelayCommand]
        private void RemoveSupplierFromVariant(ItemSupplier? itemSupplier)
        {
            if (itemSupplier == null || SelectedVariantForSupplierEdit == null)
                return;

            SelectedVariantSuppliers.Remove(itemSupplier);
            SelectedVariantForSupplierEdit.ItemSuppliers.Remove(itemSupplier);

            if (itemSupplier.IsPrimary && SelectedVariantForSupplierEdit.ItemSuppliers.Any())
            {
                SelectedVariantForSupplierEdit.ItemSuppliers.First().IsPrimary = true;
                RebuildSelectedVariantSuppliers();
            }

            StatusMessage = "Supplier removed from selected variant.";
        }

        private void RebuildSelectedVariantSuppliers()
        {
            SelectedVariantSuppliers.Clear();

            if (SelectedVariantForSupplierEdit?.ItemSuppliers == null)
                return;

            foreach (var supplier in SelectedVariantForSupplierEdit.ItemSuppliers)
                SelectedVariantSuppliers.Add(supplier);
        }

        // =========================================================
        // BULK SUPPLIER ASSIGNMENT
        // =========================================================

        partial void OnBulkSupplierToAssignChanged(Supplier? value)
        {
            ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkSupplierCostInputChanged(decimal value)
        {
            ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkSupplierMinimumOrderQuantityInputChanged(int value)
        {
            ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanApplySupplierToAllVariants))]
        private void ApplySupplierToAllVariants()
        {
            if (!GeneratedVariants.Any())
            {
                MessageBox.Show(
                    "Generate variants first before assigning suppliers.",
                    "No Variants",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (BulkSupplierToAssign == null)
            {
                MessageBox.Show(
                    "Please select a supplier.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (BulkSupplierCostInput < 0)
            {
                MessageBox.Show(
                    "Supplier cost cannot be negative.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (BulkSupplierMinimumOrderQuantityInput <= 0)
            {
                MessageBox.Show(
                    "Minimum order quantity must be greater than zero.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var variant in GeneratedVariants)
            {
                variant.ItemSuppliers ??= new List<ItemSupplier>();

                if (BulkSupplierIsPrimaryInput)
                {
                    foreach (var existingSupplier in variant.ItemSuppliers)
                        existingSupplier.IsPrimary = false;
                }

                string vendorCode = BuildBulkVendorItemCode(
                    BulkSupplierItemCodePrefix,
                    variant.SkuCode);

                var existing = variant.ItemSuppliers
                    .FirstOrDefault(s => s.SupplierId == BulkSupplierToAssign.Id);

                if (existing == null)
                {
                    var link = new ItemSupplier
                    {
                        SupplierId = BulkSupplierToAssign.Id,
                        Supplier = BulkSupplierToAssign,
                        ItemVariantId = variant.Id,
                        SupplierItemCode = vendorCode,
                        LastCostPrice = BulkSupplierCostInput,
                        MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput,
                        IsPrimary = BulkSupplierIsPrimaryInput || !variant.ItemSuppliers.Any()
                    };

                    variant.ItemSuppliers.Add(link);
                    addedCount++;
                }
                else
                {
                    existing.Supplier = BulkSupplierToAssign;
                    existing.SupplierItemCode = vendorCode;
                    existing.LastCostPrice = BulkSupplierCostInput;
                    existing.MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput;
                    existing.IsPrimary = BulkSupplierIsPrimaryInput || existing.IsPrimary;
                    updatedCount++;
                }

                if (!variant.ItemSuppliers.Any(s => s.IsPrimary))
                {
                    var first = variant.ItemSuppliers.FirstOrDefault();
                    if (first != null)
                        first.IsPrimary = true;
                }
            }

            if (SelectedVariantForSupplierEdit != null)
                RebuildSelectedVariantSuppliers();

            StatusMessage = $"Supplier bulk assignment completed. Added: {addedCount}, Updated: {updatedCount}.";
        }

        private static string BuildBulkVendorItemCode(string prefix, string skuCode)
        {
            string cleanPrefix = NormalizeText(prefix);
            string cleanSku = NormalizeText(skuCode);

            if (string.IsNullOrWhiteSpace(cleanPrefix))
                return string.Empty;

            string value = $"{cleanPrefix}-{cleanSku}";

            return value.Length <= 100
                ? value
                : value.Substring(0, 100);
        }

        // =========================================================
        // SAVE / SEARCH / DELETE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string itemCode = BuildItemCode();

            if (!ValidateBeforeSave(itemCode))
                return;

            IsBusy = true;

            try
            {
                bool itemCodeUnique = await _itemMasterRepository.IsItemCodeUniqueAsync(
                    itemCode,
                    CurrentItem.Id);

                if (!itemCodeUnique)
                {
                    MessageBox.Show(
                        $"Item code '{itemCode}' already exists.",
                        "Duplicate Item Code",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (CurrentItem.Id == 0)
                {
                    foreach (var variant in GeneratedVariants)
                    {
                        bool skuUnique = await _itemMasterRepository.IsSkuCodeUniqueAsync(variant.SkuCode);

                        if (!skuUnique)
                        {
                            MessageBox.Show(
                                $"SKU '{variant.SkuCode}' already exists.",
                                "Duplicate SKU",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }

                        bool barcodeUnique = await _itemMasterRepository.IsBarcodeUniqueAsync(variant.Barcode);

                        if (!barcodeUnique)
                        {
                            MessageBox.Show(
                                $"Barcode '{variant.Barcode}' already exists.",
                                "Duplicate Barcode",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                CurrentItem.ItemCode = itemCode;
                CurrentItem.CategoryId = SelectedCategory!.Id;
                CurrentItem.SubCategoryId = SelectedSubCategory?.Id;
                CurrentItem.UnitOfMeasureId = SelectedUom!.Id;
                CurrentItem.BaseUom = SelectedUom.UomCode;
                CurrentItem.TaxCode = SelectedTaxCode;

                CurrentItem.IsScaleItem = BulkIsScaleItem;

                // New separated tracking flags.
                CurrentItem.HasBatchTracking = BulkHasBatchTracking;
                CurrentItem.HasExpiryTracking = BulkHasExpiryTracking;

                // Legacy compatibility.
                CurrentItem.HasBatchExpiry = BulkHasExpiryTracking;

                CurrentItem.IsSerialized = BulkIsSerialized;

                CurrentItem.Category = null!;
                CurrentItem.SubCategory = null;
                CurrentItem.UnitOfMeasure = null!;
                CurrentItem.Variants = new List<ItemVariant>();

                foreach (var variant in GeneratedVariants)
                {
                    variant.ItemParent = null!;

                    foreach (var mapping in variant.PropertyMappings)
                    {
                        mapping.ItemVariant = null!;
                        mapping.AttributeGroup = null!;
                        mapping.AttributeValue = null!;
                    }

                    foreach (var supplier in variant.ItemSuppliers)
                    {
                        supplier.ItemVariant = null!;
                        supplier.Supplier = null!;
                    }
                }

                var mappingsList = GeneratedVariants
                    .SelectMany(v => v.PropertyMappings)
                    .ToList();

                await _itemMasterRepository.SaveFullMatrixAsync(
                    CurrentItem,
                    GeneratedVariants.ToList(),
                    mappingsList);

                await LoadMasterGridInternalAsync();
                Clear();

                StatusMessage = "Item saved successfully.";

                MessageBox.Show(
                    "Item saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Save Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"Failed to save item:\n\n{message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
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
                StatusMessage = "Failed to load item database.";

                MessageBox.Show(
                    $"Failed to load item database:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadMasterGridInternalAsync()
        {
            Items.Clear();

            var data = await _itemMasterRepository.GetSummariesAsync(MasterSearchText);

            foreach (var item in data)
                Items.Add(item);

            StatusMessage = $"{Items.Count} item record(s) loaded.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchItemsAsync()
        {
            await LoadMasterGridAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshDatabaseAsync()
        {
            MasterSearchText = string.Empty;
            await LoadMasterGridAsync();
        }

        [RelayCommand]
        private void Clear()
        {
            _isClearing = true;

            CurrentItem = new ItemParent
            {
                HasBatchTracking = true,
                HasExpiryTracking = false,
                HasBatchExpiry = false,
                AllowCashierDiscount = true
            };

            ItemPrefix = string.Empty;
            ItemSuffix = string.Empty;
            IsCodeReadOnly = false;

            SelectedCategory = null;
            SelectedSubCategory = null;
            SubCategories.Clear();

            SelectedUom = Uoms.FirstOrDefault();
            SelectedTaxCode = TaxCodes.FirstOrDefault() ?? string.Empty;

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
            SelectedVariantSuppliers.Clear();
            SelectedVariantForSupplierEdit = null;

            SupplierToAdd = null;
            SupplierItemCodeInput = string.Empty;
            SupplierCostInput = 0m;
            SupplierIsPrimaryInput = false;
            SupplierMinimumOrderQuantityInput = 1;

            BulkSupplierToAssign = null;
            BulkSupplierItemCodePrefix = string.Empty;
            BulkSupplierCostInput = 0m;
            BulkSupplierMinimumOrderQuantityInput = 1;
            BulkSupplierIsPrimaryInput = true;

            SelectedDatabaseItem = null;

            _isClearing = false;

            StatusMessage = "Ready for new item.";

            NotifyCommandStates();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedDatabaseItem == null)
                return;

            var result = MessageBox.Show(
                $"Deactivate item '{SelectedDatabaseItem.ItemName}' and all its variants?\n\n" +
                "This keeps sales, GRN, stock, and transaction history safe.",
                "Confirm Deactivation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _itemMasterRepository.DeleteMatrixAsync(SelectedDatabaseItem.ParentId);

                await LoadMasterGridInternalAsync();
                Clear();

                StatusMessage = "Item deactivated successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete/deactivate failed.";

                MessageBox.Show(
                    $"Failed to deactivate item:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // PROPERTY CHANGE HANDLERS
        // =========================================================

        partial void OnMasterSearchTextChanged(string value)
        {
            StatusMessage = "Type search text and click SEARCH.";
        }

        partial void OnItemSuffixChanged(string value)
        {
            GenerateVariantsCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedUomChanged(UnitOfMeasure? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedTaxCodeChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyCommandStates();
        }

        private void NotifyCommandStates()
        {
            LoadMasterGridCommand.NotifyCanExecuteChanged();
            SearchItemsCommand.NotifyCanExecuteChanged();
            RefreshDatabaseCommand.NotifyCanExecuteChanged();

            AddPropertyCommand.NotifyCanExecuteChanged();
            GenerateVariantsCommand.NotifyCanExecuteChanged();

            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
            ApplySupplierToAllVariantsCommand.NotifyCanExecuteChanged();

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanAddProperty()
        {
            return !IsBusy &&
                   SelectedPropertyKey != null &&
                   PropertyValueInput != null;
        }

        private bool CanGenerateVariants()
        {
            return !IsBusy;
        }

        private bool CanAddSupplierToVariant()
        {
            return !IsBusy &&
                   SelectedVariantForSupplierEdit != null &&
                   SupplierToAdd != null &&
                   SupplierCostInput >= 0 &&
                   SupplierMinimumOrderQuantityInput > 0;
        }

        private bool CanApplySupplierToAllVariants()
        {
            return !IsBusy &&
                   GeneratedVariants.Any() &&
                   BulkSupplierToAssign != null &&
                   BulkSupplierCostInput >= 0 &&
                   BulkSupplierMinimumOrderQuantityInput > 0;
        }

        private bool CanSave()
        {
            return !IsBusy;
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedDatabaseItem != null;
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private string BuildItemCode()
        {
            string code = IsCodeReadOnly && string.IsNullOrWhiteSpace(ItemSuffix)
                ? ItemPrefix
                : $"{ItemPrefix}{ItemSuffix}";

            return NormalizeCode(code);
        }

        private bool ValidateBeforeVariantGeneration(string itemCode)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show(
                    "Please select a category.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (SelectedSubCategory == null)
            {
                MessageBox.Show(
                    "Please select a sub-category.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!ValidateItemCode(itemCode))
                return false;

            if (BulkCost < 0 ||
                BulkRetailPrice < 0 ||
                BulkWholesalePrice < 0 ||
                BulkMinimumPrice < 0 ||
                BulkMaximumPrice < 0)
            {
                MessageBox.Show(
                    "Price values cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (BulkMaximumPrice > 0 && BulkMinimumPrice > BulkMaximumPrice)
            {
                MessageBox.Show(
                    "Minimum price cannot be greater than maximum price.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (BulkReorderLevel < 0)
            {
                MessageBox.Show(
                    "Reorder level cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!BulkHasBatchTracking && BulkHasExpiryTracking)
            {
                MessageBox.Show(
                    "Expiry tracking requires batch tracking.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool ValidateBeforeSave(string itemCode)
        {
            if (!ValidateBeforeVariantGeneration(itemCode))
                return false;

            if (string.IsNullOrWhiteSpace(CurrentItem.ItemName))
            {
                MessageBox.Show(
                    "Item name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (CurrentItem.ItemName.Trim().Length > 150)
            {
                MessageBox.Show(
                    "Item name cannot be longer than 150 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CurrentItem.PrintName) &&
                CurrentItem.PrintName.Trim().Length > 50)
            {
                MessageBox.Show(
                    "Print name cannot be longer than 50 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (SelectedUom == null)
            {
                MessageBox.Show(
                    "Please select a Unit of Measure.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedTaxCode))
            {
                MessageBox.Show(
                    "Please select a tax code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!GeneratedVariants.Any())
            {
                MessageBox.Show(
                    "Generate variants before saving.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            foreach (var variant in GeneratedVariants)
            {
                variant.ItemSuppliers ??= new List<ItemSupplier>();

                if (variant.ItemSuppliers.Count(s => s.IsPrimary) > 1)
                {
                    MessageBox.Show(
                        $"Variant '{variant.SkuCode}' has more than one primary supplier.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                var duplicateSupplier = variant.ItemSuppliers
                    .GroupBy(s => s.SupplierId)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicateSupplier != null)
                {
                    MessageBox.Show(
                        $"Variant '{variant.SkuCode}' has the same supplier assigned more than once.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                foreach (var supplier in variant.ItemSuppliers)
                {
                    if (supplier.MinimumOrderQuantity <= 0)
                    {
                        MessageBox.Show(
                            $"Variant '{variant.SkuCode}' has invalid supplier MOQ.",
                            "Validation Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }

                    if (supplier.LastCostPrice < 0)
                    {
                        MessageBox.Show(
                            $"Variant '{variant.SkuCode}' has invalid supplier cost.",
                            "Validation Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateItemCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                MessageBox.Show(
                    "Item code is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (itemCode.Length > 50)
            {
                MessageBox.Show(
                    "Item code cannot be longer than 50 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (itemCode.EndsWith("-"))
            {
                MessageBox.Show(
                    "Item code is incomplete. Please enter the item suffix after the category/sub-category prefix.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!CodeRegex.IsMatch(itemCode))
            {
                MessageBox.Show(
                    "Item code can only contain letters, numbers, dash, and underscore.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static string NormalizeCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}