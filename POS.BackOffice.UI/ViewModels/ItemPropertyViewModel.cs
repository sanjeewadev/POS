using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ItemPropertyViewModel : ViewModelBase
    {
        private const int MaxDisplayOrder = 9999;

        private readonly AttributeRepository _attributeRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isApplyingSelection;

        // =========================================================
        // GROUP FIELDS
        // =========================================================

        [ObservableProperty]
        private string _groupNameInput = string.Empty;

        [ObservableProperty]
        private string _groupDisplayOrderText = "0";

        [ObservableProperty]
        private bool _isGroupDeactivated = false;

        [ObservableProperty]
        private string _groupSearchText = string.Empty;

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
        private string _valueDisplayOrderText = "0";

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

        public ItemPropertyViewModel(
            AttributeRepository attributeRepository,
            CategoryRepository categoryRepository,
            IMessageBoxService messageBoxService)
        {
            _attributeRepository = attributeRepository ?? throw new ArgumentNullException(nameof(attributeRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;

            try
            {
                await LoadGroupsInternalAsync();
                _isInitialized = true;
                StatusMessage = "Global item property page loaded.";
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                StatusMessage = "Failed to initialize item property page.";

                _messageBoxService.ShowError(
                    $"Failed to initialize item property page:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
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
                StatusMessage = $"{AttributeGroups.Count} global group record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load groups.";

                _messageBoxService.ShowError(
                    $"Failed to load groups:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadGroupsInternalAsync()
        {
            AttributeGroups.Clear();

            var groups = await _attributeRepository.GetAllGroupsAsync(
                searchTerm: GroupSearchText,
                includeDeactivated: true);

            foreach (var group in groups)
                AttributeGroups.Add(group);
        }

        // Kept for compatibility with old bindings. New XAML uses LoadGroupsCommand.
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchGroupsAsync()
        {
            await LoadGroupsAsync();
        }

        partial void OnGroupSearchTextChanged(string value)
        {
            if (_isInitialized)
                StatusMessage = "Type group search text and click SEARCH / REFRESH.";
        }

        partial void OnSelectedAttributeGroupChanged(AttributeGroup? value)
        {
            if (_isApplyingSelection)
                return;

            _ = ApplySelectedGroupAsync(value);
        }

        private async Task ApplySelectedGroupAsync(AttributeGroup? value)
        {
            IsBusy = true;

            try
            {
                _isApplyingSelection = true;

                if (value != null)
                {
                    GroupNameInput = value.GroupName ?? string.Empty;
                    GroupDisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                    IsGroupDeactivated = value.IsDeactivated;

                    IsValueManagerEnabled = true;
                    ValueManagerHeader = $"Adding values to global group: {value.GroupName}";

                    SelectedAttributeValue = null;
                    ValueNameInput = string.Empty;
                    ValueDisplayOrderText = "0";
                    IsValueDeactivated = false;

                    await LoadValuesInternalAsync();

                    StatusMessage = $"Editing global group: {value.GroupName}";
                }
                else
                {
                    AttributeValues.Clear();
                    SelectedAttributeValue = null;
                    ValueNameInput = string.Empty;
                    ValueDisplayOrderText = "0";
                    IsValueDeactivated = false;

                    IsValueManagerEnabled = false;
                    ValueManagerHeader = "Please select a group from the left to add values.";

                    StatusMessage = "Ready for new global group.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to apply selected group.";

                _messageBoxService.ShowError(
                    $"Failed to load selected group details:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                _isApplyingSelection = false;
                IsBusy = false;

                NotifyCommandStates();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveGroup))]
        private async Task SaveGroupAsync()
        {
            string groupName = NormalizeName(GroupNameInput);

            if (!TryParseDisplayOrder(GroupDisplayOrderText, "Group Display Order", out int displayOrder))
                return;

            if (!ValidateGroupInput(groupName, displayOrder))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedAttributeGroup?.Id ?? 0;

                bool isUnique = await _attributeRepository.IsGroupUniqueAsync(groupName, currentId);

                if (!isUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The group '{groupName}' already exists.",
                        "Duplicate Group");

                    return;
                }

                if (SelectedAttributeGroup == null)
                {
                    var newGroup = new AttributeGroup
                    {
                        GroupName = groupName,
                        DisplayOrder = displayOrder,
                        IsDeactivated = IsGroupDeactivated
                    };

                    await _attributeRepository.AddGroupAsync(newGroup);
                }
                else
                {
                    var updatedGroup = new AttributeGroup
                    {
                        Id = SelectedAttributeGroup.Id,
                        GroupName = groupName,
                        DisplayOrder = displayOrder,
                        IsDeactivated = IsGroupDeactivated
                    };

                    await _attributeRepository.UpdateGroupAsync(updatedGroup);
                }

                await LoadGroupsInternalAsync();
                ResetGroupForm("Global group saved successfully.");

                _messageBoxService.ShowInformation(
                    "Global group saved successfully.",
                    "Success");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Group save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Group save failed.";

                _messageBoxService.ShowError(
                    $"Error saving group:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearGroup()
        {
            ResetGroupForm("Ready for new global group.");
        }

        private void ResetGroupForm(string statusMessage)
        {
            _isApplyingSelection = true;

            SelectedAttributeGroup = null;
            GroupNameInput = string.Empty;
            GroupDisplayOrderText = "0";
            IsGroupDeactivated = false;

            SelectedAttributeValue = null;
            ValueNameInput = string.Empty;
            ValueDisplayOrderText = "0";
            IsValueDeactivated = false;
            AttributeValues.Clear();

            IsValueManagerEnabled = false;
            ValueManagerHeader = "Please select a group from the left to add values.";

            _isApplyingSelection = false;

            StatusMessage = statusMessage;
            NotifyCommandStates();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteGroup))]
        private async Task DeleteGroupAsync()
        {
            if (SelectedAttributeGroup == null)
                return;

            var selected = SelectedAttributeGroup;

            IsBusy = true;

            try
            {
                var linkedData = await _attributeRepository.GetGroupLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.GroupName),
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Linked data check failed.";

                _messageBoxService.ShowError(
                    $"Could not check linked records:\n\n{ex.Message}",
                    "Delete Check Error");

                return;
            }
            finally
            {
                IsBusy = false;
            }

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Delete unused group '{selected.GroupName}'?\n\n" +
                "This is only safe for wrongly-created groups with no values and no item usage.\n\n" +
                "For real business records, deactivate the group instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _attributeRepository.DeleteGroupAsync(selected.Id);

                await LoadGroupsInternalAsync();
                ResetGroupForm("Unused group deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Unused group deleted successfully.",
                    "Deleted");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Delete Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"Error deleting group:\n\n{ex.Message}",
                    "Delete Error");
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

                _messageBoxService.ShowError(
                    $"Failed to load values:\n\n{ex.Message}",
                    "Database Error");
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
                groupId: SelectedAttributeGroup.Id,
                searchTerm: ValueSearchText,
                includeDeactivated: true);

            foreach (var value in values)
                AttributeValues.Add(value);
        }

        // Kept for compatibility with old bindings. New XAML uses LoadValuesCommand.
        [RelayCommand(CanExecute = nameof(CanRunValueCommand))]
        private async Task SearchValuesAsync()
        {
            await LoadValuesAsync();
        }

        partial void OnValueSearchTextChanged(string value)
        {
            if (_isInitialized && SelectedAttributeGroup != null)
                StatusMessage = "Type value search text and click SEARCH / REFRESH.";
        }

        partial void OnSelectedAttributeValueChanged(AttributeValue? value)
        {
            if (_isApplyingSelection)
                return;

            if (value != null)
            {
                ValueNameInput = value.ValueName ?? string.Empty;
                ValueDisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                IsValueDeactivated = value.IsDeactivated;

                StatusMessage = $"Editing value: {value.ValueName}";
            }
            else
            {
                ValueNameInput = string.Empty;
                ValueDisplayOrderText = "0";
                IsValueDeactivated = false;
            }

            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSaveValue))]
        private async Task SaveValueAsync()
        {
            if (SelectedAttributeGroup == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select an attribute group before adding values.",
                    "Validation Error");

                return;
            }

            string valueName = NormalizeName(ValueNameInput);

            if (!TryParseDisplayOrder(ValueDisplayOrderText, "Value Display Order", out int displayOrder))
                return;

            if (!ValidateValueInput(valueName, displayOrder))
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
                    _messageBoxService.ShowWarning(
                        $"The value '{valueName}' already exists in this group.",
                        "Duplicate Value");

                    return;
                }

                if (SelectedAttributeValue == null)
                {
                    var newValue = new AttributeValue
                    {
                        AttributeGroupId = SelectedAttributeGroup.Id,
                        ValueName = valueName,
                        DisplayOrder = displayOrder,
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
                        AttributeGroupId = SelectedAttributeValue.AttributeGroupId,
                        ValueName = valueName,
                        DisplayOrder = displayOrder,
                        IsDeactivated = IsValueDeactivated
                    };

                    await _attributeRepository.UpdateValueAsync(updatedValue);
                    StatusMessage = "Value updated successfully.";
                }

                await LoadValuesInternalAsync();
                ResetValueForm(StatusMessage);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Value save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Value save failed.";

                _messageBoxService.ShowError(
                    $"Database Error:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearValue()
        {
            ResetValueForm(
                SelectedAttributeGroup == null
                    ? "Please select a group first."
                    : $"Ready to add values to: {SelectedAttributeGroup.GroupName}");
        }

        private void ResetValueForm(string statusMessage)
        {
            SelectedAttributeValue = null;
            ValueNameInput = string.Empty;
            ValueDisplayOrderText = "0";
            IsValueDeactivated = false;

            StatusMessage = statusMessage;

            SaveValueCommand.NotifyCanExecuteChanged();
            DeleteValueCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteValue))]
        private async Task DeleteValueAsync()
        {
            if (SelectedAttributeValue == null)
                return;

            var selected = SelectedAttributeValue;

            IsBusy = true;

            try
            {
                var linkedData = await _attributeRepository.GetValueLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.ValueName),
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Linked data check failed.";

                _messageBoxService.ShowError(
                    $"Could not check linked records:\n\n{ex.Message}",
                    "Delete Check Error");

                return;
            }
            finally
            {
                IsBusy = false;
            }

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Delete unused value '{selected.ValueName}'?\n\n" +
                "This is only safe for wrongly-created values that are not used by any item variant.\n\n" +
                "For real business records, deactivate the value instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _attributeRepository.DeleteValueAsync(selected.Id);

                await LoadValuesInternalAsync();
                ResetValueForm("Unused value deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Unused value deleted successfully.",
                    "Deleted");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Delete Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"Error deleting value:\n\n{ex.Message}",
                    "Delete Error");
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

        partial void OnGroupDisplayOrderTextChanged(string value)
        {
            SaveGroupCommand.NotifyCanExecuteChanged();
        }

        partial void OnValueNameInputChanged(string value)
        {
            SaveValueCommand.NotifyCanExecuteChanged();
        }

        partial void OnValueDisplayOrderTextChanged(string value)
        {
            SaveValueCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            NotifyCommandStates();
        }

        private void NotifyCommandStates()
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
            return !IsBusy && !string.IsNullOrWhiteSpace(GroupNameInput);
        }

        private bool CanDeleteGroup()
        {
            return !IsBusy && SelectedAttributeGroup != null;
        }

        private bool CanSaveValue()
        {
            return !IsBusy &&
                   SelectedAttributeGroup != null &&
                   IsValueManagerEnabled &&
                   !string.IsNullOrWhiteSpace(ValueNameInput);
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

        private bool TryParseDisplayOrder(string value, string fieldName, out int displayOrder)
        {
            displayOrder = 0;

            string rawValue = (value ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                displayOrder = 0;
                return true;
            }

            bool parsed = int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out displayOrder);

            if (!parsed)
            {
                _messageBoxService.ShowWarning(
                    $"{fieldName} must be a whole number.",
                    "Validation Error");

                return false;
            }

            if (displayOrder < 0)
            {
                _messageBoxService.ShowWarning(
                    $"{fieldName} cannot be negative.",
                    "Validation Error");

                return false;
            }

            if (displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning(
                    $"{fieldName} cannot be greater than {MaxDisplayOrder}.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private bool ValidateGroupInput(string groupName, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                _messageBoxService.ShowWarning("Group Name is required.", "Validation Error");
                return false;
            }

            if (groupName.Length > 50)
            {
                _messageBoxService.ShowWarning("Group Name cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (displayOrder < 0 || displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning($"Group Display Order must be between 0 and {MaxDisplayOrder}.", "Validation Error");
                return false;
            }

            return true;
        }

        private bool ValidateValueInput(string valueName, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(valueName))
            {
                _messageBoxService.ShowWarning("Value Name is required.", "Validation Error");
                return false;
            }

            if (valueName.Length > 50)
            {
                _messageBoxService.ShowWarning("Value Name cannot be longer than 50 characters.", "Validation Error");
                return false;
            }

            if (displayOrder < 0 || displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning($"Value Display Order must be between 0 and {MaxDisplayOrder}.", "Validation Error");
                return false;
            }

            return true;
        }
    }
}
