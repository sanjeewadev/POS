using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public class ExpressItemAdminViewModel : ViewModelBase
    {
        private readonly ExpressItemRepository _repository;

        // ==========================================
        // PROPERTIES: LEFT PANE (VARIANT SEARCH)
        // ==========================================
        public ObservableCollection<ItemVariant> SearchResults { get; set; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(nameof(SearchText)); }
        }

        private ItemVariant? _selectedVariant;
        public ItemVariant? SelectedVariant
        {
            get => _selectedVariant;
            set
            {
                _selectedVariant = value;
                OnPropertyChanged(nameof(SelectedVariant));

                // SMART FEATURE: Auto-fill the Display Label if a new variant is picked 
                // and the label is currently empty
                if (value != null && string.IsNullOrWhiteSpace(CurrentLayout.DisplayLabel))
                {
                    string autoName = string.IsNullOrWhiteSpace(value.VariantDescription)
                        ? value.ItemParent?.ItemName ?? ""
                        : $"{value.ItemParent?.ItemName} {value.VariantDescription}";

                    // Truncate to 20 characters so it fits nicely on a square POS button
                    CurrentLayout.DisplayLabel = autoName.Length > 20 ? autoName.Substring(0, 20) : autoName;
                }
            }
        }

        // ==========================================
        // PROPERTIES: RIGHT PANE (LAYOUT SETUP)
        // ==========================================
        public ObservableCollection<ExpressItemLayout> Layouts { get; set; } = new();

        private ExpressItemLayout _currentLayout = new ExpressItemLayout();
        public ExpressItemLayout CurrentLayout
        {
            get => _currentLayout;
            set { _currentLayout = value; OnPropertyChanged(nameof(CurrentLayout)); }
        }

        private ExpressItemLayout? _selectedLayout;
        public ExpressItemLayout? SelectedLayout
        {
            get => _selectedLayout;
            set
            {
                _selectedLayout = value;
                OnPropertyChanged(nameof(SelectedLayout));

                if (value != null)
                {
                    // Copy to CurrentLayout so editing doesn't instantly affect the grid until saved
                    CurrentLayout = new ExpressItemLayout
                    {
                        Id = value.Id,
                        ItemVariantId = value.ItemVariantId,
                        TabCategory = value.TabCategory,
                        DisplayLabel = value.DisplayLabel,
                        ButtonColorHex = value.ButtonColorHex,
                        TextColorHex = value.TextColorHex,
                        GridRow = value.GridRow,
                        GridColumn = value.GridColumn,
                        IsActive = value.IsActive,
                        ItemVariant = value.ItemVariant // Keep the reference for the UI display
                    };
                }
            }
        }

        // Color Palette choices for the manager to pick from
        public ObservableCollection<string> AvailableColors { get; } = new()
        {
            "#005555", "#8B0000", "#D97706", "#059669", "#1E3A8A",
            "#4C1D95", "#B91C1C", "#333333", "#0F766E", "#BE123C"
        };

        // ==========================================
        // COMMANDS
        // ==========================================
        public ICommand SearchCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public ExpressItemAdminViewModel(ExpressItemRepository repository)
        {
            _repository = repository;

            SearchCommand = new RelayCommand(async (o) => await ExecuteSearch());
            SaveCommand = new RelayCommand(async (o) => await ExecuteSave());
            DeleteCommand = new RelayCommand(async (o) => await ExecuteDelete());
            ClearCommand = new RelayCommand((o) => ExecuteClear());

            _ = LoadLayoutsAsync();
        }

        // ==========================================
        // METHODS
        // ==========================================
        private async Task LoadLayoutsAsync()
        {
            var data = await _repository.GetAllLayoutsAsync();
            Layouts.Clear();
            foreach (var item in data) Layouts.Add(item);
        }

        private async Task ExecuteSearch()
        {
            var data = await _repository.SearchVariantsAsync(SearchText);
            SearchResults.Clear();
            foreach (var item in data) SearchResults.Add(item);
        }

        private async Task ExecuteSave()
        {
            // Validation
            if (CurrentLayout.Id == 0 && SelectedVariant == null)
            {
                MessageBox.Show("Please search and select an Item Variant first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(CurrentLayout.DisplayLabel) || string.IsNullOrWhiteSpace(CurrentLayout.TabCategory))
            {
                MessageBox.Show("Tab Category and Display Label are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // If it's a new layout, attach the variant ID from the search results
                if (CurrentLayout.Id == 0)
                {
                    CurrentLayout.ItemVariantId = SelectedVariant!.Id;
                }

                await _repository.SaveLayoutAsync(CurrentLayout);
                MessageBox.Show("Express button layout saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadLayoutsAsync();
                ExecuteClear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving layout: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteDelete()
        {
            if (SelectedLayout == null || SelectedLayout.Id == 0) return;

            if (MessageBox.Show($"Delete button mapping for '{SelectedLayout.DisplayLabel}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _repository.DeleteLayoutAsync(SelectedLayout.Id);
                await LoadLayoutsAsync();
                ExecuteClear();
            }
        }

        private void ExecuteClear()
        {
            // Reset to defaults
            CurrentLayout = new ExpressItemLayout();
            SelectedLayout = null;
            SelectedVariant = null;
        }
    }
}