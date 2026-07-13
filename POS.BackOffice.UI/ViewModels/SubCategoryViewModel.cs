using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
    public partial class SubCategoryViewModel : ViewModelBase
    {
        private const int MaxSubCategoryCodeLength = 40;
        private const int MaxDisplayOrder = 999999;

        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;
        private bool _isApplyingSelection;

        private static readonly Regex SubCategoryCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [ObservableProperty]
        private Category? _selectedParentCategory;

        [ObservableProperty]
        private string _selectedParentCode = string.Empty;

        [ObservableProperty]
        private string _selectedParentName = string.Empty;

        [ObservableProperty]
        private string _parentPrefix = string.Empty;

        [ObservableProperty]
        private string _subCategorySuffix = string.Empty;

        [ObservableProperty]
        private string _subCategoryName = string.Empty;

        [ObservableProperty]
        private string _displayOrderText = "0";

        [ObservableProperty]
        private bool _isDeactivated = false;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private bool _isParentSelectionEnabled = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedFilterCategory;

        [ObservableProperty]
        private bool _includeDeactivated = true;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<Category> ParentCategories { get; } = new();

        public ObservableCollection<Category> FilterCategories { get; } = new();

        public ObservableCollection<Category> Categories { get; } = new();

        public ObservableCollection<SubCategory> SubCategories { get; } = new();

        public SubCategoryViewModel(
            CategoryRepository categoryRepository,
            SubCategoryRepository subCategoryRepository,
            IMessageBoxService messageBoxService)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _subCategoryRepository = subCategoryRepository ?? throw new ArgumentNullException(nameof(subCategoryRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            IsBusy = true;

            try
            {
                await LoadCategoriesAsync();
                await LoadSubCategoriesInternalAsync();

                StatusMessage = "Sub-category page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize sub-category page.";

                _messageBoxService.ShowError(
                    $"Failed to initialize sub-category page:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCategoriesAsync()
        {
            ParentCategories.Clear();
            FilterCategories.Clear();
            Categories.Clear();

            var activeCategories = await _categoryRepository.GetActiveAsync();
            var allCategories = await _categoryRepository.GetAllAsync(includeDeactivated: true);

            FilterCategories.Add(new Category
            {
                Id = 0,
                CategoryCode = "ALL",
                CategoryName = "-- ALL CATEGORIES --"
            });

            foreach (var category in allCategories)
            {
                FilterCategories.Add(category);
            }

            foreach (var category in activeCategories)
            {
                ParentCategories.Add(category);
                Categories.Add(category);
            }

            if (SelectedFilterCategory == null)
            {
                SelectedFilterCategory = FilterCategories.FirstOrDefault();
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadSubCategoriesAsync()
        {
            IsBusy = true;

            try
            {
                await LoadSubCategoriesInternalAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load sub-categories.";

                _messageBoxService.ShowError(
                    $"Failed to load sub-categories:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSubCategoriesInternalAsync()
        {
            SubCategories.Clear();

            int? parentFilterId =
                SelectedFilterCategory != null && SelectedFilterCategory.Id > 0
                    ? SelectedFilterCategory.Id
                    : null;

            var data = await _subCategoryRepository.GetAllFilteredAsync(
                parentCategoryId: parentFilterId,
                searchTerm: SearchText,
                includeDeactivated: IncludeDeactivated);

            foreach (var item in data)
            {
                SubCategories.Add(item);
            }

            StatusMessage = $"{SubCategories.Count} sub-category record(s) loaded.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadSubCategoriesAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedSubCategory == null)
            {
                await CreateSubCategoryAsync();
            }
            else
            {
                await UpdateSubCategoryAsync();
            }
        }

        private async Task CreateSubCategoryAsync()
        {
            if (SelectedParentCategory == null || SelectedParentCategory.Id <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Please select a valid parent category.",
                    "Validation Error");

                return;
            }

            if (SelectedParentCategory.IsDeactivated)
            {
                _messageBoxService.ShowWarning(
                    "Cannot create a sub-category under a deactivated parent category.",
                    "Validation Error");

                return;
            }

            string suffix = NormalizeCode(SubCategorySuffix);
            string code = BuildFinalSubCategoryCode();
            string name = NormalizeName(SubCategoryName);

            if (!TryParseDisplayOrder(DisplayOrderText, out int displayOrder))
                return;

            if (!ValidateInput(code, suffix, name, displayOrder))
                return;

            IsBusy = true;

            try
            {
                bool isCodeUnique = await _subCategoryRepository.IsCodeUniqueAsync(
                    SelectedParentCategory.Id,
                    code,
                    0);

                if (!isCodeUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Sub-Category Code '{code}' already exists under this parent category.",
                        "Duplicate Sub-Category Code");

                    return;
                }

                bool isNameUnique = await _subCategoryRepository.IsNameUniqueAsync(
                    SelectedParentCategory.Id,
                    name,
                    0);

                if (!isNameUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Sub-Category Name '{name}' already exists under this parent category.",
                        "Duplicate Sub-Category Name");

                    return;
                }

                var newSubCategory = new SubCategory
                {
                    CategoryId = SelectedParentCategory.Id,
                    SubCategoryCode = code,
                    SubCategoryName = name,
                    DisplayOrder = displayOrder,
                    IsDeactivated = IsDeactivated
                };

                await _subCategoryRepository.AddAsync(newSubCategory);

                await LoadSubCategoriesInternalAsync();
                ResetForm("Sub-category created successfully.");

                _messageBoxService.ShowInformation(
                    "Sub-category created successfully.",
                    "Success");
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

        private async Task UpdateSubCategoryAsync()
        {
            if (SelectedSubCategory == null)
                return;

            string name = NormalizeName(SubCategoryName);

            if (!TryParseDisplayOrder(DisplayOrderText, out int displayOrder))
                return;

            if (!ValidateNameAndDisplayOrder(name, displayOrder))
                return;

            IsBusy = true;

            try
            {
                bool isNameUnique = await _subCategoryRepository.IsNameUniqueAsync(
                    SelectedSubCategory.CategoryId,
                    name,
                    SelectedSubCategory.Id);

                if (!isNameUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The Sub-Category Name '{name}' already exists under this parent category.",
                        "Duplicate Sub-Category Name");

                    return;
                }

                var updatedSubCategory = new SubCategory
                {
                    Id = SelectedSubCategory.Id,

                    // Parent category and code are intentionally kept stable after creation.
                    CategoryId = SelectedSubCategory.CategoryId,
                    SubCategoryCode = SelectedSubCategory.SubCategoryCode,

                    SubCategoryName = name,
                    DisplayOrder = displayOrder,
                    IsDeactivated = IsDeactivated
                };

                await _subCategoryRepository.UpdateAsync(updatedSubCategory);

                await LoadSubCategoriesInternalAsync();
                ResetForm("Sub-category updated successfully.");

                _messageBoxService.ShowInformation(
                    "Sub-category updated successfully.",
                    "Success");
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
            ResetForm("Ready for new sub-category.");
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedSubCategory == null)
                return;

            var selected = SelectedSubCategory;

            IsBusy = true;

            try
            {
                var linkedData = await _subCategoryRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.SubCategoryName),
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
                $"Delete sub-category '{selected.SubCategoryName}'?\n\n" +
                "This is only safe for wrongly-created or unused test sub-categories.\n\n" +
                "For real business records, deactivate the sub-category instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _subCategoryRepository.DeleteAsync(selected.Id);

                await LoadSubCategoriesInternalAsync();
                ResetForm("Sub-category deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Sub-category deleted successfully.",
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
            _isApplyingSelection = true;

            SelectedParentCategory = null;
            SelectedParentCode = string.Empty;
            SelectedParentName = string.Empty;
            ParentPrefix = string.Empty;
            SubCategorySuffix = string.Empty;
            SubCategoryName = string.Empty;
            DisplayOrderText = "0";
            IsDeactivated = false;
            SelectedSubCategory = null;
            IsCodeReadOnly = false;
            IsParentSelectionEnabled = true;

            _isApplyingSelection = false;

            StatusMessage = statusMessage;

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedParentCategoryChanged(Category? value)
        {
            if (_isApplyingSelection)
                return;

            if (value != null && value.Id > 0)
            {
                SelectedParentCode = value.CategoryCode ?? string.Empty;
                SelectedParentName = value.CategoryName ?? string.Empty;

                if (!IsCodeReadOnly)
                {
                    ParentPrefix = $"{NormalizeCode(SelectedParentCode)}-";
                }
            }
            else
            {
                SelectedParentCode = string.Empty;
                SelectedParentName = string.Empty;

                if (!IsCodeReadOnly)
                {
                    ParentPrefix = string.Empty;
                }
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedFilterCategoryChanged(Category? value)
        {
            if (_isInitialized && !IsBusy)
            {
                _ = LoadSubCategoriesAsync();
            }
        }

        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (_isApplyingSelection)
                return;

            if (value != null)
            {
                _isApplyingSelection = true;

                IsCodeReadOnly = true;
                IsParentSelectionEnabled = false;

                var parent =
                    ParentCategories.FirstOrDefault(c => c.Id == value.CategoryId)
                    ?? FilterCategories.FirstOrDefault(c => c.Id == value.CategoryId)
                    ?? value.Category;

                if (parent != null &&
                    ParentCategories.All(c => c.Id != parent.Id))
                {
                    ParentCategories.Add(parent);
                }

                SelectedParentCategory = parent;
                SelectedParentCode = parent?.CategoryCode ?? string.Empty;
                SelectedParentName = parent?.CategoryName ?? string.Empty;

                string fullCode = value.SubCategoryCode ?? string.Empty;
                string calculatedPrefix = !string.IsNullOrWhiteSpace(SelectedParentCode)
                    ? $"{NormalizeCode(SelectedParentCode)}-"
                    : string.Empty;

                if (!string.IsNullOrEmpty(calculatedPrefix) &&
                    fullCode.StartsWith(calculatedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    ParentPrefix = calculatedPrefix;
                    SubCategorySuffix = fullCode.Substring(calculatedPrefix.Length);
                }
                else
                {
                    ParentPrefix = string.Empty;
                    SubCategorySuffix = fullCode;
                }

                SubCategoryName = value.SubCategoryName ?? string.Empty;
                DisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                IsDeactivated = value.IsDeactivated;

                StatusMessage = $"Editing sub-category: {value.SubCategoryName}";

                _isApplyingSelection = false;
            }
            else
            {
                IsCodeReadOnly = false;
                IsParentSelectionEnabled = true;
            }

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnSubCategorySuffixChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSubCategoryNameChanged(string value)
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
                _ = LoadSubCategoriesAsync();
            }
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadSubCategoriesCommand.NotifyCanExecuteChanged();
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
            if (IsBusy)
                return false;

            if (SelectedSubCategory == null)
            {
                return SelectedParentCategory != null &&
                       !string.IsNullOrWhiteSpace(SubCategorySuffix) &&
                       !string.IsNullOrWhiteSpace(SubCategoryName);
            }

            return !string.IsNullOrWhiteSpace(SubCategoryName);
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedSubCategory != null;
        }

        private string BuildFinalSubCategoryCode()
        {
            string prefix = NormalizeCode(ParentPrefix);
            string suffix = NormalizeCode(SubCategorySuffix);

            return $"{prefix}{suffix}".Trim();
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeName(string name)
        {
            return (name ?? string.Empty).Trim();
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
            string suffix,
            string name,
            int displayOrder)
        {
            if (SelectedParentCategory == null || SelectedParentCategory.Id <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Please select a valid parent category.",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Code suffix is required.",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Code is required.",
                    "Validation Error");

                return false;
            }

            if (code.EndsWith("-", StringComparison.Ordinal))
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Code is incomplete. Enter the code part after the parent prefix.",
                    "Validation Error");

                return false;
            }

            if (code.Length > MaxSubCategoryCodeLength)
            {
                _messageBoxService.ShowWarning(
                    $"Sub-Category Code cannot be longer than {MaxSubCategoryCodeLength} characters.",
                    "Validation Error");

                return false;
            }

            if (!SubCategoryCodeRegex.IsMatch(code))
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Code can only contain letters, numbers, dash, and underscore.\n\nExample: 001-01",
                    "Validation Error");

                return false;
            }

            return ValidateNameAndDisplayOrder(name, displayOrder);
        }

        private bool ValidateNameAndDisplayOrder(string name, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Name is required.",
                    "Validation Error");

                return false;
            }

            if (name.Length > 100)
            {
                _messageBoxService.ShowWarning(
                    "Sub-Category Name cannot be longer than 100 characters.",
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