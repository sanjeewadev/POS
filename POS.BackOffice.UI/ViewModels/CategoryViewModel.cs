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
    public partial class CategoryViewModel : ViewModelBase
    {
        private readonly CategoryRepository _categoryRepository;

        [ObservableProperty]
        private string _categoryCode = string.Empty;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedCategory;

        public ObservableCollection<Category> Categories { get; set; } = new();

        public CategoryViewModel(CategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;

            // Fire-and-forget data loading. The UI draws instantly, data fills in a millisecond later.
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            Categories.Clear();
            var data = await _categoryRepository.GetAllAsync();

            // Simple Search Filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                data = data.Where(c =>
                    (c.CategoryName != null && c.CategoryName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)) ||
                    (c.CategoryCode != null && c.CategoryCode.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var item in data)
            {
                Categories.Add(item);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CategoryCode) || string.IsNullOrWhiteSpace(CategoryName))
            {
                MessageBox.Show("Category Code and Name are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedCategory == null)
            {
                // Create New
                var newCategory = new Category
                {
                    CategoryCode = this.CategoryCode,
                    CategoryName = this.CategoryName,
                    IsActive = this.IsActive
                };
                await _categoryRepository.AddAsync(newCategory);
            }
            else
            {
                // Update Existing
                SelectedCategory.CategoryCode = this.CategoryCode;
                SelectedCategory.CategoryName = this.CategoryName;
                SelectedCategory.IsActive = this.IsActive;
                await _categoryRepository.UpdateAsync(SelectedCategory);
            }

            await LoadDataAsync();
            Clear();
            MessageBox.Show("Category saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Clear()
        {
            CategoryCode = string.Empty;
            CategoryName = string.Empty;
            IsActive = true;
            SelectedCategory = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedCategory == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete {SelectedCategory.CategoryName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _categoryRepository.DeleteAsync(SelectedCategory.Id);
                await LoadDataAsync();
                Clear();
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadDataAsync();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                CategoryCode = value.CategoryCode ?? string.Empty;
                CategoryName = value.CategoryName ?? string.Empty;
                IsActive = value.IsActive;
            }
        }
    }
}