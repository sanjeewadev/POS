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
    // Helper wrapper for Zone 2 Builder
    public class MatrixPropertySelection
    {
        public AttributeGroup Group { get; set; } = null!;
        public AttributeValue Value { get; set; } = null!;
        public string DisplayText => $"{Group.GroupName}: {Value.ValueName}";
    }

    public partial class ItemMasterViewModel : ViewModelBase
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly AttributeRepository _attributeRepository;

        // --- ZONE 1: PARENT IDENTITY ---
        [ObservableProperty] private ItemParent _currentItem = new();
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private SubCategory? _selectedSubCategory;
        [ObservableProperty] private string _selectedUom = string.Empty;
        [ObservableProperty] private string _selectedTaxCode = string.Empty;

        // --- ZONE 2: VARIANT BUILDER ---
        [ObservableProperty] private AttributeGroup? _selectedPropertyKey;
        [ObservableProperty] private AttributeValue? _propertyValueInput;
        public ObservableCollection<MatrixPropertySelection> DynamicProperties { get; set; } = new();

        // --- ZONE 3: BULK PRICING ---
        [ObservableProperty] private decimal _bulkCost = 0m;
        [ObservableProperty] private int _bulkReorderLevel = 0;
        [ObservableProperty] private decimal _bulkRetailMarkupPercent = 0m;
        [ObservableProperty] private decimal _bulkRetailPrice = 0m;
        [ObservableProperty] private decimal _bulkWholesaleMarkupPercent = 0m;
        [ObservableProperty] private decimal _bulkWholesalePrice = 0m;
        [ObservableProperty] private decimal _bulkMinimumPrice = 0m;

        [ObservableProperty] private bool _bulkIsScaleItem = false;
        [ObservableProperty] private bool _bulkHasBatchExpiry = false;
        [ObservableProperty] private bool _bulkIsSerialized = false;

        // --- ZONE 4 & 6: GRIDS ---
        public ObservableCollection<ItemVariant> GeneratedVariants { get; set; } = new();
        public ObservableCollection<ItemMasterSummaryDto> Items { get; set; } = new();

        [ObservableProperty] private string _masterSearchText = string.Empty;
        [ObservableProperty] private ItemMasterSummaryDto? _selectedDatabaseItem;

        // --- LOOKUPS ---
        public ObservableCollection<Category> Categories { get; set; } = new();
        public ObservableCollection<SubCategory> SubCategories { get; set; } = new();
        public ObservableCollection<AttributeGroup> PropertyKeys { get; set; } = new();

        // Dummy data for dropdowns (These would come from DB in production)
        public ObservableCollection<string> Uoms { get; set; } = new(new[] { "PCS", "KG", "BOX", "MTR" });
        public ObservableCollection<string> TaxCodes { get; set; } = new(new[] { "TAX-FREE", "VAT-18", "VAT-5" });

        public ItemMasterViewModel(ItemMasterRepository itemMasterRepository, CategoryRepository categoryRepository, AttributeRepository attributeRepository)
        {
            _itemMasterRepository = itemMasterRepository;
            _categoryRepository = categoryRepository;
            _attributeRepository = attributeRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            foreach (var cat in categories) Categories.Add(cat);

            await LoadMasterGridAsync();
        }

        // --- UI TRIGGERS ---
        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value == null) return;

            // 1. Sync Category to Model
            CurrentItem.CategoryId = value.Id;

            // 2. Load Assigned Attribute Groups dynamically
            _ = LoadPropertyKeysForCategoryAsync(value.Id);
        }

        private async Task LoadPropertyKeysForCategoryAsync(int categoryId)
        {
            PropertyKeys.Clear();
            var groups = await _attributeRepository.GetAllGroupsAsync();
            // Simulating assigned groups (Requires the CategoryAttributeGroup mapping from DB)
            foreach (var g in groups) PropertyKeys.Add(g);
        }

        // --- ZONE 2 BUILDER ACTIONS ---
        [RelayCommand]
        private void AddProperty()
        {
            if (SelectedPropertyKey == null || PropertyValueInput == null) return;

            // Prevent duplicating the exact same property in the list
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

        // The Engine: Generates Matrix Combinations (Cartesian Product)
        [RelayCommand]
        private void GenerateVariants()
        {
            if (string.IsNullOrWhiteSpace(CurrentItem.ItemCode))
            {
                MessageBox.Show("Please define the Parent Item Code first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GeneratedVariants.Clear();

            if (!DynamicProperties.Any())
            {
                // Single standard item (No Matrix)
                GeneratedVariants.Add(new ItemVariant { SkuCode = CurrentItem.ItemCode, VariantDescription = "Standard" });
                return;
            }

            // Group the properties (e.g., All Colors together, All Sizes together)
            var groupedProperties = DynamicProperties.GroupBy(p => p.Group.Id).Select(g => g.ToList()).ToList();
            var combinations = GenerateCombinations(groupedProperties);

            foreach (var combo in combinations)
            {
                string skuSuffix = string.Join("-", combo.Select(c => c.Value.ValueName.Substring(0, Math.Min(3, c.Value.ValueName.Length)).ToUpper()));
                string desc = string.Join(" / ", combo.Select(c => c.Value.ValueName));

                GeneratedVariants.Add(new ItemVariant
                {
                    SkuCode = $"{CurrentItem.ItemCode}-{skuSuffix}",
                    VariantDescription = desc,
                    CostPrice = BulkCost,
                    RetailPrice = BulkRetailPrice
                });
            }
        }

        // Recursive Cartesian Product Logic
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

        // --- ZONE 3 BULK PRICING ---
        partial void OnBulkCostChanged(decimal value) => CalculateRetail();
        partial void OnBulkRetailMarkupPercentChanged(decimal value) => CalculateRetail();

        private void CalculateRetail()
        {
            BulkRetailPrice = BulkCost + (BulkCost * (BulkRetailMarkupPercent / 100m));
            BulkWholesalePrice = BulkCost + (BulkCost * (BulkWholesaleMarkupPercent / 100m));
        }

        [RelayCommand]
        private void ApplyBulkDefaults()
        {
            foreach (var variant in GeneratedVariants)
            {
                variant.CostPrice = BulkCost;
                variant.RetailPrice = BulkRetailPrice;
                variant.WholesalePrice = BulkWholesalePrice;
                variant.MinimumPrice = BulkMinimumPrice;
                variant.ReorderLevel = BulkReorderLevel;
            }
        }

        // --- SAVE EXECUTION ---
        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentItem.ItemCode) || string.IsNullOrWhiteSpace(CurrentItem.ItemName))
            {
                MessageBox.Show("Parent Item Code and Name are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                // Finalize Parent properties
                CurrentItem.BaseUom = SelectedUom;
                CurrentItem.TaxCode = SelectedTaxCode;
                CurrentItem.IsScaleItem = BulkIsScaleItem;
                CurrentItem.HasBatchExpiry = BulkHasBatchExpiry;
                CurrentItem.IsSerialized = BulkIsSerialized;

                var mappingsList = new List<ItemPropertyMapping>(); // Placeholder for specific mappings if needed

                await _itemMasterRepository.SaveFullMatrixAsync(CurrentItem, GeneratedVariants.ToList(), mappingsList);

                await LoadMasterGridAsync();
                Clear();
                MessageBox.Show("Matrix Item saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save item: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            DynamicProperties.Clear();
            GeneratedVariants.Clear();
            SelectedCategory = null;
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
                }
                catch (DbUpdateException)
                {
                    MessageBox.Show("Cannot delete this item as it contains sales or purchase history.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}