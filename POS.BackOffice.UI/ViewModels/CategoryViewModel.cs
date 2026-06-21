using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;

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

        // Added property to control the XAML IsReadOnly binding
        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        public ObservableCollection<Category> Categories { get; set; } = new();

        public CategoryViewModel(CategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                Categories.Clear();
                // Pass the search text directly to the database repository
                var data = await _categoryRepository.GetAllAsync(SearchText);

                foreach (var item in data)
                {
                    Categories.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(CategoryCode) || string.IsNullOrWhiteSpace(CategoryName))
            {
                MessageBox.Show("Category Code and Name are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Duplicate Check Validation
                int currentId = SelectedCategory?.Id ?? 0;
                bool isUnique = await _categoryRepository.IsCodeUniqueAsync(CategoryCode, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The Category Code '{CategoryCode}' is already in use. Please enter a unique code.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Save or Update
                if (SelectedCategory == null)
                {
                    var newCategory = new Category
                    {
                        CategoryCode = this.CategoryCode.Trim().ToUpper(), // Standardize codes to uppercase
                        CategoryName = this.CategoryName.Trim(),
                        IsDeactivated = this.IsDeactivated,
                        CreatedAt = DateTime.Now
                    };
                    await _categoryRepository.AddAsync(newCategory);
                }
                else
                {
                    SelectedCategory.CategoryCode = this.CategoryCode.Trim().ToUpper();
                    SelectedCategory.CategoryName = this.CategoryName.Trim();
                    SelectedCategory.IsDeactivated = this.IsDeactivated;

                    await _categoryRepository.UpdateAsync(SelectedCategory);
                }

                // 4. Reset UI
                await LoadDataAsync();
                Clear();
                MessageBox.Show("Category saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            CategoryCode = string.Empty;
            CategoryName = string.Empty;
            IsDeactivated = false;
            SelectedCategory = null;

            // Unlock the Category Code field for new entries
            IsCodeReadOnly = false;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedCategory == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{SelectedCategory.CategoryName}'?",
                                         "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _categoryRepository.DeleteAsync(SelectedCategory.Id);
                    await LoadDataAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    // Defensive Architecture: Catching Foreign Key Violations
                    MessageBox.Show("This category cannot be deleted because it is currently linked to items in your inventory. \n\nPlease Deactivate it instead.",
                                    "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Real-time search filter (fires when typing)
        partial void OnSearchTextChanged(string value)
        {
            _ = LoadDataAsync();
        }

        // Fills the text boxes when a user clicks a row in the DataGrid
        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                CategoryCode = value.CategoryCode ?? string.Empty;
                CategoryName = value.CategoryName ?? string.Empty;
                IsDeactivated = value.IsDeactivated;

                // Lock the Category Code field for existing entries
                IsCodeReadOnly = true;
            }
            else
            {
                IsCodeReadOnly = false;
            }
        }
    }
}