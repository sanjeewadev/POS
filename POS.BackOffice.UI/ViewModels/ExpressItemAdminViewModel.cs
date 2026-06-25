using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ExpressItemAdminViewModel : ObservableObject
    {
        private readonly ExpressItemRepository _repository;

        // ==========================================
        // PROPERTIES: LEFT PANE (VARIANT SEARCH)
        // ==========================================
        public ObservableCollection<ItemVariant> SearchResults { get; } = new();

        [ObservableProperty] private string _searchText = string.Empty;

        [ObservableProperty] private ItemVariant? _selectedVariant;

        // ==========================================
        // PROPERTIES: RIGHT PANE (LAYOUT SETUP)
        // ==========================================
        public ObservableCollection<ExpressItemLayout> Layouts { get; } = new();

        [ObservableProperty] private ExpressItemLayout _currentLayout = new ExpressItemLayout();

        [ObservableProperty] private ExpressItemLayout? _selectedLayout;

        // Color Palette choices for the manager to design their POS screens
        public ObservableCollection<string> AvailableColors { get; } = new(new[]
        {
            "#005555", "#8B0000", "#D97706", "#059669", "#1E3A8A",
            "#4C1D95", "#B91C1C", "#333333", "#0F766E", "#BE123C"
        });

        public ExpressItemAdminViewModel(ExpressItemRepository repository)
        {
            _repository = repository;
            _ = LoadLayoutsAsync();
        }

        // ==========================================
        // SMART UI TRIGGERS
        // ==========================================

        // Fires automatically when a manager clicks a searched item
        partial void OnSelectedVariantChanged(ItemVariant? value)
        {
            if (value != null && string.IsNullOrWhiteSpace(CurrentLayout.DisplayLabel))
            {
                // Auto-generate the button label based on Parent Name + Variant Description
                string autoName = string.IsNullOrWhiteSpace(value.VariantDescription)
                    ? value.ItemParent?.ItemName ?? ""
                    : $"{value.ItemParent?.ItemName} {value.VariantDescription}";

                // Truncate to exactly 20 characters so it fits nicely on a square POS touch button
                CurrentLayout.DisplayLabel = autoName.Length > 20 ? autoName.Substring(0, 20).Trim() : autoName;

                // Force UI to refresh the form to show the newly generated name
                OnPropertyChanged(nameof(CurrentLayout));
            }
        }

        // Fires automatically when a manager selects an existing layout to edit
        partial void OnSelectedLayoutChanged(ExpressItemLayout? value)
        {
            if (value != null)
            {
                // We CLONE the object. This prevents live UI edits from corrupting 
                // the DataGrid display until the user actually hits "Save".
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
                    ItemVariant = value.ItemVariant
                };
            }
        }

        // ==========================================
        // COMMANDS & EXECUTION
        // ==========================================

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            var data = await _repository.SearchVariantsAsync(SearchText);
            SearchResults.Clear();
            foreach (var item in data) SearchResults.Add(item);
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Basic Validation
            if (CurrentLayout.Id == 0 && SelectedVariant == null)
            {
                MessageBox.Show("Please search and select an Item Variant from the left pane first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentLayout.TabCategory) || string.IsNullOrWhiteSpace(CurrentLayout.DisplayLabel))
            {
                MessageBox.Show("Tab Category and Button Display Label are strictly required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Assign variant ID if it's a brand new button layout
                if (CurrentLayout.Id == 0)
                {
                    CurrentLayout.ItemVariantId = SelectedVariant!.Id;
                }

                await _repository.SaveLayoutAsync(CurrentLayout);
                MessageBox.Show("Express POS button successfully mapped and saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadLayoutsAsync();
                Clear();
            }
            catch (InvalidOperationException ex)
            {
                // Catch the strict Grid Collision rule we built into the repository!
                MessageBox.Show(ex.Message, "Grid Collision Detected", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save layout: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedLayout == null || SelectedLayout.Id == 0) return;

            if (MessageBox.Show($"Are you sure you want to completely remove the '{SelectedLayout.DisplayLabel}' button from the POS?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _repository.DeleteLayoutAsync(SelectedLayout.Id);
                await LoadLayoutsAsync();
                Clear();
            }
        }

        [RelayCommand]
        private void Clear()
        {
            CurrentLayout = new ExpressItemLayout();
            SelectedLayout = null;
            SelectedVariant = null;
        }

        private async Task LoadLayoutsAsync()
        {
            var data = await _repository.GetAllLayoutsAsync();
            Layouts.Clear();
            foreach (var item in data) Layouts.Add(item);
        }
    }
}