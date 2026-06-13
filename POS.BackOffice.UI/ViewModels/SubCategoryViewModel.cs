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
    public partial class SubCategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;

        // --- Form Fields ---
        [ObservableProperty]
        private Category? _selectedParentCategory;

        [ObservableProperty]
        private string _subCategoryCode = string.Empty;

        [ObservableProperty]
        private string _subCategoryName = string.Empty;

        [ObservableProperty]
        private bool _isDeactivated = false;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        // --- Filter Fields ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedFilterCategory;

        // --- Data Collections ---
        public ObservableCollection<Category> Categories { get; set; } = new();
        public ObservableCollection<SubCategory> SubCategories { get; set; } = new();

        public SubCategoryViewModel(CategoryRepository categoryRepository, SubCategoryRepository subCategoryRepository)
        {
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadSubCategoriesAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                Categories.Clear();
                // Only load categories that are NOT deactivated for the dropdowns
                var activeCategories = (await _categoryRepository.GetAllAsync()).Where(c => !c.IsDeactivated);

                // Add a "Clear Filter" option for the right-side filter dropdown
                Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --", CategoryCode = "ALL" });

                foreach (var cat in activeCategories)
                {
                    Categories.Add(cat);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load parent categories: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadSubCategoriesAsync()
        {
            try
            {
                SubCategories.Clear();

                // 1. Determine if a specific parent category is selected for filtering
                int? parentFilterId = (SelectedFilterCategory != null && SelectedFilterCategory.Id != 0)
                                      ? SelectedFilterCategory.Id
                                      : null;

                // 2. Fetch data using the blazing-fast SQL repository method we just built
                var data = await _subCategoryRepository.GetAllFilteredAsync(parentFilterId, SearchText);

                foreach (var item in data)
                {
                    SubCategories.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load sub-categories: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadSubCategoriesAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Basic Validation
            if (SelectedParentCategory == null || SelectedParentCategory.Id == 0)
            {
                MessageBox.Show("Please select a valid Parent Category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SubCategoryCode) || string.IsNullOrWhiteSpace(SubCategoryName))
            {
                MessageBox.Show("Sub-Category Code and Name are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Duplicate Check Validation
                int currentId = SelectedSubCategory?.Id ?? 0;
                bool isUnique = await _subCategoryRepository.IsCodeUniqueAsync(SubCategoryCode, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The Sub-Category Code '{SubCategoryCode}' is already in use.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Save or Update
                if (SelectedSubCategory == null)
                {
                    var newSub = new SubCategory
                    {
                        CategoryId = SelectedParentCategory.Id,
                        SubCategoryCode = this.SubCategoryCode.Trim().ToUpper(),
                        SubCategoryName = this.SubCategoryName.Trim(),
                        IsDeactivated = this.IsDeactivated,
                        CreatedAt = DateTime.Now
                    };
                    await _subCategoryRepository.AddAsync(newSub);
                }
                else
                {
                    SelectedSubCategory.CategoryId = SelectedParentCategory.Id;
                    SelectedSubCategory.SubCategoryCode = this.SubCategoryCode.Trim().ToUpper();
                    SelectedSubCategory.SubCategoryName = this.SubCategoryName.Trim();
                    SelectedSubCategory.IsDeactivated = this.IsDeactivated;

                    await _subCategoryRepository.UpdateAsync(SelectedSubCategory);
                }

                // 4. Reset UI
                await LoadSubCategoriesAsync();
                Clear();
                MessageBox.Show("Sub-Category saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedParentCategory = null;
            SubCategoryCode = string.Empty;
            SubCategoryName = string.Empty;
            IsDeactivated = false;
            SelectedSubCategory = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedSubCategory == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{SelectedSubCategory.SubCategoryName}'?",
                                         "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _subCategoryRepository.DeleteAsync(SelectedSubCategory.Id);
                    await LoadSubCategoriesAsync();
                    Clear();
                }
                catch (DbUpdateException)
                {
                    // Defensive Architecture: Catching Foreign Key Violations if Items are attached to this Sub-Category
                    MessageBox.Show("This sub-category cannot be deleted because it is currently linked to items in your inventory. \n\nPlease Deactivate it instead.",
                                    "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- Auto-Triggers ---

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadSubCategoriesAsync();
        }

        partial void OnSelectedFilterCategoryChanged(Category? value)
        {
            _ = LoadSubCategoriesAsync();
        }

        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (value != null)
            {
                SelectedParentCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
                SubCategoryCode = value.SubCategoryCode ?? string.Empty;
                SubCategoryName = value.SubCategoryName ?? string.Empty;
                IsDeactivated = value.IsDeactivated;
            }
        }
    }
}