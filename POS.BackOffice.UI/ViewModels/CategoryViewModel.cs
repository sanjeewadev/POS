using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;

        private static readonly Regex CategoryCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        [ObservableProperty]
        private string _categoryCode = string.Empty;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private bool _isDeactivated = false;

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

        public CategoryViewModel(CategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
            _ = LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                Categories.Clear();

                var data = await _categoryRepository.GetAllAsync(SearchText);

                foreach (var item in data)
                {
                    Categories.Add(item);
                }

                StatusMessage = $"{Categories.Count} category record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load categories.";
                MessageBox.Show(
                    $"Failed to load categories:\n\n{ex.Message}",
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
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string code = NormalizeCode(CategoryCode);
            string name = NormalizeName(CategoryName);

            if (!ValidateInput(code, name))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedCategory?.Id ?? 0;

                bool isCodeUnique = await _categoryRepository.IsCodeUniqueAsync(code, currentId);
                if (!isCodeUnique)
                {
                    MessageBox.Show(
                        $"The Category Code '{code}' is already in use. Please enter a unique code.",
                        "Duplicate Category Code",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool isNameUnique = await _categoryRepository.IsNameUniqueAsync(name, currentId);
                if (!isNameUnique)
                {
                    MessageBox.Show(
                        $"The Category Name '{name}' is already in use. Please enter a unique name.",
                        "Duplicate Category Name",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (SelectedCategory == null)
                {
                    var newCategory = new Category
                    {
                        CategoryCode = code,
                        CategoryName = name,
                        IsDeactivated = IsDeactivated
                    };

                    await _categoryRepository.AddAsync(newCategory);
                    StatusMessage = "Category created successfully.";
                }
                else
                {
                    var updatedCategory = new Category
                    {
                        Id = SelectedCategory.Id,

                        // Category code is intentionally kept stable after creation.
                        // Repository will not update the code.
                        CategoryCode = SelectedCategory.CategoryCode,

                        CategoryName = name,
                        IsDeactivated = IsDeactivated
                    };

                    await _categoryRepository.UpdateAsync(updatedCategory);
                    StatusMessage = "Category updated successfully.";
                }

                await LoadDataAsync();
                Clear();

                MessageBox.Show(
                    "Category saved successfully.",
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
            CategoryCode = string.Empty;
            CategoryName = string.Empty;
            IsDeactivated = false;
            SelectedCategory = null;
            IsCodeReadOnly = false;
            StatusMessage = "Ready for new category.";
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedCategory == null)
                return;

            var result = MessageBox.Show(
                $"Delete category '{SelectedCategory.CategoryName}'?\n\n" +
                "This will only work if the category has no linked sub-categories, items, or attribute assignments.\n\n" +
                "If it is already used, deactivate it instead.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _categoryRepository.DeleteAsync(SelectedCategory.Id);

                await LoadDataAsync();
                Clear();

                StatusMessage = "Category deleted successfully.";
                MessageBox.Show(
                    "Category deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";
                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this category from new item creation, tick 'Deactivate Category' and click SAVE.",
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

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                CategoryCode = value.CategoryCode ?? string.Empty;
                CategoryName = value.CategoryName ?? string.Empty;
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

        partial void OnIsBusyChanged(bool value)
        {
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
            return !IsBusy;
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

        private static bool ValidateInput(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(
                    "Category Code is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (code.Length > 20)
            {
                MessageBox.Show(
                    "Category Code cannot be longer than 20 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!CategoryCodeRegex.IsMatch(code))
            {
                MessageBox.Show(
                    "Category Code can only contain letters, numbers, dash, and underscore.\n\nExample: CAT-001",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "Category Name is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (name.Length > 100)
            {
                MessageBox.Show(
                    "Category Name cannot be longer than 100 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}