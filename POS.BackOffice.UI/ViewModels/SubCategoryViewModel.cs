using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SubCategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;

        private bool _isApplyingSelection = false;

        private static readonly Regex SubCategoryCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        // =========================================================
        // FORM FIELDS
        // =========================================================

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
        private bool _isDeactivated = false;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private bool _isParentSelectionEnabled = true;

        // =========================================================
        // FILTER FIELDS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedFilterCategory;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        // =========================================================
        // DATA COLLECTIONS
        // =========================================================

        // Parent selector should use this.
        public ObservableCollection<Category> ParentCategories { get; } = new();

        // Filter dropdown should use this. It includes "-- ALL CATEGORIES --".
        public ObservableCollection<Category> FilterCategories { get; } = new();

        // Kept for backward compatibility with your current XAML.
        // The updated XAML can use ParentCategories and FilterCategories separately.
        public ObservableCollection<Category> Categories { get; } = new();

        public ObservableCollection<SubCategory> SubCategories { get; } = new();

        public SubCategoryViewModel(
            CategoryRepository categoryRepository,
            SubCategoryRepository subCategoryRepository)
        {
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                await LoadCategoriesAsync();
                await LoadSubCategoriesAsync();

                StatusMessage = "Sub-category page loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize sub-category page.";

                MessageBox.Show(
                    $"Failed to initialize sub-category page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            var allCategories = (await _categoryRepository.GetAllAsync()).ToList();

            FilterCategories.Add(new Category
            {
                Id = 0,
                CategoryCode = "ALL",
                CategoryName = "-- ALL CATEGORIES --"
            });

            foreach (var category in allCategories)
            {
                // Filter list should show all categories, including deactivated,
                // because old sub-categories may still belong to deactivated parents.
                FilterCategories.Add(category);

                // Parent selector should normally show only active categories for new creation.
                if (!category.IsDeactivated)
                {
                    ParentCategories.Add(category);

                    // Backward compatibility for current XAML.
                    Categories.Add(category);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadSubCategoriesAsync()
        {
            IsBusy = true;

            try
            {
                SubCategories.Clear();

                int? parentFilterId =
                    SelectedFilterCategory != null && SelectedFilterCategory.Id > 0
                        ? SelectedFilterCategory.Id
                        : null;

                var data = await _subCategoryRepository.GetAllFilteredAsync(parentFilterId, SearchText);

                foreach (var item in data)
                {
                    SubCategories.Add(item);
                }

                StatusMessage = $"{SubCategories.Count} sub-category record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load sub-categories.";

                MessageBox.Show(
                    $"Failed to load sub-categories:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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
                MessageBox.Show(
                    "Please select a valid parent category.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string code = BuildFinalSubCategoryCode();
            string name = NormalizeName(SubCategoryName);

            if (!ValidateInput(code, name))
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
                    MessageBox.Show(
                        $"The Sub-Category Code '{code}' already exists under this parent category.",
                        "Duplicate Sub-Category Code",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool isNameUnique = await _subCategoryRepository.IsNameUniqueAsync(
                    SelectedParentCategory.Id,
                    name,
                    0);

                if (!isNameUnique)
                {
                    MessageBox.Show(
                        $"The Sub-Category Name '{name}' already exists under this parent category.",
                        "Duplicate Sub-Category Name",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var newSubCategory = new SubCategory
                {
                    CategoryId = SelectedParentCategory.Id,
                    SubCategoryCode = code,
                    SubCategoryName = name,
                    IsDeactivated = IsDeactivated
                };

                await _subCategoryRepository.AddAsync(newSubCategory);

                await LoadSubCategoriesAsync();
                Clear();

                StatusMessage = "Sub-category created successfully.";

                MessageBox.Show(
                    "Sub-category saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"An error occurred while saving:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            if (!ValidateNameOnly(name))
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
                    MessageBox.Show(
                        $"The Sub-Category Name '{name}' already exists under this parent category.",
                        "Duplicate Sub-Category Name",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var updatedSubCategory = new SubCategory
                {
                    Id = SelectedSubCategory.Id,

                    // Parent category and code are intentionally kept stable after creation.
                    CategoryId = SelectedSubCategory.CategoryId,
                    SubCategoryCode = SelectedSubCategory.SubCategoryCode,

                    SubCategoryName = name,
                    IsDeactivated = IsDeactivated
                };

                await _subCategoryRepository.UpdateAsync(updatedSubCategory);

                await LoadSubCategoriesAsync();
                Clear();

                StatusMessage = "Sub-category updated successfully.";

                MessageBox.Show(
                    "Sub-category saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"An error occurred while saving:\n\n{ex.Message}",
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
        private void Clear()
        {
            _isApplyingSelection = true;

            SelectedParentCategory = null;
            SelectedParentCode = string.Empty;
            SelectedParentName = string.Empty;
            ParentPrefix = string.Empty;
            SubCategorySuffix = string.Empty;
            SubCategoryName = string.Empty;
            IsDeactivated = false;
            SelectedSubCategory = null;
            IsCodeReadOnly = false;
            IsParentSelectionEnabled = true;

            _isApplyingSelection = false;

            StatusMessage = "Ready for new sub-category.";

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedSubCategory == null)
                return;

            var result = MessageBox.Show(
                $"Delete sub-category '{SelectedSubCategory.SubCategoryName}'?\n\n" +
                "This will only work if the sub-category has no linked item records.\n\n" +
                "If it is already used, deactivate it instead.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _subCategoryRepository.DeleteAsync(SelectedSubCategory.Id);

                await LoadSubCategoriesAsync();
                Clear();

                StatusMessage = "Sub-category deleted successfully.";

                MessageBox.Show(
                    "Sub-category deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this sub-category from new item creation, tick 'Deactivate Sub-Category' and click SAVE.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                MessageBox.Show(
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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
                    ParentPrefix = $"{NormalizeCode(value.CategoryCode)}-";
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
            if (!IsBusy)
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

        partial void OnIsBusyChanged(bool value)
        {
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
            return !IsBusy;
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

        private static bool ValidateInput(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(
                    "Sub-Category Code is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (code.Length > 20)
            {
                MessageBox.Show(
                    "Sub-Category Code cannot be longer than 20 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!SubCategoryCodeRegex.IsMatch(code))
            {
                MessageBox.Show(
                    "Sub-Category Code can only contain letters, numbers, dash, and underscore.\n\nExample: BEV-SOFT",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return ValidateNameOnly(name);
        }

        private static bool ValidateNameOnly(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "Sub-Category Name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (name.Length > 100)
            {
                MessageBox.Show(
                    "Sub-Category Name cannot be longer than 100 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}