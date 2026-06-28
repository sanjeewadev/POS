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
    // Wrapper for ListBox category checkboxes.
    public partial class CategorySelectionWrapper : ObservableObject
    {
        public int CategoryId { get; set; }

        public string CategoryCode { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public bool IsDeactivated { get; set; }

        public string DisplayName =>
            IsDeactivated
                ? $"{CategoryCode} - {CategoryName} (Deactivated)"
                : $"{CategoryCode} - {CategoryName}";

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isVisible = true;
    }

    public partial class ItemPropertyViewModel : ViewModelBase
    {
        private readonly AttributeRepository _attributeRepository;
        private readonly CategoryRepository _categoryRepository;

        private bool _isApplyingSelection = false;

        // =========================================================
        // GROUP FIELDS
        // =========================================================

        [ObservableProperty]
        private string _groupNameInput = string.Empty;

        [ObservableProperty]
        private int _groupDisplayOrder = 0;

        [ObservableProperty]
        private bool _isGroupDeactivated = false;

        [ObservableProperty]
        private string _groupSearchText = string.Empty;

        [ObservableProperty]
        private string _categorySearchText = string.Empty;

        [ObservableProperty]
        private AttributeGroup? _selectedAttributeGroup;

        // =========================================================
        // VALUE FIELDS
        // =========================================================

        [ObservableProperty]
        private bool _isValueManagerEnabled = false;

        [ObservableProperty]
        private string _valueManagerHeader = "Please select a group from the left to add values.";

        [ObservableProperty]
        private string _valueNameInput = string.Empty;

        [ObservableProperty]
        private int _valueDisplayOrder = 0;

        [ObservableProperty]
        private bool _isValueDeactivated = false;

        [ObservableProperty]
        private string _valueSearchText = string.Empty;

        [ObservableProperty]
        private AttributeValue? _selectedAttributeValue;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<AttributeGroup> AttributeGroups { get; } = new();

        public ObservableCollection<AttributeValue> AttributeValues { get; } = new();

        public ObservableCollection<CategorySelectionWrapper> Categories { get; } = new();

        public ItemPropertyViewModel(
            AttributeRepository attributeRepository,
            CategoryRepository categoryRepository)
        {
            _attributeRepository = attributeRepository;
            _categoryRepository = categoryRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                await LoadCategoriesInternalAsync();
                await LoadGroupsInternalAsync();

                StatusMessage = "Item property page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize item property page.";

                MessageBox.Show(
                    $"Failed to initialize item property page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // CATEGORY CHECKBOX LOGIC
        // =========================================================

        private async Task LoadCategoriesInternalAsync()
        {
            Categories.Clear();

            var categories = await _categoryRepository.GetAllAsync();

            foreach (var cat in categories.OrderBy(c => c.CategoryName))
            {
                Categories.Add(new CategorySelectionWrapper
                {
                    CategoryId = cat.Id,
                    CategoryCode = cat.CategoryCode ?? string.Empty,
                    CategoryName = cat.CategoryName ?? string.Empty,
                    IsDeactivated = cat.IsDeactivated,
                    IsSelected = false,
                    IsVisible = true
                });
            }
        }

        partial void OnCategorySearchTextChanged(string value)
        {
            string search = (value ?? string.Empty).Trim().ToLower();

            foreach (var cat in Categories)
            {
                cat.IsVisible =
                    string.IsNullOrWhiteSpace(search) ||
                    cat.CategoryCode.ToLower().Contains(search) ||
                    cat.CategoryName.ToLower().Contains(search) ||
                    cat.DisplayName.ToLower().Contains(search);
            }
        }

        // =========================================================
        // GROUP MANAGEMENT
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadGroupsAsync()
        {
            IsBusy = true;

            try
            {
                await LoadGroupsInternalAsync();
                StatusMessage = $"{AttributeGroups.Count} group record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load groups.";

                MessageBox.Show(
                    $"Failed to load groups:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadGroupsInternalAsync()
        {
            AttributeGroups.Clear();

            var groups = await _attributeRepository.GetAllGroupsAsync(GroupSearchText);

            foreach (var group in groups)
            {
                AttributeGroups.Add(group);
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchGroupsAsync()
        {
            await LoadGroupsAsync();
        }

        partial void OnGroupSearchTextChanged(string value)
        {
            // Do not hit the database on every key press.
            // The updated XAML will use Search button or Enter key.
            StatusMessage = "Type search text and click SEARCH.";
        }

        partial void OnSelectedAttributeGroupChanged(AttributeGroup? value)
        {
            if (_isApplyingSelection)
                return;

            _ = ApplySelectedGroupAsync(value);
        }

        private async Task ApplySelectedGroupAsync(AttributeGroup? value)
        {
            try
            {
                if (value != null)
                {
                    _isApplyingSelection = true;

                    GroupNameInput = value.GroupName ?? string.Empty;
                    GroupDisplayOrder = value.DisplayOrder;
                    IsGroupDeactivated = value.IsDeactivated;

                    IsValueManagerEnabled = true;
                    ValueManagerHeader = $"Adding values to: {value.GroupName}";

                    SelectedAttributeValue = null;
                    ValueNameInput = string.Empty;
                    ValueDisplayOrder = 0;
                    IsValueDeactivated = false;

                    foreach (var cat in Categories)
                    {
                        cat.IsSelected = false;
                    }

                    var assignedIds = await _attributeRepository.GetAssignedCategoryIdsForGroupAsync(value.Id);

                    foreach (var cat in Categories)
                    {
                        cat.IsSelected = assignedIds.Contains(cat.CategoryId);
                    }

                    await LoadValuesInternalAsync();

                    StatusMessage = $"Editing group: {value.GroupName}";

                    _isApplyingSelection = false;
                }
                else
                {
                    AttributeValues.Clear();
                    IsValueManagerEnabled = false;
                    ValueManagerHeader = "Please select a group from the left to add values.";

                    foreach (var cat in Categories)
                    {
                        cat.IsSelected = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _isApplyingSelection = false;
                StatusMessage = "Failed to apply selected group.";

                MessageBox.Show(
                    $"Failed to load selected group details:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            SaveGroupCommand.NotifyCanExecuteChanged();
            DeleteGroupCommand.NotifyCanExecuteChanged();
            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSaveGroup))]
        private async Task SaveGroupAsync()
        {
            string groupName = NormalizeName(GroupNameInput);

            if (!ValidateGroupInput(groupName, GroupDisplayOrder))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedAttributeGroup?.Id ?? 0;

                bool isUnique = await _attributeRepository.IsGroupUniqueAsync(groupName, currentId);

                if (!isUnique)
                {
                    MessageBox.Show(
                        $"The group '{groupName}' already exists.",
                        "Duplicate Group",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                AttributeGroup savedGroup;

                if (SelectedAttributeGroup == null)
                {
                    savedGroup = new AttributeGroup
                    {
                        GroupName = groupName,
                        DisplayOrder = GroupDisplayOrder,
                        IsDeactivated = IsGroupDeactivated
                    };

                    savedGroup = await _attributeRepository.AddGroupAsync(savedGroup);
                }
                else
                {
                    savedGroup = new AttributeGroup
                    {
                        Id = SelectedAttributeGroup.Id,
                        GroupName = groupName,
                        DisplayOrder = GroupDisplayOrder,
                        IsDeactivated = IsGroupDeactivated
                    };

                    await _attributeRepository.UpdateGroupAsync(savedGroup);
                }

                var checkedCategoryIds = Categories
                    .Where(c => c.IsSelected)
                    .Select(c => c.CategoryId)
                    .ToList();

                await _attributeRepository.SyncGroupToCategoriesAsync(savedGroup.Id, checkedCategoryIds);

                await LoadGroupsInternalAsync();
                ClearGroup();

                StatusMessage = "Group saved successfully.";

                MessageBox.Show(
                    "Group saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Group save failed.";

                MessageBox.Show(
                    $"Error saving group:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearGroup()
        {
            _isApplyingSelection = true;

            SelectedAttributeGroup = null;
            GroupNameInput = string.Empty;
            GroupDisplayOrder = 0;
            IsGroupDeactivated = false;
            CategorySearchText = string.Empty;

            foreach (var cat in Categories)
            {
                cat.IsSelected = false;
                cat.IsVisible = true;
            }

            SelectedAttributeValue = null;
            ValueNameInput = string.Empty;
            ValueDisplayOrder = 0;
            IsValueDeactivated = false;
            AttributeValues.Clear();

            IsValueManagerEnabled = false;
            ValueManagerHeader = "Please select a group from the left to add values.";

            _isApplyingSelection = false;

            StatusMessage = "Ready for new group.";

            SaveGroupCommand.NotifyCanExecuteChanged();
            DeleteGroupCommand.NotifyCanExecuteChanged();
            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteGroup))]
        private async Task DeleteGroupAsync()
        {
            if (SelectedAttributeGroup == null)
                return;

            var result = MessageBox.Show(
                $"Delete group '{SelectedAttributeGroup.GroupName}'?\n\n" +
                "This only works if the group has no values, no category assignments, and no item variant usage.\n\n" +
                "If it is already used, deactivate it instead.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _attributeRepository.DeleteGroupAsync(SelectedAttributeGroup.Id);

                await LoadGroupsInternalAsync();
                ClearGroup();

                StatusMessage = "Group deleted successfully.";

                MessageBox.Show(
                    "Group deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this group, tick 'Deactivate Group' and click SAVE.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                MessageBox.Show(
                    $"Error deleting group:\n\n{ex.Message}",
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
        // VALUE MANAGEMENT
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunValueCommand))]
        private async Task LoadValuesAsync()
        {
            IsBusy = true;

            try
            {
                await LoadValuesInternalAsync();
                StatusMessage = $"{AttributeValues.Count} value record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load values.";

                MessageBox.Show(
                    $"Failed to load values:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadValuesInternalAsync()
        {
            AttributeValues.Clear();

            if (SelectedAttributeGroup == null)
                return;

            var values = await _attributeRepository.GetAllValuesFilteredAsync(
                SelectedAttributeGroup.Id,
                ValueSearchText);

            foreach (var value in values)
            {
                AttributeValues.Add(value);
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunValueCommand))]
        private async Task SearchValuesAsync()
        {
            await LoadValuesAsync();
        }

        partial void OnValueSearchTextChanged(string value)
        {
            // Do not hit the database on every key press.
            // The updated XAML will use Search button or Enter key.
        }

        partial void OnSelectedAttributeValueChanged(AttributeValue? value)
        {
            if (value != null)
            {
                ValueNameInput = value.ValueName ?? string.Empty;
                ValueDisplayOrder = value.DisplayOrder;
                IsValueDeactivated = value.IsDeactivated;

                StatusMessage = $"Editing value: {value.ValueName}";
            }

            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSaveValue))]
        private async Task SaveValueAsync()
        {
            if (SelectedAttributeGroup == null)
            {
                MessageBox.Show(
                    "Please select an attribute group before adding values.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string valueName = NormalizeName(ValueNameInput);

            if (!ValidateValueInput(valueName, ValueDisplayOrder))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedAttributeValue?.Id ?? 0;

                bool isUnique = await _attributeRepository.IsValueUniqueAsync(
                    valueName,
                    SelectedAttributeGroup.Id,
                    currentId);

                if (!isUnique)
                {
                    MessageBox.Show(
                        $"The value '{valueName}' already exists in this group.",
                        "Duplicate Value",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (SelectedAttributeValue == null)
                {
                    var newValue = new AttributeValue
                    {
                        AttributeGroupId = SelectedAttributeGroup.Id,
                        ValueName = valueName,
                        DisplayOrder = ValueDisplayOrder,
                        IsDeactivated = IsValueDeactivated
                    };

                    await _attributeRepository.AddValueAsync(newValue);
                    StatusMessage = "Value created successfully.";
                }
                else
                {
                    var updatedValue = new AttributeValue
                    {
                        Id = SelectedAttributeValue.Id,

                        // AttributeGroupId is kept stable by the repository.
                        AttributeGroupId = SelectedAttributeValue.AttributeGroupId,

                        ValueName = valueName,
                        DisplayOrder = ValueDisplayOrder,
                        IsDeactivated = IsValueDeactivated
                    };

                    await _attributeRepository.UpdateValueAsync(updatedValue);
                    StatusMessage = "Value updated successfully.";
                }

                await LoadValuesInternalAsync();
                ClearValue();
            }
            catch (Exception ex)
            {
                StatusMessage = "Value save failed.";

                MessageBox.Show(
                    $"Database Error:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearValue()
        {
            SelectedAttributeValue = null;
            ValueNameInput = string.Empty;
            ValueDisplayOrder = 0;
            IsValueDeactivated = false;

            StatusMessage = SelectedAttributeGroup == null
                ? "Please select a group first."
                : $"Ready to add values to: {SelectedAttributeGroup.GroupName}";

            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteValue))]
        private async Task DeleteValueAsync()
        {
            if (SelectedAttributeValue == null)
                return;

            var result = MessageBox.Show(
                $"Delete value '{SelectedAttributeValue.ValueName}'?\n\n" +
                "This only works if the value is not used by item variants.\n\n" +
                "If it is already used, deactivate it instead.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _attributeRepository.DeleteValueAsync(SelectedAttributeValue.Id);

                await LoadValuesInternalAsync();
                ClearValue();

                StatusMessage = "Value deleted successfully.";

                MessageBox.Show(
                    "Value deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this value, tick 'Deactivate Value' and click SAVE.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                MessageBox.Show(
                    $"Error deleting value:\n\n{ex.Message}",
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
        // COMMAND STATE
        // =========================================================

        partial void OnGroupNameInputChanged(string value)
        {
            SaveGroupCommand.NotifyCanExecuteChanged();
        }

        partial void OnGroupDisplayOrderChanged(int value)
        {
            SaveGroupCommand.NotifyCanExecuteChanged();
        }

        partial void OnValueNameInputChanged(string value)
        {
            SaveValueCommand.NotifyCanExecuteChanged();
        }

        partial void OnValueDisplayOrderChanged(int value)
        {
            SaveValueCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            LoadGroupsCommand.NotifyCanExecuteChanged();
            SearchGroupsCommand.NotifyCanExecuteChanged();
            SaveGroupCommand.NotifyCanExecuteChanged();
            DeleteGroupCommand.NotifyCanExecuteChanged();

            LoadValuesCommand.NotifyCanExecuteChanged();
            SearchValuesCommand.NotifyCanExecuteChanged();
            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanRunValueCommand()
        {
            return !IsBusy && SelectedAttributeGroup != null;
        }

        private bool CanSaveGroup()
        {
            return !IsBusy;
        }

        private bool CanDeleteGroup()
        {
            return !IsBusy && SelectedAttributeGroup != null;
        }

        private bool CanSaveValue()
        {
            return !IsBusy && SelectedAttributeGroup != null && IsValueManagerEnabled;
        }

        private bool CanDeleteValue()
        {
            return !IsBusy && SelectedAttributeValue != null;
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool ValidateGroupInput(string groupName, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show(
                    "Group Name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (groupName.Length > 50)
            {
                MessageBox.Show(
                    "Group Name cannot be longer than 50 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (displayOrder < 0)
            {
                MessageBox.Show(
                    "Group Display Order cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static bool ValidateValueInput(string valueName, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(valueName))
            {
                MessageBox.Show(
                    "Value Name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (valueName.Length > 50)
            {
                MessageBox.Show(
                    "Value Name cannot be longer than 50 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (displayOrder < 0)
            {
                MessageBox.Show(
                    "Value Display Order cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}