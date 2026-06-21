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
    // Wrapper for ListBox Checkboxes
    public partial class CategorySelectionWrapper : ObservableObject
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isVisible = true; // For the search filter
    }

    public partial class ItemPropertyViewModel : ViewModelBase
    {
        private readonly AttributeRepository _attributeRepository;
        private readonly CategoryRepository _categoryRepository;

        // --- GROUP FIELDS (Left Side) ---
        [ObservableProperty] private string _groupNameInput = string.Empty;
        [ObservableProperty] private bool _isGroupDeactivated = false;
        [ObservableProperty] private string _groupSearchText = string.Empty;
        [ObservableProperty] private string _categorySearchText = string.Empty;
        [ObservableProperty] private AttributeGroup? _selectedAttributeGroup;

        // --- VALUE FIELDS (Right Side) ---
        [ObservableProperty] private bool _isValueManagerEnabled = false;
        [ObservableProperty] private string _valueManagerHeader = string.Empty;
        [ObservableProperty] private string _valueNameInput = string.Empty;
        [ObservableProperty] private bool _isValueDeactivated = false;
        [ObservableProperty] private string _valueSearchText = string.Empty;
        [ObservableProperty] private AttributeValue? _selectedAttributeValue;

        // --- COLLECTIONS ---
        public ObservableCollection<AttributeGroup> AttributeGroups { get; set; } = new();
        public ObservableCollection<AttributeValue> AttributeValues { get; set; } = new();
        public ObservableCollection<CategorySelectionWrapper> Categories { get; set; } = new();

        public ItemPropertyViewModel(AttributeRepository attributeRepository, CategoryRepository categoryRepository)
        {
            _attributeRepository = attributeRepository;
            _categoryRepository = categoryRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadCategoriesForCheckboxesAsync();
            await LoadGroupsAsync();
        }

        // ==========================================
        // CATEGORY CHECKBOX LOGIC
        // ==========================================
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
                    IsSelected = false,
                    IsVisible = true
                });
            }
        }

        partial void OnCategorySearchTextChanged(string value)
        {
            var search = value?.ToLower() ?? string.Empty;
            foreach (var cat in Categories)
            {
                cat.IsVisible = string.IsNullOrWhiteSpace(search) || cat.CategoryName.ToLower().Contains(search);
            }
        }

        // ==========================================
        // GROUP MANAGEMENT (Left Side)
        // ==========================================
        private async Task LoadGroupsAsync()
        {
            try
            {
                AttributeGroups.Clear();
                var groups = await _attributeRepository.GetAllGroupsAsync(GroupSearchText);
                foreach (var g in groups) { AttributeGroups.Add(g); }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnGroupSearchTextChanged(string value) => _ = LoadGroupsAsync();

        partial void OnSelectedAttributeGroupChanged(AttributeGroup? value)
        {
            if (value != null)
            {
                // Populate Group Form
                GroupNameInput = value.GroupName ?? string.Empty;
                IsGroupDeactivated = value.IsDeactivated;

                // Enable Right Side
                IsValueManagerEnabled = true;
                ValueManagerHeader = $"Adding values to: {value.GroupName}";

                // Sync Checkboxes
                _ = SyncCheckboxesToSelectedGroupAsync(value.Id);

                // Load Values for this specific group
                _ = LoadValuesAsync();
            }
            else
            {
                IsValueManagerEnabled = false;
                ValueManagerHeader = "Please select a Group from the left to add values.";
                AttributeValues.Clear();
                foreach (var cat in Categories) { cat.IsSelected = false; }
            }
        }

        private async Task SyncCheckboxesToSelectedGroupAsync(int groupId)
        {
            foreach (var cat in Categories) { cat.IsSelected = false; }
            var assignedIds = await _attributeRepository.GetAssignedCategoryIdsForGroupAsync(groupId);
            foreach (var cat in Categories)
            {
                if (assignedIds.Contains(cat.CategoryId)) { cat.IsSelected = true; }
            }
        }

        [RelayCommand]
        private async Task SaveGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(GroupNameInput))
            {
                MessageBox.Show("Group Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int currentId = SelectedAttributeGroup?.Id ?? 0;
                bool isUnique = await _attributeRepository.IsGroupUniqueAsync(GroupNameInput, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The Group '{GroupNameInput}' already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AttributeGroup savedGroup;

                if (SelectedAttributeGroup == null)
                {
                    savedGroup = new AttributeGroup
                    {
                        GroupName = GroupNameInput.Trim(),
                        IsDeactivated = IsGroupDeactivated
                    };
                    await _attributeRepository.AddGroupAsync(savedGroup);
                }
                else
                {
                    SelectedAttributeGroup.GroupName = GroupNameInput.Trim();
                    SelectedAttributeGroup.IsDeactivated = IsGroupDeactivated;
                    await _attributeRepository.UpdateGroupAsync(SelectedAttributeGroup);
                    savedGroup = SelectedAttributeGroup;
                }

                // Sync the Checkboxes to the Group
                var checkedCategoryIds = Categories.Where(c => c.IsSelected).Select(c => c.CategoryId).ToList();
                await _attributeRepository.SyncGroupToCategoriesAsync(savedGroup.Id, checkedCategoryIds);

                await LoadGroupsAsync();
                ClearGroup();
                MessageBox.Show("Group saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearGroup()
        {
            SelectedAttributeGroup = null;
            GroupNameInput = string.Empty;
            IsGroupDeactivated = false;
            foreach (var cat in Categories) { cat.IsSelected = false; }
        }

        [RelayCommand]
        private async Task DeleteGroupAsync()
        {
            if (SelectedAttributeGroup == null) return;

            var result = MessageBox.Show($"Delete the group '{SelectedAttributeGroup.GroupName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _attributeRepository.DeleteGroupAsync(SelectedAttributeGroup.Id);
                    await LoadGroupsAsync();
                    ClearGroup();
                }
                catch (DbUpdateException)
                {
                    MessageBox.Show("Cannot delete this group because items in your inventory currently use its values. Please Deactivate it instead.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==========================================
        // VALUE MANAGEMENT (Right Side)
        // ==========================================
        private async Task LoadValuesAsync()
        {
            if (SelectedAttributeGroup == null) return;

            try
            {
                AttributeValues.Clear();
                var data = await _attributeRepository.GetAllValuesFilteredAsync(SelectedAttributeGroup.Id, ValueSearchText);
                foreach (var item in data) { AttributeValues.Add(item); }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load values: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SearchValuesAsync() => await LoadValuesAsync();

        partial void OnSelectedAttributeValueChanged(AttributeValue? value)
        {
            if (value != null)
            {
                ValueNameInput = value.ValueName ?? string.Empty;
                IsValueDeactivated = value.IsDeactivated;
            }
        }

        [RelayCommand]
        private async Task SaveValueAsync()
        {
            if (SelectedAttributeGroup == null) return;

            if (string.IsNullOrWhiteSpace(ValueNameInput))
            {
                MessageBox.Show("Value Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int currentId = SelectedAttributeValue?.Id ?? 0;
                bool isUnique = await _attributeRepository.IsValueUniqueAsync(ValueNameInput, SelectedAttributeGroup.Id, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The value '{ValueNameInput}' already exists in this group.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedAttributeValue == null)
                {
                    var newValue = new AttributeValue
                    {
                        AttributeGroupId = SelectedAttributeGroup.Id,
                        ValueName = ValueNameInput.Trim(),
                        IsDeactivated = IsValueDeactivated
                    };
                    await _attributeRepository.AddValueAsync(newValue);
                }
                else
                {
                    SelectedAttributeValue.ValueName = ValueNameInput.Trim();
                    SelectedAttributeValue.IsDeactivated = IsValueDeactivated;
                    await _attributeRepository.UpdateValueAsync(SelectedAttributeValue);
                }

                await LoadValuesAsync();
                ClearValue();
                // Notice we do NOT clear the SelectedAttributeGroup here, allowing for rapid-fire data entry!
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearValue()
        {
            SelectedAttributeValue = null;
            ValueNameInput = string.Empty;
            IsValueDeactivated = false;
        }

        [RelayCommand]
        private async Task DeleteValueAsync()
        {
            if (SelectedAttributeValue == null) return;

            var result = MessageBox.Show($"Delete the attribute value '{SelectedAttributeValue.ValueName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _attributeRepository.DeleteValueAsync(SelectedAttributeValue.Id);
                    await LoadValuesAsync();
                    ClearValue();
                }
                catch (DbUpdateException)
                {
                    MessageBox.Show("Cannot delete this value because items in your inventory currently use it. Please Deactivate it instead.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}