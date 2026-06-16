using System;
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
    // -----------------------------------------------------------
    // UI WRAPPER: Required for CheckBox Binding in the ListBox
    // -----------------------------------------------------------
    public partial class CategorySelectionWrapper : ObservableObject
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }

    // -----------------------------------------------------------
    // MAIN VIEW MODEL
    // -----------------------------------------------------------
    public partial class ItemPropertyViewModel : ObservableObject
    {
        private readonly AttributeRepository _attributeRepository;
        private readonly CategoryRepository _categoryRepository;

        // --- Form Fields ---
        [ObservableProperty]
        private AttributeGroup? _selectedAttributeGroup;

        [ObservableProperty]
        private string _valueName = string.Empty;

        [ObservableProperty]
        private bool _isDeactivated = false;

        [ObservableProperty]
        private AttributeValue? _selectedAttributeValue;

        // --- Filter Fields ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private AttributeGroup? _selectedFilterGroup;

        // --- Collections ---
        public ObservableCollection<AttributeGroup> AttributeGroups { get; set; } = new();
        public ObservableCollection<AttributeValue> AttributeValues { get; set; } = new();

        // The wrapped list for the UI CheckBoxes
        public ObservableCollection<CategorySelectionWrapper> Categories { get; set; } = new();

        public ItemPropertyViewModel(AttributeRepository attributeRepository, CategoryRepository categoryRepository)
        {
            _attributeRepository = attributeRepository;
            _categoryRepository = categoryRepository;

            // Fire up the data engine when the view loads
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadGroupsAsync();
            await LoadCategoriesForCheckboxesAsync();
            await LoadValuesAsync();
        }

        private async Task LoadGroupsAsync()
        {
            AttributeGroups.Clear();
            var groups = await _attributeRepository.GetAllGroupsAsync();

            // For the filter dropdown
            AttributeGroups.Add(new AttributeGroup { Id = 0, GroupName = "-- ALL GROUPS --" });

            foreach (var g in groups)
            {
                AttributeGroups.Add(g);
            }
        }

        private async Task LoadCategoriesForCheckboxesAsync()
        {
            Categories.Clear();
            var activeCategories = (await _categoryRepository.GetAllAsync()).Where(c => !c.IsDeactivated);

            foreach (var cat in activeCategories)
            {
                Categories.Add(new CategorySelectionWrapper
                {
                    CategoryId = cat.Id,
                    CategoryName = cat.CategoryName,
                    IsSelected = false
                });
            }
        }

        [RelayCommand]
        private async Task LoadValuesAsync()
        {
            AttributeValues.Clear();
            int? filterGroupId = (SelectedFilterGroup != null && SelectedFilterGroup.Id != 0) ? SelectedFilterGroup.Id : null;

            var data = await _attributeRepository.GetAllValuesFilteredAsync(filterGroupId, SearchText);

            foreach (var item in data)
            {
                AttributeValues.Add(item);
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadValuesAsync();
        }

        // ==========================================
        // THE '+ ADD' DIALOG EXECUTION
        // ==========================================
        [RelayCommand]
        private async Task AddGroupAsync()
        {
            // 1. Open the clean popup window we just built
            var dialog = new POS.BackOffice.UI.Dialogs.InputDialogView(
                title: "New Attribute Group",
                prompt: "Enter Group Name (e.g., Color, Size, Storage):"
            );

            // 2. Wait for the user to hit Save or Cancel
            bool? result = dialog.ShowDialog();

            // 3. If they hit Save and the text is valid
            if (result == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                try
                {
                    // Save to SQLite
                    var newGroup = await _attributeRepository.AddGroupAsync(dialog.InputText);

                    // Refresh the ComboBoxes seamlessly
                    await LoadGroupsAsync();

                    // Auto-select the brand new group so the user can start typing values immediately
                    SelectedAttributeGroup = AttributeGroups.FirstOrDefault(g => g.Id == newGroup.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save group: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==========================================
        // MATRIX SAVING ENGINE
        // ==========================================
        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedAttributeGroup == null || SelectedAttributeGroup.Id == 0)
            {
                MessageBox.Show("Please select an Attribute Group.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ValueName))
            {
                MessageBox.Show("Attribute Value is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int currentId = SelectedAttributeValue?.Id ?? 0;
                bool isUnique = await _attributeRepository.IsValueUniqueAsync(ValueName, SelectedAttributeGroup.Id, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The value '{ValueName}' already exists in the '{SelectedAttributeGroup.GroupName}' group.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Save the Value
                if (SelectedAttributeValue == null)
                {
                    var newValue = new AttributeValue
                    {
                        AttributeGroupId = SelectedAttributeGroup.Id,
                        ValueName = this.ValueName.Trim(),
                        IsDeactivated = this.IsDeactivated
                    };
                    await _attributeRepository.AddValueAsync(newValue);
                }
                else
                {
                    SelectedAttributeValue.AttributeGroupId = SelectedAttributeGroup.Id;
                    SelectedAttributeValue.ValueName = this.ValueName.Trim();
                    SelectedAttributeValue.IsDeactivated = this.IsDeactivated;
                    await _attributeRepository.UpdateValueAsync(SelectedAttributeValue);
                }

                // 2. Sync the Many-to-Many Category Checkboxes
                var checkedCategoryIds = Categories.Where(c => c.IsSelected).Select(c => c.CategoryId).ToList();
                await _attributeRepository.SyncGroupToCategoriesAsync(SelectedAttributeGroup.Id, checkedCategoryIds);

                await LoadValuesAsync();
                Clear();
                MessageBox.Show("Attribute saved and categories synchronized successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedAttributeGroup = null;
            ValueName = string.Empty;
            IsDeactivated = false;
            SelectedAttributeValue = null;

            // Untick all checkboxes safely
            foreach (var cat in Categories)
            {
                cat.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedAttributeValue == null) return;

            var result = MessageBox.Show($"Delete the attribute value '{SelectedAttributeValue.ValueName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _attributeRepository.DeleteValueAsync(SelectedAttributeValue.Id);
                    await LoadValuesAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    MessageBox.Show("Cannot delete this value because items in your inventory currently use it. Please Deactivate it instead.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==========================================
        // UI AUTO-TRIGGERS
        // ==========================================

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadValuesAsync();
        }

        partial void OnSelectedFilterGroupChanged(AttributeGroup? value)
        {
            _ = LoadValuesAsync();
        }

        // When a user selects a value in the Grid, populate the form
        partial void OnSelectedAttributeValueChanged(AttributeValue? value)
        {
            if (value != null)
            {
                SelectedAttributeGroup = AttributeGroups.FirstOrDefault(g => g.Id == value.AttributeGroupId);
                ValueName = value.ValueName ?? string.Empty;
                IsDeactivated = value.IsDeactivated;
            }
        }

        // MAGICAL SYNC: When a Group is selected, query the DB and tick the corresponding Category boxes
        partial void OnSelectedAttributeGroupChanged(AttributeGroup? value)
        {
            if (value != null && value.Id != 0)
            {
                _ = SyncCheckboxesToSelectedGroupAsync(value.Id);
            }
        }

        private async Task SyncCheckboxesToSelectedGroupAsync(int groupId)
        {
            // Untick all
            foreach (var cat in Categories) { cat.IsSelected = false; }

            // Get assigned IDs from database
            var assignedIds = await _attributeRepository.GetAssignedCategoryIdsForGroupAsync(groupId);

            // Tick the matching ones
            foreach (var cat in Categories)
            {
                if (assignedIds.Contains(cat.CategoryId))
                {
                    cat.IsSelected = true;
                }
            }
        }
    }
}