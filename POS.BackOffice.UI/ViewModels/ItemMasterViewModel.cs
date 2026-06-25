using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
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

    public partial class ItemMasterViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly AttributeRepository _attributeRepository;
        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly SupplierRepository _supplierRepository;

        private bool _isLoadingItem = false;

        // --- ZONE 1: PARENT IDENTITY & CLASSIFICATION ---
        [ObservableProperty] private ItemParent _currentItem = new();
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private SubCategory? _selectedSubCategory;

        [ObservableProperty] private string _itemPrefix = string.Empty;
        [ObservableProperty] private string _itemSuffix = string.Empty;
        [ObservableProperty] private bool _isCodeReadOnly = false;

        [ObservableProperty] private string _selectedUom = string.Empty;
        [ObservableProperty] private string _selectedTaxCode = string.Empty;

        // --- ZONE 2: VARIANT BUILDER ---
        [ObservableProperty] private AttributeGroup? _selectedPropertyKey;
        [ObservableProperty] private AttributeValue? _propertyValueInput;
        public ObservableCollection<MatrixPropertySelection> DynamicProperties { get; set; } = new();

        // --- ZONE 3: BULK PRICING & LOGISTICS ---
        [ObservableProperty] private decimal _bulkCost = 0m;
        [ObservableProperty] private int _bulkReorderLevel = 0;
        [ObservableProperty] private decimal _bulkRetailMarkupPercent = 0m;
        [ObservableProperty] private decimal _bulkRetailPrice = 0m;
        [ObservableProperty] private decimal _bulkWholesaleMarkupPercent = 0m;
        [ObservableProperty] private decimal _bulkWholesalePrice = 0m;
        [ObservableProperty] private decimal _bulkMinimumPrice = 0m;
        [ObservableProperty] private decimal _bulkMaximumPrice = 0m;

        [ObservableProperty] private bool _bulkIsScaleItem = false;
        [ObservableProperty] private bool _bulkHasBatchExpiry = false;
        [ObservableProperty] private bool _bulkIsSerialized = false;

        // --- ZONE 4 & 6: GRIDS ---
        public ObservableCollection<ItemVariant> GeneratedVariants { get; set; } = new();
        public ObservableCollection<ItemMasterSummaryDto> Items { get; set; } = new();

        [ObservableProperty] private string _masterSearchText = string.Empty;
        [ObservableProperty] private ItemMasterSummaryDto? _selectedDatabaseItem;

        // --- ZONE 7: SUPPLIER MANAGEMENT STATE ---
        public ObservableCollection<Supplier> AvailableSuppliers { get; set; } = new();
        [ObservableProperty] private ItemVariant? _selectedVariantForSupplierEdit;
        public ObservableCollection<ItemSupplier> SelectedVariantSuppliers { get; set; } = new();

        [ObservableProperty] private Supplier? _supplierToAdd;
        [ObservableProperty] private string _supplierItemCodeInput = string.Empty;
        [ObservableProperty] private decimal _supplierCostInput = 0m;

        // --- LOOKUPS ---
        public ObservableCollection<Category> Categories { get; set; } = new();
        public ObservableCollection<SubCategory> SubCategories { get; set; } = new();
        public ObservableCollection<AttributeGroup> PropertyKeys { get; set; } = new();
        public ObservableCollection<AttributeValue> PropertyValues { get; set; } = new();
        public ObservableCollection<string> Uoms { get; set; } = new();
        public ObservableCollection<string> TaxCodes { get; set; } = new(new[] { "TAX-FREE", "VAT-18", "VAT-5" });

        private static readonly Random _random = new Random();

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
            Categories.Clear();
            var categories = await _categoryRepository.GetAllAsync();
            foreach (var cat in categories.Where(c => !c.IsDeactivated)) Categories.Add(cat);

            Uoms.Clear();
            var dbUoms = await _uomRepository.GetAllAsync();
            foreach (var uom in dbUoms.Where(u => u.IsActive)) Uoms.Add(uom.UomCode);

            AvailableSuppliers.Clear();
            var suppliers = await _supplierRepository.GetAllAsync();
            foreach (var sup in suppliers.Where(s => !s.IsDeactivated)) AvailableSuppliers.Add(sup);

            await LoadMasterGridAsync();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value == null) return;
            CurrentItem.CategoryId = value.Id;

            if (!_isLoadingItem)
            {
                _ = LoadSubCategoriesAsync(value.Id);
                _ = LoadPropertyKeysForCategoryAsync(value.Id);
                SelectedSubCategory = null;
            }
        }

        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (value != null && SelectedCategory != null && !IsCodeReadOnly && !_isLoadingItem)
            {
                ItemPrefix = $"{SelectedCategory.CategoryCode}-{value.SubCategoryCode}-";
            }
        }

        partial void OnSelectedDatabaseItemChanged(ItemMasterSummaryDto? value)
        {
            if (value != null)
            {
                _ = LoadFullItemDetailsAsync(value.ParentId);
            }
            else
            {
                IsCodeReadOnly = false;
            }
        }

        partial void OnSelectedVariantForSupplierEditChanged(ItemVariant? value)
        {
            SelectedVariantSuppliers.Clear();
            if (value != null && value.ItemSuppliers != null)
            {
                foreach (var sup in value.ItemSuppliers)
                {
                    SelectedVariantSuppliers.Add(sup);
                }
                SupplierCostInput = value.CostPrice;
            }
        }

        private async Task LoadFullItemDetailsAsync(int parentId)
        {
            _isLoadingItem = true;

            try
            {
                var fullItem = await _itemMasterRepository.GetFullMatrixByIdAsync(parentId);
                if (fullItem == null) return;

                CurrentItem = fullItem;
                IsCodeReadOnly = true;
                ItemPrefix = fullItem.ItemCode;
                ItemSuffix = string.Empty;

                SelectedCategory = Categories.FirstOrDefault(c => c.Id == fullItem.CategoryId);

                await LoadSubCategoriesAsync(fullItem.CategoryId);
                SelectedSubCategory = SubCategories.FirstOrDefault(s => s.Id == fullItem.SubCategoryId);

                await LoadPropertyKeysForCategoryAsync(fullItem.CategoryId);

                SelectedUom = fullItem.BaseUom;
                SelectedTaxCode = fullItem.TaxCode;

                BulkIsScaleItem = fullItem.IsScaleItem;
                BulkHasBatchExpiry = fullItem.HasBatchExpiry;
                BulkIsSerialized = fullItem.IsSerialized;

                GeneratedVariants.Clear();
                SelectedVariantSuppliers.Clear();

                foreach (var variant in fullItem.Variants.Where(v => !v.IsDeactivated))
                {
                    if (variant.ItemSuppliers == null) variant.ItemSuppliers = new List<ItemSupplier>();
                    GeneratedVariants.Add(variant);
                }

                DynamicProperties.Clear();
            }
            finally
            {
                _isLoadingItem = false;
            }
        }

        private async Task LoadSubCategoriesAsync(int categoryId)
        {
            SubCategories.Clear();
            var subCats = await _subCategoryRepository.GetAllAsync();
            foreach (var sub in subCats.Where(s => !s.IsDeactivated && s.CategoryId == categoryId)) SubCategories.Add(sub);
        }

        private async Task LoadPropertyKeysForCategoryAsync(int categoryId)
        {
            PropertyKeys.Clear();
            var groups = await _attributeRepository.GetAllGroupsAsync();
            var assignedIds = await _attributeRepository.GetAssignedCategoryIdsForGroupAsync(categoryId);

            foreach (var g in groups.Where(g => assignedIds.Contains(g.Id)))
            {
                PropertyKeys.Add(g);
            }
        }

        partial void OnSelectedPropertyKeyChanged(AttributeGroup? value)
        {
            if (value == null)
            {
                PropertyValues.Clear();
                return;
            }
            _ = LoadPropertyValuesAsync(value.Id);
        }

        private async Task LoadPropertyValuesAsync(int groupId)
        {
            PropertyValues.Clear();
            var values = await _attributeRepository.GetAllValuesFilteredAsync(groupId, "");
            foreach (var val in values.Where(v => !v.IsDeactivated)) PropertyValues.Add(val);
        }

        [RelayCommand]
        private void AddSupplierToVariant()
        {
            if (SelectedVariantForSupplierEdit == null)
            {
                MessageBox.Show("Please select a Variant from the main grid first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SupplierToAdd == null)
            {
                MessageBox.Show("Please select a Supplier from the dropdown.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SupplierCostInput <= 0)
            {
                MessageBox.Show("Supplier Cost must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedVariantSuppliers.Any(s => s.SupplierId == SupplierToAdd.Id))
            {
                MessageBox.Show("This supplier is already attached to this variant.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newBridge = new ItemSupplier
            {
                SupplierId = SupplierToAdd.Id,
                Supplier = SupplierToAdd,
                ItemVariantId = SelectedVariantForSupplierEdit.Id,
                SupplierItemCode = SupplierItemCodeInput,
                LastCostPrice = SupplierCostInput,
                IsPrimary = false, // Ignored by UI, defaults to false to satisfy the DB constraint safely
                MinimumOrderQuantity = 1
            };

            SelectedVariantSuppliers.Add(newBridge);
            SelectedVariantForSupplierEdit.ItemSuppliers.Add(newBridge);

            SupplierToAdd = null;
            SupplierItemCodeInput = string.Empty;
        }

        [RelayCommand]
        private void RemoveSupplierFromVariant(ItemSupplier? itemSupplier)
        {
            if (itemSupplier == null || SelectedVariantForSupplierEdit == null) return;
            SelectedVariantSuppliers.Remove(itemSupplier);
            SelectedVariantForSupplierEdit.ItemSuppliers.Remove(itemSupplier);
        }

        [RelayCommand]
        private void AddProperty()
        {
            if (SelectedPropertyKey == null || PropertyValueInput == null) return;

            if (!DynamicProperties.Any(p => p.Group.Id == SelectedPropertyKey.Id && p.Value.Id == PropertyValueInput.Id))
            {
                DynamicProperties.Add(new MatrixPropertySelection
                {
                    Group = SelectedPropertyKey,
                    Value = PropertyValueInput
                });
            }
        }

        [RelayCommand]
        private void RemoveProperty(MatrixPropertySelection selection)
        {
            if (selection != null) DynamicProperties.Remove(selection);
        }

        [RelayCommand]
        private void GenerateVariants()
        {
            CurrentItem.ItemCode = $"{ItemPrefix}{ItemSuffix}".Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(CurrentItem.ItemCode))
            {
                MessageBox.Show("Please complete the Item Code (Prefix + Suffix) first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GeneratedVariants.Clear();
            SelectedVariantSuppliers.Clear();

            if (!DynamicProperties.Any())
            {
                GeneratedVariants.Add(new ItemVariant
                {
                    SkuCode = CurrentItem.ItemCode,
                    VariantDescription = "Standard",
                    Barcode = GenerateUniqueBarcode(),
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>()
                });
                return;
            }

            var groupedProperties = DynamicProperties.GroupBy(p => p.Group.Id).Select(g => g.ToList()).ToList();
            var combinations = GenerateCombinations(groupedProperties);

            foreach (var combo in combinations)
            {
                string skuSuffix = string.Join("-", combo.Select(c => c.Value.ValueName.Substring(0, Math.Min(3, c.Value.ValueName.Length)).ToUpper()));
                string desc = string.Join(" / ", combo.Select(c => c.Value.ValueName));

                var variant = new ItemVariant
                {
                    SkuCode = $"{CurrentItem.ItemCode}-{skuSuffix}",
                    VariantDescription = desc,
                    Barcode = GenerateUniqueBarcode(),
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice,
                    WholesalePrice = BulkWholesalePrice,
                    MinimumPrice = BulkMinimumPrice,
                    MaximumPrice = BulkMaximumPrice,
                    ReorderLevel = BulkReorderLevel,
                    ItemSuppliers = new List<ItemSupplier>()
                };

                foreach (var prop in combo)
                {
                    variant.PropertyMappings.Add(new ItemPropertyMapping
                    {
                        AttributeGroupId = prop.Group.Id,
                        AttributeValueId = prop.Value.Id,
                        ItemVariant = variant
                    });
                }

                GeneratedVariants.Add(variant);
            }
        }

        private string GenerateUniqueBarcode()
        {
            char[] digits = new char[12];
            for (int i = 0; i < 12; i++)
            {
                digits[i] = (char)('0' + _random.Next(0, 10));
            }
            return new string(digits);
        }

        private List<List<MatrixPropertySelection>> GenerateCombinations(List<List<MatrixPropertySelection>> groups, int depth = 0)
        {
            var result = new List<List<MatrixPropertySelection>>();
            if (depth == groups.Count)
            {
                result.Add(new List<MatrixPropertySelection>());
                return result;
            }

            var currentGroup = groups[depth];
            var subsequentCombinations = GenerateCombinations(groups, depth + 1);

            foreach (var prop in currentGroup)
            {
                foreach (var combo in subsequentCombinations)
                {
                    var newCombo = new List<MatrixPropertySelection> { prop };
                    newCombo.AddRange(combo);
                    result.Add(newCombo);
                }
            }
            return result;
        }

        partial void OnBulkCostChanged(decimal value) => CalculateRetail();
        partial void OnBulkRetailMarkupPercentChanged(decimal value) => CalculateRetail();
        partial void OnBulkWholesaleMarkupPercentChanged(decimal value) => CalculateRetail();

        private void CalculateRetail()
        {
            BulkRetailPrice = BulkCost + (BulkCost * (BulkRetailMarkupPercent / 100m));
            BulkWholesalePrice = BulkCost + (BulkCost * (BulkWholesaleMarkupPercent / 100m));
        }

        [RelayCommand]
        private void ApplyBulkDefaults()
        {
            if (!GeneratedVariants.Any()) return;

            var tempList = GeneratedVariants.ToList();

            foreach (var variant in tempList)
            {
                variant.CostPrice = BulkCost;
                variant.RetailPrice = BulkRetailPrice;
                variant.WholesalePrice = BulkWholesalePrice;
                variant.MinimumPrice = BulkMinimumPrice;
                variant.MaximumPrice = BulkMaximumPrice;
                variant.ReorderLevel = BulkReorderLevel;
            }

            GeneratedVariants.Clear();
            foreach (var variant in tempList)
            {
                GeneratedVariants.Add(variant);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            CurrentItem.ItemCode = $"{ItemPrefix}{ItemSuffix}".Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(CurrentItem.ItemCode) || string.IsNullOrWhiteSpace(CurrentItem.ItemName))
            {
                MessageBox.Show("Parent Item Code and Name are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedCategory == null || SelectedSubCategory == null)
            {
                MessageBox.Show("Please select a Category and Sub-Category before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!GeneratedVariants.Any())
            {
                MessageBox.Show("You must Generate Matrix Variants before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool isUnique = await _itemMasterRepository.IsItemCodeUniqueAsync(CurrentItem.ItemCode, CurrentItem.Id);
                if (!isUnique)
                {
                    MessageBox.Show("This Item Code is already in use.", "Duplicate Code", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CurrentItem.CategoryId = SelectedCategory.Id;
                CurrentItem.SubCategoryId = SelectedSubCategory.Id;
                CurrentItem.BaseUom = SelectedUom ?? string.Empty;
                CurrentItem.TaxCode = SelectedTaxCode ?? string.Empty;
                CurrentItem.IsScaleItem = BulkIsScaleItem;
                CurrentItem.HasBatchExpiry = BulkHasBatchExpiry;
                CurrentItem.IsSerialized = BulkIsSerialized;

                CurrentItem.Category = null!;
                CurrentItem.SubCategory = null!;

                var mappingsList = new List<ItemPropertyMapping>();
                foreach (var variant in GeneratedVariants)
                {
                    foreach (var mapping in variant.PropertyMappings)
                    {
                        mappingsList.Add(mapping);
                    }
                }

                await _itemMasterRepository.SaveFullMatrixAsync(CurrentItem, GeneratedVariants.ToList(), mappingsList);

                await LoadMasterGridAsync();
                Clear();
                MessageBox.Show("Matrix Item saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Failed to save item: {errorMsg}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadMasterGridAsync()
        {
            Items.Clear();
            var data = await _itemMasterRepository.GetSummariesAsync(MasterSearchText);
            foreach (var item in data) Items.Add(item);
        }

        [RelayCommand]
        private async Task SearchItemsAsync() => await LoadMasterGridAsync();

        [RelayCommand]
        private async Task RefreshDatabaseAsync()
        {
            MasterSearchText = string.Empty;
            await LoadMasterGridAsync();
        }

        [RelayCommand]
        private void Clear()
        {
            CurrentItem = new ItemParent();
            ItemPrefix = string.Empty;
            ItemSuffix = string.Empty;
            IsCodeReadOnly = false;

            DynamicProperties.Clear();
            GeneratedVariants.Clear();
            SelectedVariantSuppliers.Clear();
            SelectedVariantForSupplierEdit = null;

            SelectedCategory = null;
            SelectedSubCategory = null;
            SelectedUom = string.Empty;
            SelectedTaxCode = string.Empty;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedDatabaseItem == null) return;

            var result = MessageBox.Show($"Permanently delete Matrix Item '{SelectedDatabaseItem.ItemName}' and all its Variants?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _itemMasterRepository.DeleteMatrixAsync(SelectedDatabaseItem.ParentId);
                    await LoadMasterGridAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    MessageBox.Show("Cannot delete this item as it contains sales or purchase history.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}