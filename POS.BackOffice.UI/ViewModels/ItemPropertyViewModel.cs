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
    // Helper wrapper class to power the Category Checkbox List
    public partial class SelectableCategory : ObservableObject
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class ItemPropertyViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly AttributeGroupRepository _attributeGroupRepository;
        private readonly AttributeValueRepository _attributeValueRepository;

        // --- Form Binding Properties ---
        [ObservableProperty]
        private AttributeGroup? _selectedAttributeGroup;

        [ObservableProperty]
        private string _valueName = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private AttributeValue? _selectedAttributeValue;

        // --- Filter Properties ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private AttributeGroup? _selectedFilterGroup;

        // --- UI Collections ---
        public ObservableCollection<AttributeGroup> AttributeGroups { get; set; } = new();
        public ObservableCollection<AttributeValue> AttributeValues { get; set; } = new();
        public ObservableCollection<SelectableCategory> Categories { get; set; } = new();

        public ItemPropertyViewModel(
            CategoryRepository categoryRepository,
            AttributeGroupRepository attributeGroupRepository,
            AttributeValueRepository attributeValueRepository)
        {
            _categoryRepository = categoryRepository;
            _attributeGroupRepository = attributeGroupRepository;
            _attributeValueRepository = attributeValueRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadCategoriesChecklistAsync();
            await LoadAttributeGroupsAsync();
            await LoadAttributeValuesAsync();
        }

        private async Task LoadCategoriesChecklistAsync()
        {
            Categories.Clear();
            var databaseCategories = (await _categoryRepository.GetAllAsync()).Where(c => c.IsActive);
            foreach (var cat in databaseCategories)
            {
                Categories.Add(new SelectableCategory
                {
                    Id = cat.Id,
                    CategoryName = cat.CategoryName,
                    IsSelected = false
                });
            }
        }

        private async Task LoadAttributeGroupsAsync()
        {
            AttributeGroups.Clear();
            var groups = await _attributeGroupRepository.GetGroupsWithCategoriesAsync();

            // Inject a clearing node for the filter panel
            AttributeGroups.Add(new AttributeGroup { Id = 0, GroupName = "-- ALL GROUPS --" });

            foreach (var g in groups)
            {
                AttributeGroups.Add(g);
            }
        }

        [RelayCommand]
        private async Task LoadAttributeValuesAsync()
        {
            AttributeValues.Clear();

            // 1. Database level filtering by Group Context
            var data = (SelectedFilterGroup != null && SelectedFilterGroup.Id != 0)
                ? await _attributeValueRepository.GetValuesByGroupIdAsync(SelectedFilterGroup.Id)
                : await _attributeValueRepository.GetAllAsync();

            // 2. UI evaluation level filtering by text parameter strings
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                data = data.Where(v => v.ValueName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                       v.AttributeGroup.GroupName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var val in data)
            {
                AttributeValues.Add(val);
            }
        }

        [RelayCommand]
        private async Task AddGroupAsync()
        {
            // Simple prompt strategy to handle new category group classifications inline safely
            string groupName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new Attribute Group Name (e.g., RAM, Storage, Fabric):",
                "Create Attribute Group", "");

            if (string.IsNullOrWhiteSpace(groupName)) return;

            var newGroup = new AttributeGroup
            {
                GroupName = groupName.Trim(),
                IsActive = true
            };

            try
            {
                await _attributeGroupRepository.AddAsync(newGroup);
                await LoadAttributeGroupsAsync();
                SelectedAttributeGroup = AttributeGroups.FirstOrDefault(g => g.GroupName == newGroup.GroupName);
                MessageBox.Show($"Group '{groupName}' configured successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save group context parameters: {ex.Message}", "Database Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedAttributeGroup == null || SelectedAttributeGroup.Id == 0)
            {
                MessageBox.Show("Please select or create an Attribute Group parent context.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ValueName))
            {
                MessageBox.Show("Attribute specification entry string is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Extract checkboxes state inside the wrapper entity back into domain objects
            var selectedCategoriesList = Categories.Where(c => c.IsSelected).Select(c => new Category { Id = c.Id, CategoryName = c.CategoryName }).ToList();

            if (!selectedCategoriesList.Any())
            {
                MessageBox.Show("Please assign this attribute profile configuration to at least one category mapping target.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Update Many-to-Many entity targets linked to the Parent Group structure
                SelectedAttributeGroup.Categories = selectedCategoriesList;
                await _attributeGroupRepository.UpdateAsync(SelectedAttributeGroup);

                if (SelectedAttributeValue == null)
                {
                    // Create child parameter structure
                    var newValue = new AttributeValue
                    {
                        AttributeGroupId = SelectedAttributeGroup.Id,
                        ValueName = this.ValueName.Trim(),
                        IsActive = this.IsActive
                    };
                    await _attributeValueRepository.AddAsync(newValue);
                }
                else
                {
                    // Update target parameter context mapping indexes
                    SelectedAttributeValue.AttributeGroupId = SelectedAttributeGroup.Id;
                    SelectedAttributeValue.ValueName = this.ValueName.Trim();
                    SelectedAttributeValue.IsActive = this.IsActive;
                    await _attributeValueRepository.UpdateAsync(SelectedAttributeValue);
                }

                await LoadAttributeValuesAsync();
                await LoadAttributeGroupsAsync(); // Synchronize group context array states
                Clear();
                MessageBox.Show("Operational parameters saved securely.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling transaction boundaries: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedAttributeGroup = null;
            ValueName = string.Empty;
            IsActive = true;
            SelectedAttributeValue = null;

            foreach (var cat in Categories)
            {
                cat.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedAttributeValue == null) return;

            var choice = MessageBox.Show($"Soft-delete '{SelectedAttributeValue.ValueName}' tracking matrix safely from historical configurations?",
                "System Confirmation Alert", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (choice == MessageBoxResult.Yes)
            {
                await _attributeValueRepository.SoftDeleteAsync(SelectedAttributeValue.Id);
                await LoadAttributeValuesAsync();
                Clear();
            }
        }

        // --- Auto-Triggers ---

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadAttributeValuesAsync();
        }

        partial void OnSelectedFilterGroupChanged(AttributeGroup? value)
        {
            _ = LoadAttributeValuesAsync();
        }

        partial void OnSelectedAttributeValueChanged(AttributeValue? value)
        {
            if (value != null)
            {
                // Load parent form properties
                SelectedAttributeGroup = AttributeGroups.FirstOrDefault(g => g.Id == value.AttributeGroupId);
                ValueName = value.ValueName;
                IsActive = value.IsActive;

                // Sync checkbox mapping selections cleanly using tracking maps 
                if (SelectedAttributeGroup != null)
                {
                    foreach (var cat in Categories)
                    {
                        cat.IsSelected = SelectedAttributeGroup.Categories.Any(c => c.Id == cat.Id);
                    }
                }
            }
        }
    }
}