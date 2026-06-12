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
    public partial class SubCategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;

        // --- Form Fields ---
        [ObservableProperty]
        private Category? _selectedParentCategory; // Powers both Dual-Search Dropdowns simultaneously

        [ObservableProperty]
        private string _subCategoryCode = string.Empty;

        [ObservableProperty]
        private string _subCategoryName = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private SubCategory? _selectedSubCategory;

        // --- Filter Fields ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedFilterCategory; // Powers the right-side filter dropdown

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
            Categories.Clear();
            var activeCategories = (await _categoryRepository.GetAllAsync()).Where(c => c.IsActive);

            // Add a "Clear Filter" option for the right-side filter dropdown
            Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --", CategoryCode = "ALL" });

            foreach (var cat in activeCategories)
            {
                Categories.Add(cat);
            }
        }

        [RelayCommand]
        private async Task LoadSubCategoriesAsync()
        {
            SubCategories.Clear();

            // 1. Apply Database-Level Filtering
            var data = (SelectedFilterCategory != null && SelectedFilterCategory.Id != 0)
                ? await _subCategoryRepository.GetByCategoryIdAsync(SelectedFilterCategory.Id)
                : await _subCategoryRepository.GetAllAsync();

            // 2. Apply UI-Level Text Searching
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                data = data.Where(s =>
                    s.SubCategoryCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.SubCategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in data)
            {
                SubCategories.Add(item);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // Security & Validation
            if (SelectedParentCategory == null || SelectedParentCategory.Id == 0)
            {
                MessageBox.Show("Please select a valid Parent Category.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SubCategoryCode) || string.IsNullOrWhiteSpace(SubCategoryName))
            {
                MessageBox.Show("Sub-Category Code and Name are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (SelectedSubCategory == null)
                {
                    // Create
                    var newSub = new SubCategory
                    {
                        CategoryId = SelectedParentCategory.Id,
                        SubCategoryCode = this.SubCategoryCode,
                        SubCategoryName = this.SubCategoryName,
                        IsActive = this.IsActive
                    };
                    await _subCategoryRepository.AddAsync(newSub);
                }
                else
                {
                    // Update
                    SelectedSubCategory.CategoryId = SelectedParentCategory.Id;
                    SelectedSubCategory.SubCategoryCode = this.SubCategoryCode;
                    SelectedSubCategory.SubCategoryName = this.SubCategoryName;
                    SelectedSubCategory.IsActive = this.IsActive;
                    await _subCategoryRepository.UpdateAsync(SelectedSubCategory);
                }

                await LoadSubCategoriesAsync();
                Clear();
                MessageBox.Show("Sub-Category saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedParentCategory = null;
            SubCategoryCode = string.Empty;
            SubCategoryName = string.Empty;
            IsActive = true;
            SelectedSubCategory = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedSubCategory == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete {SelectedSubCategory.SubCategoryName}?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _subCategoryRepository.DeleteAsync(SelectedSubCategory.Id);
                await LoadSubCategoriesAsync();
                Clear();
            }
        }

        // --- Auto-Triggers ---

        // Triggers instantly when user types in the search bar
        partial void OnSearchTextChanged(string value)
        {
            _ = LoadSubCategoriesAsync();
        }

        // Triggers instantly when user changes the filter dropdown
        partial void OnSelectedFilterCategoryChanged(Category? value)
        {
            _ = LoadSubCategoriesAsync();
        }

        // Triggers when user clicks a row in the DataGrid to edit it
        partial void OnSelectedSubCategoryChanged(SubCategory? value)
        {
            if (value != null)
            {
                SelectedParentCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
                SubCategoryCode = value.SubCategoryCode;
                SubCategoryName = value.SubCategoryName;
                IsActive = value.IsActive;
            }
        }
    }
}