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

        // NEW: Split Code Properties
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
                var activeCategories = (await _categoryRepository.GetAllAsync()).Where(c => !c.IsDeactivated);
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

                int? parentFilterId = (SelectedFilterCategory != null && SelectedFilterCategory.Id != 0)
                                      ? SelectedFilterCategory.Id
                                      : null;

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
            if (SelectedParentCategory == null || SelectedParentCategory.Id == 0)
            {
                MessageBox.Show("Please select a valid Parent Category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SubCategorySuffix) || string.IsNullOrWhiteSpace(SubCategoryName))
            {
                MessageBox.Show("Sub-Category Suffix and Name are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // COMBINE PREFIX AND SUFFIX FOR THE DATABASE
                string finalSubCategoryCode = $"{ParentPrefix}{SubCategorySuffix}".Trim().ToUpper();

                int currentId = SelectedSubCategory?.Id ?? 0;
                bool isUnique = await _subCategoryRepository.IsCodeUniqueAsync(finalSubCategoryCode, currentId);

                if (!isUnique)
                {
                    MessageBox.Show($"The Sub-Category Code '{finalSubCategoryCode}' is already in use.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedSubCategory == null)
                {
                    var newSub = new SubCategory
                    {
                        CategoryId = SelectedParentCategory.Id,
                        SubCategoryCode = finalSubCategoryCode,
                        SubCategoryName = this.SubCategoryName.Trim(),
                        IsDeactivated = this.IsDeactivated,
                        CreatedAt = DateTime.Now
                    };
                    await _subCategoryRepository.AddAsync(newSub);
                }
                else
                {
                    SelectedSubCategory.CategoryId = SelectedParentCategory.Id;
                    SelectedSubCategory.SubCategoryCode = finalSubCategoryCode;
                    SelectedSubCategory.SubCategoryName = this.SubCategoryName.Trim();
                    SelectedSubCategory.IsDeactivated = this.IsDeactivated;

                    await _subCategoryRepository.UpdateAsync(SelectedSubCategory);
                }

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
            ParentPrefix = string.Empty;
            SubCategorySuffix = string.Empty;
            SubCategoryName = string.Empty;
            IsDeactivated = false;
            SelectedSubCategory = null;

            IsCodeReadOnly = false;
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
                    MessageBox.Show("This sub-category cannot be deleted because it is currently linked to items in your inventory. \n\nPlease Deactivate it instead.",
                                    "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadSubCategoriesAsync();
        }

        partial void OnSelectedFilterCategoryChanged(Category? value)
        {
            _ = LoadSubCategoriesAsync();
        }

        // Intelligent Part Numbering: Auto-fills Prefix when a parent is selected
        partial void OnSelectedParentCategoryChanged(Category? value)
        {
            if (value != null && value.Id != 0 && !IsCodeReadOnly)
            {
                ParentPrefix = $"{value.CategoryCode}-";
            }
        }

        // Splits the database code back into Prefix and Suffix when a row is clicked
        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (value != null)
            {
                SelectedParentCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);

                string fullCode = value.SubCategoryCode ?? string.Empty;
                string calculatedPrefix = SelectedParentCategory != null ? $"{SelectedParentCategory.CategoryCode}-" : string.Empty;

                // Split the code back out for the UI
                if (!string.IsNullOrEmpty(calculatedPrefix) && fullCode.StartsWith(calculatedPrefix))
                {
                    ParentPrefix = calculatedPrefix;
                    SubCategorySuffix = fullCode.Substring(calculatedPrefix.Length);
                }
                else
                {
                    // Fallback for old mismatched data
                    ParentPrefix = string.Empty;
                    SubCategorySuffix = fullCode;
                }

                SubCategoryName = value.SubCategoryName ?? string.Empty;
                IsDeactivated = value.IsDeactivated;

                IsCodeReadOnly = true;
            }
            else
            {
                IsCodeReadOnly = false;
            }
        }
    }
}