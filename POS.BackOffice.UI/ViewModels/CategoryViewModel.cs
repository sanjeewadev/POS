using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CategoryViewModel : ViewModelBase
    {
        private const int MaxDisplayOrder = 999999;

        private readonly CategoryRepository _categoryRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;

        private static readonly Regex CategoryCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [ObservableProperty]
        private string _categoryCode = string.Empty;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _displayOrderText = "0";

        [ObservableProperty]
        private bool _isDeactivated = false;

        [ObservableProperty]
        private bool _includeDeactivated = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<Category> Categories { get; } = new();

        public CategoryViewModel(
            CategoryRepository categoryRepository,
            IMessageBoxService messageBoxService)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = await TryLoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            await TryLoadDataAsync();
        }

        private async Task<bool> TryLoadDataAsync()
        {
            IsBusy = true;

            try
            {
                Categories.Clear();

                var data = await _categoryRepository.GetAllAsync(
                    searchTerm: SearchText,
                    includeDeactivated: IncludeDeactivated);

                foreach (var item in data)
                {
                    Categories.Add(item);
                }

                StatusMessage = $"{Categories.Count} category record(s) loaded.";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load categories.";

                _messageBoxService.ShowError(
                    $"Failed to load categories:\n\n{ex.Message}",
                    "Database Error");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string code = SelectedCategory == null
                ? NormalizeCode(CategoryCode)
                : NormalizeCode(SelectedCategory.CategoryCode);

            string name = NormalizeName(CategoryName);
            string description = NormalizeDescription(Description);

            if (!TryParseDisplayOrder(DisplayOrderText, out int displayOrder))
                return;

            if (!ValidateInput(code, name, description, displayOrder))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedCategory?.Id ?? 0;

                bool isCodeUnique = await _categoryRepository.IsCodeUniqueAsync(code, currentId);
                if (!isCodeUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Category Code '{code}' is already in use. Please enter a unique code.",
                        "Duplicate Category Code");
                    return;
                }

                bool isNameUnique = await _categoryRepository.IsNameUniqueAsync(name, currentId);
                if (!isNameUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Category Name '{name}' is already in use. Please enter a unique name.",
                        "Duplicate Category Name");
                    return;
                }

                if (SelectedCategory == null)
                {
                    var newCategory = new Category
                    {
                        CategoryCode = code,
                        CategoryName = name,
                        Description = description,
                        DisplayOrder = displayOrder,
                        IsDeactivated = IsDeactivated
                    };

                    await _categoryRepository.AddAsync(newCategory);

                    await LoadDataAsync();
                    ResetForm("Category created successfully.");

                    _messageBoxService.ShowInformation(
                        "Category created successfully.",
                        "Success");
                }
                else
                {
                    var updatedCategory = new Category
                    {
                        Id = SelectedCategory.Id,

                        // Category code is intentionally kept stable after creation.
                        // Repository also protects this rule.
                        CategoryCode = SelectedCategory.CategoryCode,

                        CategoryName = name,
                        Description = description,
                        DisplayOrder = displayOrder,
                        IsDeactivated = IsDeactivated
                    };

                    await _categoryRepository.UpdateAsync(updatedCategory);

                    await LoadDataAsync();
                    ResetForm("Category updated successfully.");

                    _messageBoxService.ShowInformation(
                        "Category updated successfully.",
                        "Success");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                _messageBoxService.ShowError(
                    $"An error occurred while saving:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            ResetForm("Ready for new category.");
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedCategory == null)
                return;

            var selected = SelectedCategory;

            IsBusy = true;

            try
            {
                var linkedData = await _categoryRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.CategoryName),
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
                $"Delete category '{selected.CategoryName}'?\n\n" +
                "This is only safe for wrongly-created or unused test categories.\n\n" +
                "For real business records, deactivate the category instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _categoryRepository.DeleteAsync(selected.Id);

                await LoadDataAsync();
                ResetForm("Category deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Category deleted successfully.",
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
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetForm(string statusMessage)
        {
            CategoryCode = string.Empty;
            CategoryName = string.Empty;
            Description = string.Empty;
            DisplayOrderText = "0";
            IsDeactivated = false;
            SelectedCategory = null;
            IsCodeReadOnly = false;
            StatusMessage = statusMessage;

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                CategoryCode = value.CategoryCode ?? string.Empty;
                CategoryName = value.CategoryName ?? string.Empty;
                Description = value.Description ?? string.Empty;
                DisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                IsDeactivated = value.IsDeactivated;
                IsCodeReadOnly = true;

                StatusMessage = $"Editing category: {value.CategoryName}";
            }
            else
            {
                IsCodeReadOnly = false;
            }

            DeleteCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnCategoryCodeChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnCategoryNameChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnDescriptionChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnDisplayOrderTextChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIncludeDeactivatedChanged(bool value)
        {
            if (_isInitialized && !IsBusy)
            {
                _ = LoadDataAsync();
            }
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy &&
                   !string.IsNullOrWhiteSpace(CategoryCode) &&
                   !string.IsNullOrWhiteSpace(CategoryName);
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedCategory != null;
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeName(string name)
        {
            return (name ?? string.Empty).Trim();
        }

        private static string NormalizeDescription(string description)
        {
            return (description ?? string.Empty).Trim();
        }

        private bool TryParseDisplayOrder(string value, out int displayOrder)
        {
            displayOrder = 0;

            string rawValue = (value ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                displayOrder = 0;
                DisplayOrderText = "0";
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
                    "Display Order must be a whole number.",
                    "Validation Error");

                return false;
            }

            if (displayOrder < 0)
            {
                _messageBoxService.ShowWarning(
                    "Display Order cannot be less than zero.",
                    "Validation Error");

                return false;
            }

            if (displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning(
                    $"Display Order cannot be greater than {MaxDisplayOrder}.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private bool ValidateInput(
            string code,
            string name,
            string description,
            int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                _messageBoxService.ShowWarning(
                    "Category Code is required.",
                    "Validation Error");

                return false;
            }

            if (code.Length > 20)
            {
                _messageBoxService.ShowWarning(
                    "Category Code cannot be longer than 20 characters.",
                    "Validation Error");

                return false;
            }

            if (!CategoryCodeRegex.IsMatch(code))
            {
                _messageBoxService.ShowWarning(
                    "Category Code can only contain letters, numbers, dash, and underscore.\n\nExample: CAT-001",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                _messageBoxService.ShowWarning(
                    "Category Name is required.",
                    "Validation Error");

                return false;
            }

            if (name.Length > 100)
            {
                _messageBoxService.ShowWarning(
                    "Category Name cannot be longer than 100 characters.",
                    "Validation Error");

                return false;
            }

            if (description.Length > 250)
            {
                _messageBoxService.ShowWarning(
                    "Description cannot be longer than 250 characters.",
                    "Validation Error");

                return false;
            }

            if (displayOrder < 0 || displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning(
                    $"Display Order must be between 0 and {MaxDisplayOrder}.",
                    "Validation Error");

                return false;
            }

            return true;
        }
    }
}