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

    public partial class ItemMasterViewModel : ViewModelBase
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly AttributeRepository _attributeRepository;
        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isLoadingItem;
        private bool _isClearing;
        private bool _isUpdatingSupplierSelection;

        private static readonly Random _random = new();

        private static readonly Regex CodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [ObservableProperty]
        private ItemParent _currentItem = new()
        {
            HasBatchTracking = true,
            HasExpiryTracking = false,
            HasBatchExpiry = false,
            AllowCashierDiscount = true
        };

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

        [ObservableProperty]
        private AttributeGroup? _selectedPropertyKey;

        [ObservableProperty]
        private AttributeValue? _propertyValueInput;

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

        public ObservableCollection<ItemMasterSummaryDto> Items { get; } = new();

        [ObservableProperty]
        private string _masterSearchText = string.Empty;

        [ObservableProperty]
        private bool _includeDeactivatedItems = false;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedDatabaseItem;

        public ObservableCollection<ItemVariant> GeneratedVariants { get; } = new();

        [ObservableProperty]
        private ItemVariant? _selectedVariantForSupplierEdit;

        // =========================================================
        // VARIANT SUPPLIER ASSIGNMENT SELECTION
        // =========================================================

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

        public ObservableCollection<string> TaxCodes { get; } = new(new[]
        {
            "TAX-FREE",
            "VAT-18",
            "VAT-5"
        });

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
            SupplierRepository supplierRepository,
            IMessageBoxService messageBoxService)
        {
            _itemMasterRepository = itemMasterRepository ?? throw new ArgumentNullException(nameof(itemMasterRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _subCategoryRepository = subCategoryRepository ?? throw new ArgumentNullException(nameof(subCategoryRepository));
            _attributeRepository = attributeRepository ?? throw new ArgumentNullException(nameof(attributeRepository));
            _uomRepository = uomRepository ?? throw new ArgumentNullException(nameof(uomRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanInitialize))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
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

                _messageBoxService.ShowError(
                    $"Failed to initialize Item Master:\n\n{ex.Message}",
                    "Database Error");
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

            var suppliers = await _supplierRepository.GetActiveAsync();

            foreach (var supplier in suppliers.OrderBy(s => s.SupplierCode).ThenBy(s => s.SupplierName))
                AvailableSuppliers.Add(supplier);

            SelectedTaxCode = TaxCodes.FirstOrDefault() ?? string.Empty;
            SelectedUom = Uoms.FirstOrDefault();
        }

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
                UpdateSupplierAssignmentSelectionCount();

                await LoadSubCategoriesAsync(categoryId);
                await LoadPropertyKeysForCategoryAsync(categoryId);

                StatusMessage = "Category changed. Select a sub-category and build variants again.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load category details.";

                _messageBoxService.ShowError(
                    $"Failed to load category details:\n\n{ex.Message}",
                    "Database Error");
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
                _messageBoxService.ShowError(
                    $"Failed to load property values:\n\n{ex.Message}",
                    "Database Error");
            }
        }

        partial void OnPropertyValueInputChanged(AttributeValue? value)
        {
            AddPropertyCommand.NotifyCanExecuteChanged();
        }

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
                BulkHasBatchTracking = fullItem.HasBatchTracking;
                BulkHasExpiryTracking = fullItem.HasExpiryTracking || fullItem.HasBatchExpiry;
                BulkHasBatchExpiry = BulkHasExpiryTracking;
                BulkIsSerialized = fullItem.IsSerialized;

                GeneratedVariants.Clear();
                SelectedVariantSuppliers.Clear();
                SelectedVariantForSupplierEdit = null;

                foreach (var variant in fullItem.Variants
                             .OrderBy(v => v.IsDeactivated)
                             .ThenBy(v => v.VariantDescription)
                             .ThenBy(v => v.SkuCode))
                {
                    variant.PropertyMappings ??= new List<ItemPropertyMapping>();
                    variant.ItemSuppliers ??= new List<ItemSupplier>();
                    variant.IsSelectedForSupplierAssignment = false;

                    ApplyParentDisplayNames(variant);

                    GeneratedVariants.Add(variant);
                }

                RebuildBuilderSelectionFromVariants();
                UpdateSupplierAssignmentSelectionCount();

                StatusMessage = fullItem.IsDeactivated
                    ? $"Loaded deactivated item: {fullItem.ItemCode}"
                    : $"Loaded item: {fullItem.ItemCode}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item.";

                _messageBoxService.ShowError(
                    $"Failed to load item:\n\n{ex.Message}",
                    "Database Error");
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
                    AverageCost = BulkCost,
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>(),
                    PropertyMappings = new List<ItemPropertyMapping>(),
                    IsSelectedForSupplierAssignment = false
                };

                ApplyParentDisplayNames(standardVariant);
                RestoreSupplierLinksIfAvailable(standardVariant, existingSupplierLinks);

                GeneratedVariants.Add(standardVariant);

                UpdateSupplierAssignmentSelectionCount();

                StatusMessage = "1 standard variant generated. The word 'Standard' will not be printed in the final product name.";
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
                    AverageCost = BulkCost,
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>(),
                    PropertyMappings = new List<ItemPropertyMapping>(),
                    IsSelectedForSupplierAssignment = false
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

                GeneratedVariants.Add(variant);
            }

            UpdateSupplierAssignmentSelectionCount();

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
                        SupplierItemCode = string.Empty,
                        LastCostPrice = s.LastCostPrice,
                        IsPrimary = false,
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

        partial void OnBulkHasBatchTrackingChanged(bool value)
        {
            if (!value)
            {
                BulkHasExpiryTracking = false;
                BulkHasBatchExpiry = false;
            }

            NotifyCommandStates();
        }

        partial void OnBulkHasExpiryTrackingChanged(bool value)
        {
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
            SaveCommand.NotifyCanExecuteChanged();
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
                _messageBoxService.ShowWarning(
                    "Generate variants first before applying bulk defaults.",
                    "No Variants");

                return;
            }

            var variants = GeneratedVariants.ToList();

            foreach (var variant in variants)
            {
                variant.AverageCost = BulkCost;
                variant.CostPrice = BulkCost;
                variant.RetailPrice = BulkRetailPrice;
                variant.WholesalePrice = BulkWholesalePrice;
                variant.MinimumPrice = BulkMinimumPrice;
                variant.MaximumPrice = BulkMaximumPrice;
                variant.ReorderLevel = BulkReorderLevel;

                ApplyParentDisplayNames(variant);
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

            UpdateSupplierAssignmentSelectionCount();
        }

        // =========================================================
        // VARIANT SUPPLIER SELECTION / ASSIGNMENT
        // =========================================================

        partial void OnSelectAllVariantsForSupplierAssignmentChanged(bool value)
        {
            if (_isUpdatingSupplierSelection)
                return;

            var variants = GeneratedVariants.ToList();

            foreach (var variant in variants)
                variant.IsSelectedForSupplierAssignment = value;

            RefreshGeneratedVariantGrid(variants);
            UpdateSupplierAssignmentSelectionCount();
        }

        [RelayCommand]
        private void RefreshSupplierAssignmentSelection()
        {
            UpdateSupplierAssignmentSelectionCount();
        }

        private void UpdateSupplierAssignmentSelectionCount()
        {
            int selectedCount = GeneratedVariants.Count(v => v.IsSelectedForSupplierAssignment);
            bool allSelected = GeneratedVariants.Any() && selectedCount == GeneratedVariants.Count;

            _isUpdatingSupplierSelection = true;

            try
            {
                SelectedSupplierAssignmentCount = selectedCount;
                SelectAllVariantsForSupplierAssignment = allSelected;
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
            UpdateSupplierAssignmentSelectionCount();

            var selectedVariants = GeneratedVariants
                .Where(v => v.IsSelectedForSupplierAssignment)
                .ToList();

            if (!selectedVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "Please tick one or more variants first.",
                    "Selection Required");

                return;
            }

            if (!AvailableSuppliers.Any())
            {
                _messageBoxService.ShowWarning(
                    "No active suppliers found. Please create suppliers first.",
                    "Supplier Required");

                return;
            }

            decimal defaultCost = selectedVariants
                .FirstOrDefault(v => v.CostPrice > 0)?.CostPrice ?? BulkCost;

            if (defaultCost < 0)
                defaultCost = 0m;

            var dialog = new AssignVariantSuppliersDialog(
                AvailableSuppliers.ToList(),
                defaultCost,
                defaultMoq: 1,
                selectedVariantCount: selectedVariants.Count)
            {
                Owner = GetDialogOwner()
            };

            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult != true)
                return;

            Supplier? supplier = dialog.SelectedSupplier;

            if (supplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Validation");

                return;
            }

            if (dialog.SupplierCost < 0)
            {
                _messageBoxService.ShowWarning(
                    "Supplier cost cannot be negative.",
                    "Validation");

                return;
            }

            if (dialog.MinimumOrderQuantity <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Minimum order quantity must be greater than zero.",
                    "Validation");

                return;
            }

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var variant in selectedVariants)
            {
                AddOrUpdateSupplierLinkForVariant(
                    variant,
                    supplier,
                    dialog.SupplierCost,
                    dialog.MinimumOrderQuantity,
                    ref addedCount,
                    ref updatedCount);
            }

            if (SelectedVariantForSupplierEdit != null &&
                selectedVariants.Any(v => string.Equals(v.SkuCode, SelectedVariantForSupplierEdit.SkuCode, StringComparison.OrdinalIgnoreCase)))
            {
                RebuildSelectedVariantSuppliers();
            }

            StatusMessage =
                $"Supplier assignment completed. Selected variants: {selectedVariants.Count}, Added: {addedCount}, Updated: {updatedCount}. Click SAVE FULL ITEM to update the database.";

            NotifyCommandStates();
        }

        private void AddOrUpdateSupplierLinkForVariant(
            ItemVariant variant,
            Supplier supplier,
            decimal cost,
            int minimumOrderQuantity,
            ref int addedCount,
            ref int updatedCount)
        {
            variant.ItemSuppliers ??= new List<ItemSupplier>();

            var existing = variant.ItemSuppliers
                .FirstOrDefault(s => s.SupplierId == supplier.Id);

            if (existing == null)
            {
                variant.ItemSuppliers.Add(new ItemSupplier
                {
                    SupplierId = supplier.Id,
                    Supplier = supplier,
                    ItemVariantId = variant.Id,
                    SupplierItemCode = string.Empty,
                    LastCostPrice = cost,
                    MinimumOrderQuantity = minimumOrderQuantity,
                    IsPrimary = false
                });

                addedCount++;
                return;
            }

            existing.Supplier = supplier;
            existing.SupplierItemCode = string.Empty;
            existing.LastCostPrice = cost;
            existing.MinimumOrderQuantity = minimumOrderQuantity;
            existing.IsPrimary = false;
            updatedCount++;
        }

        private static Window? GetDialogOwner()
        {
            return Application.Current?.Windows
                       .OfType<Window>()
                       .FirstOrDefault(w => w.IsActive)
                   ?? Application.Current?.MainWindow;
        }

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

            foreach (var supplier in value.ItemSuppliers)
            {
                supplier.SupplierItemCode = string.Empty;
                supplier.IsPrimary = false;
                SelectedVariantSuppliers.Add(supplier);
            }

            SupplierToAdd = null;
            SupplierCostInput = value.CostPrice;
            SupplierMinimumOrderQuantityInput = 1;

            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedSupplierLinkForEditChanged(ItemSupplier? value)
        {
            if (value == null)
                return;

            SupplierToAdd =
                AvailableSuppliers.FirstOrDefault(s => s.Id == value.SupplierId) ??
                value.Supplier;

            SupplierCostInput = value.LastCostPrice;
            SupplierMinimumOrderQuantityInput = value.MinimumOrderQuantity <= 0
                ? 1
                : value.MinimumOrderQuantity;

            AddSupplierToVariantCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void ClearSupplierEdit()
        {
            SelectedSupplierLinkForEdit = null;
            SupplierToAdd = null;
            SupplierCostInput = SelectedVariantForSupplierEdit?.CostPrice ?? 0m;
            SupplierMinimumOrderQuantityInput = 1;

            StatusMessage = "Supplier edit cleared.";
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
                _messageBoxService.ShowWarning(
                    "Please select a variant first.",
                    "Selection Required");

                return;
            }

            if (SupplierToAdd == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Validation");

                return;
            }

            if (SupplierCostInput < 0)
            {
                _messageBoxService.ShowWarning(
                    "Supplier cost cannot be negative.",
                    "Validation");

                return;
            }

            if (SupplierMinimumOrderQuantityInput <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Minimum order quantity must be greater than zero.",
                    "Validation");

                return;
            }

            SelectedVariantForSupplierEdit.ItemSuppliers ??= new List<ItemSupplier>();

            ItemSupplier? editTarget = null;

            if (SelectedSupplierLinkForEdit != null)
            {
                editTarget = SelectedVariantForSupplierEdit.ItemSuppliers
                    .FirstOrDefault(s => ReferenceEquals(s, SelectedSupplierLinkForEdit));

                editTarget ??= SelectedVariantForSupplierEdit.ItemSuppliers
                    .FirstOrDefault(s => s.SupplierId == SelectedSupplierLinkForEdit.SupplierId);
            }

            if (editTarget != null)
            {
                bool targetSupplierAlreadyExists = SelectedVariantForSupplierEdit.ItemSuppliers.Any(s =>
                    !ReferenceEquals(s, editTarget) &&
                    s.SupplierId == SupplierToAdd.Id);

                if (targetSupplierAlreadyExists)
                {
                    _messageBoxService.ShowWarning(
                        "This supplier is already assigned to the selected variant.",
                        "Duplicate Supplier");

                    return;
                }

                editTarget.SupplierId = SupplierToAdd.Id;
                editTarget.Supplier = SupplierToAdd;
                editTarget.ItemVariantId = SelectedVariantForSupplierEdit.Id;
                editTarget.SupplierItemCode = string.Empty;
                editTarget.LastCostPrice = SupplierCostInput;
                editTarget.MinimumOrderQuantity = SupplierMinimumOrderQuantityInput;
                editTarget.IsPrimary = false;

                RebuildSelectedVariantSuppliers();

                SelectedSupplierLinkForEdit = SelectedVariantSuppliers
                    .FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);

                StatusMessage = "Supplier link updated. Click SAVE FULL ITEM to update the database.";
                return;
            }

            var existing = SelectedVariantForSupplierEdit.ItemSuppliers
                .FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);

            if (existing != null)
            {
                existing.Supplier = SupplierToAdd;
                existing.SupplierItemCode = string.Empty;
                existing.LastCostPrice = SupplierCostInput;
                existing.MinimumOrderQuantity = SupplierMinimumOrderQuantityInput;
                existing.IsPrimary = false;

                RebuildSelectedVariantSuppliers();

                SelectedSupplierLinkForEdit = SelectedVariantSuppliers
                    .FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);

                StatusMessage = "Existing supplier link updated. Click SAVE FULL ITEM to update the database.";
                return;
            }

            var newSupplierLink = new ItemSupplier
            {
                SupplierId = SupplierToAdd.Id,
                Supplier = SupplierToAdd,
                ItemVariantId = SelectedVariantForSupplierEdit.Id,
                SupplierItemCode = string.Empty,
                LastCostPrice = SupplierCostInput,
                IsPrimary = false,
                MinimumOrderQuantity = SupplierMinimumOrderQuantityInput
            };

            SelectedVariantForSupplierEdit.ItemSuppliers.Add(newSupplierLink);

            RebuildSelectedVariantSuppliers();

            SelectedSupplierLinkForEdit = SelectedVariantSuppliers
                .FirstOrDefault(s => s.SupplierId == SupplierToAdd.Id);

            StatusMessage = "Supplier link added. Click SAVE FULL ITEM to update the database.";
        }

        [RelayCommand]
        private void RemoveSupplierFromVariant(ItemSupplier? itemSupplier)
        {
            if (itemSupplier == null || SelectedVariantForSupplierEdit == null)
                return;

            var suppliers = SelectedVariantForSupplierEdit.ItemSuppliers?
                .ToList() ?? new List<ItemSupplier>();

            var removeTarget = suppliers.FirstOrDefault(s =>
                ReferenceEquals(s, itemSupplier) ||
                s.SupplierId == itemSupplier.SupplierId);

            if (removeTarget != null)
                suppliers.Remove(removeTarget);

            SelectedVariantForSupplierEdit.ItemSuppliers = suppliers;

            if (SelectedSupplierLinkForEdit != null &&
                SelectedSupplierLinkForEdit.SupplierId == itemSupplier.SupplierId)
            {
                ClearSupplierEdit();
            }

            RebuildSelectedVariantSuppliers();

            StatusMessage = "Supplier removed from selected variant. Click SAVE FULL ITEM to update the database.";
        }

        private void RebuildSelectedVariantSuppliers()
        {
            SelectedVariantSuppliers.Clear();

            if (SelectedVariantForSupplierEdit?.ItemSuppliers == null)
                return;

            foreach (var supplier in SelectedVariantForSupplierEdit.ItemSuppliers
                         .OrderBy(s => s.Supplier?.SupplierCode)
                         .ThenBy(s => s.Supplier?.SupplierName))
            {
                supplier.SupplierItemCode = string.Empty;
                supplier.IsPrimary = false;
                SelectedVariantSuppliers.Add(supplier);
            }
        }

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
                _messageBoxService.ShowWarning(
                    "Generate variants first before assigning suppliers.",
                    "No Variants");

                return;
            }

            if (BulkSupplierToAssign == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Validation");

                return;
            }

            if (BulkSupplierCostInput < 0)
            {
                _messageBoxService.ShowWarning(
                    "Supplier cost cannot be negative.",
                    "Validation");

                return;
            }

            if (BulkSupplierMinimumOrderQuantityInput <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Minimum order quantity must be greater than zero.",
                    "Validation");

                return;
            }

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var variant in GeneratedVariants)
            {
                variant.ItemSuppliers ??= new List<ItemSupplier>();

                var existing = variant.ItemSuppliers
                    .FirstOrDefault(s => s.SupplierId == BulkSupplierToAssign.Id);

                if (existing == null)
                {
                    var link = new ItemSupplier
                    {
                        SupplierId = BulkSupplierToAssign.Id,
                        Supplier = BulkSupplierToAssign,
                        ItemVariantId = variant.Id,
                        SupplierItemCode = string.Empty,
                        LastCostPrice = BulkSupplierCostInput,
                        MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput,
                        IsPrimary = false
                    };

                    variant.ItemSuppliers.Add(link);
                    addedCount++;
                }
                else
                {
                    existing.Supplier = BulkSupplierToAssign;
                    existing.SupplierItemCode = string.Empty;
                    existing.LastCostPrice = BulkSupplierCostInput;
                    existing.MinimumOrderQuantity = BulkSupplierMinimumOrderQuantityInput;
                    existing.IsPrimary = false;
                    updatedCount++;
                }
            }

            if (SelectedVariantForSupplierEdit != null)
                RebuildSelectedVariantSuppliers();

            StatusMessage = $"Supplier bulk assignment completed. Added: {addedCount}, Updated: {updatedCount}. Click SAVE FULL ITEM to update the database.";
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string itemCode = BuildItemCode();

            if (!ValidateBeforeSave(itemCode))
                return;

            IsBusy = true;

            try
            {
                ApplyParentDisplayNamesToAllVariants();

                bool itemCodeUnique = await _itemMasterRepository.IsItemCodeUniqueAsync(
                    itemCode,
                    CurrentItem.Id);

                if (!itemCodeUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"Item code '{itemCode}' already exists.",
                        "Duplicate Item Code");

                    return;
                }

                foreach (var variant in GeneratedVariants)
                {
                    bool skuUnique = await _itemMasterRepository.IsSkuCodeUniqueAsync(
                        variant.SkuCode,
                        variant.Id);

                    if (!skuUnique)
                    {
                        _messageBoxService.ShowWarning(
                            $"SKU '{variant.SkuCode}' already exists.",
                            "Duplicate SKU");

                        return;
                    }

                    bool barcodeUnique = await _itemMasterRepository.IsBarcodeUniqueAsync(
                        variant.Barcode,
                        variant.Id);

                    if (!barcodeUnique)
                    {
                        _messageBoxService.ShowWarning(
                            $"Barcode '{variant.Barcode}' already exists.",
                            "Duplicate Barcode");

                        return;
                    }
                }

                CurrentItem.ItemCode = itemCode;
                CurrentItem.CategoryId = SelectedCategory!.Id;
                CurrentItem.SubCategoryId = SelectedSubCategory?.Id;
                CurrentItem.UnitOfMeasureId = SelectedUom!.Id;
                CurrentItem.BaseUom = SelectedUom.UomCode;
                CurrentItem.TaxCode = SelectedTaxCode;

                CurrentItem.IsScaleItem = BulkIsScaleItem;
                CurrentItem.HasBatchTracking = BulkHasBatchTracking;
                CurrentItem.HasExpiryTracking = BulkHasExpiryTracking;
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
                        supplier.SupplierItemCode = string.Empty;
                        supplier.IsPrimary = false;
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

                _messageBoxService.ShowInformation(
                    "Item saved successfully.",
                    "Success");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Save failed.";

                _messageBoxService.ShowError(
                    $"Failed to save item:\n\n{message}",
                    "Database Error");
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

                _messageBoxService.ShowError(
                    $"Failed to load item database:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadMasterGridInternalAsync()
        {
            Items.Clear();

            var data = await _itemMasterRepository.GetSummariesAsync(
                searchTerm: MasterSearchText,
                includeDeactivated: IncludeDeactivatedItems);

            foreach (var item in data)
                Items.Add(item);

            StatusMessage = IncludeDeactivatedItems
                ? $"{Items.Count} item record(s) loaded, including deactivated items."
                : $"{Items.Count} active item record(s) loaded.";
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

            NotifyCommandStates();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedDatabaseItem == null)
                return;

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Deactivate item '{SelectedDatabaseItem.ItemName}' and all its variants?\n\n" +
                "This keeps sales, GRN, stock, and transaction history safe.\n\n" +
                "To find it again later, tick 'Include Deactivated' in the Item Database.",
                "Confirm Deactivation",
                MessageBoxImage.Warning);

            if (!confirmed)
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

                _messageBoxService.ShowError(
                    $"Failed to deactivate item:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnMasterSearchTextChanged(string value)
        {
            if (_isInitialized)
                StatusMessage = "Type search text and click SEARCH.";
        }

        partial void OnIncludeDeactivatedItemsChanged(bool value)
        {
            if (_isInitialized && !IsBusy)
                _ = LoadMasterGridAsync();
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
            DeleteCommand.NotifyCanExecuteChanged();
        }

        private bool CanInitialize()
        {
            return !IsBusy && !_isInitialized;
        }

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

        private bool CanAssignSupplierToSelectedVariants()
        {
            return !IsBusy &&
                   GeneratedVariants.Any(v => v.IsSelectedForSupplierAssignment);
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
                _messageBoxService.ShowWarning("Please select a category.", "Validation Error");
                return false;
            }

            if (SelectedSubCategory == null)
            {
                _messageBoxService.ShowWarning("Please select a sub-category.", "Validation Error");
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
                _messageBoxService.ShowWarning("Price values cannot be negative.", "Validation Error");
                return false;
            }

            if (BulkMaximumPrice > 0 && BulkMinimumPrice > BulkMaximumPrice)
            {
                _messageBoxService.ShowWarning("Minimum price cannot be greater than maximum price.", "Validation Error");
                return false;
            }

            if (BulkReorderLevel < 0)
            {
                _messageBoxService.ShowWarning("Reorder level cannot be negative.", "Validation Error");
                return false;
            }

            if (!BulkHasBatchTracking && BulkHasExpiryTracking)
            {
                _messageBoxService.ShowWarning("Expiry tracking requires batch tracking.", "Validation Error");
                return false;
            }

            if (BulkIsScaleItem && SelectedUom != null && !SelectedUom.AllowDecimals)
            {
                bool continueAnyway = _messageBoxService.ShowConfirmation(
                    $"The selected UOM '{SelectedUom.UomCode}' does not allow decimals.\n\n" +
                    "Scale items normally need a decimal UOM such as KG, G, L, or M.\n\n" +
                    "Continue anyway?",
                    "Scale Item Warning",
                    MessageBoxImage.Warning);

                if (!continueAnyway)
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
                _messageBoxService.ShowWarning("Item name is required.", "Validation Error");
                return false;
            }

            if (CurrentItem.ItemName.Trim().Length > 150)
            {
                _messageBoxService.ShowWarning("Item name cannot be longer than 150 characters.", "Validation Error");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CurrentItem.PrintName) &&
                CurrentItem.PrintName.Trim().Length > 50)
            {
                _messageBoxService.ShowWarning("Print name cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (SelectedUom == null)
            {
                _messageBoxService.ShowWarning("Please select a Unit of Measure.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedTaxCode))
            {
                _messageBoxService.ShowWarning("Please select a tax code.", "Validation Error");
                return false;
            }

            if (!GeneratedVariants.Any())
            {
                _messageBoxService.ShowWarning("Generate variants before saving.", "Validation Error");
                return false;
            }

            foreach (var variant in GeneratedVariants)
            {
                variant.ItemSuppliers ??= new List<ItemSupplier>();

                var duplicateSupplier = variant.ItemSuppliers
                    .GroupBy(s => s.SupplierId)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicateSupplier != null)
                {
                    _messageBoxService.ShowWarning(
                        $"Variant '{variant.SkuCode}' has the same supplier assigned more than once.",
                        "Validation Error");

                    return false;
                }

                foreach (var supplier in variant.ItemSuppliers)
                {
                    supplier.SupplierItemCode = string.Empty;
                    supplier.IsPrimary = false;

                    if (supplier.MinimumOrderQuantity <= 0)
                    {
                        _messageBoxService.ShowWarning(
                            $"Variant '{variant.SkuCode}' has invalid supplier MOQ.",
                            "Validation Error");

                        return false;
                    }

                    if (supplier.LastCostPrice < 0)
                    {
                        _messageBoxService.ShowWarning(
                            $"Variant '{variant.SkuCode}' has invalid supplier cost.",
                            "Validation Error");

                        return false;
                    }
                }
            }

            return true;
        }

        private bool ValidateItemCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                _messageBoxService.ShowWarning("Item code is required.", "Validation Error");
                return false;
            }

            if (itemCode.Length > 50)
            {
                _messageBoxService.ShowWarning("Item code cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (itemCode.EndsWith("-"))
            {
                _messageBoxService.ShowWarning(
                    "Item code is incomplete. Please enter the item suffix after the category/sub-category prefix.",
                    "Validation Error");

                return false;
            }

            if (!CodeRegex.IsMatch(itemCode))
            {
                _messageBoxService.ShowWarning(
                    "Item code can only contain letters, numbers, dash, and underscore.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private void ApplyParentDisplayNamesToAllVariants()
        {
            foreach (var variant in GeneratedVariants)
                ApplyParentDisplayNames(variant);
        }

        private void ApplyParentDisplayNames(ItemVariant variant)
        {
            variant.ParentItemName = NormalizeText(CurrentItem.ItemName);
            variant.ParentPrintName = NormalizeText(CurrentItem.PrintName);
        }

        private static string NormalizeCode(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}