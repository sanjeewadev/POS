using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ExpressItemAdminViewModel : ObservableObject
    {
        private readonly ExpressItemRepository _repository;
        private readonly DispatcherTimer _searchDebounceTimer;

        private bool _isLoadingLayoutSelection = false;

        // =========================================================
        // SEARCH / ITEM SELECTION
        // =========================================================

        public ObservableCollection<ExpressItemSearchDto> SearchResults { get; } = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ExpressItemSearchDto? _selectedSearchItem;

        // =========================================================
        // EXPRESS BUTTON SETUP
        // =========================================================

        public ObservableCollection<ExpressItemLayoutDto> Layouts { get; } = new();

        [ObservableProperty]
        private ExpressItemLayoutDto _currentLayout = new();

        [ObservableProperty]
        private ExpressItemLayoutDto? _selectedLayout;

        public ObservableCollection<string> AvailableColors { get; } = new(new[]
        {
            "#005555",
            "#8B0000",
            "#D97706",
            "#059669",
            "#1E3A8A",
            "#4C1D95",
            "#B91C1C",
            "#333333",
            "#0F766E",
            "#BE123C",
            "#2563EB",
            "#7C2D12"
        });

        public ObservableCollection<string> AvailableTextColors { get; } = new(new[]
        {
            "#FFFFFF",
            "#000000",
            "#FFFF00",
            "#FFD700"
        });

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _searchResultCount = 0;

        [ObservableProperty]
        private int _layoutCount = 0;

        [ObservableProperty]
        private int _activeButtonCount = 0;

        public bool IsEditMode => CurrentLayout.LayoutId > 0;

        public ExpressItemAdminViewModel(ExpressItemRepository repository)
        {
            _repository = repository;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };

            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                await SearchAsync();
            };

            _ = InitializeAsync();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        private async Task InitializeAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading express item admin page...";

            try
            {
                await LoadLayoutsAsync();
                await PrepareBlankLayoutAsync();

                StatusMessage = "Express item admin page ready.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load express item admin page.";

                MessageBox.Show(
                    $"Failed to load express item admin page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // SEARCH
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            _searchDebounceTimer.Stop();

            if (string.IsNullOrWhiteSpace(value))
            {
                SearchResults.Clear();
                SearchResultCount = 0;
                return;
            }

            _searchDebounceTimer.Start();
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            string search = (SearchText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(search))
            {
                SearchResults.Clear();
                SearchResultCount = 0;
                StatusMessage = "Search text is empty.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Searching sellable items...";

            try
            {
                SearchResults.Clear();

                var results = await _repository.SearchSellableItemsAsync(search);

                foreach (var item in results)
                    SearchResults.Add(item);

                SearchResultCount = SearchResults.Count;

                StatusMessage = $"Found {SearchResultCount} item(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Item search failed.";

                MessageBox.Show(
                    $"Item search failed:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedSearchItemChanged(ExpressItemSearchDto? value)
        {
            if (value == null)
                return;

            if (_isLoadingLayoutSelection)
                return;

            _ = StartNewButtonFromSelectedItemAsync(value);
        }

        private async Task StartNewButtonFromSelectedItemAsync(ExpressItemSearchDto item)
        {
            try
            {
                var position = await _repository.GetNextAvailablePositionAsync();

                CurrentLayout = ExpressItemLayoutDto.FromSearchItem(
                    item,
                    position.Row,
                    position.Column);

                SelectedLayout = null;

                OnPropertyChanged(nameof(IsEditMode));

                StatusMessage = $"Prepared new express button for {item.ItemCode}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to prepare express button:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // LAYOUT SELECTION
        // =========================================================

        partial void OnSelectedLayoutChanged(ExpressItemLayoutDto? value)
        {
            if (value == null)
                return;

            _isLoadingLayoutSelection = true;

            CurrentLayout = value.Clone();
            SelectedSearchItem = null;

            _isLoadingLayoutSelection = false;

            OnPropertyChanged(nameof(IsEditMode));

            StatusMessage = $"Editing express button: {CurrentLayout.DisplayLabel}.";
        }

        // =========================================================
        // SAVE
        // =========================================================

        [RelayCommand]
        private async Task SaveAsync()
        {
            var errors = CurrentLayout.ValidateForSave();

            if (errors.Any())
            {
                MessageBox.Show(
                    "Cannot save express button because validation failed:\n\n" +
                    string.Join("\n", errors),
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Saving express button...";

            try
            {
                await _repository.SaveLayoutAsync(CurrentLayout);

                MessageBox.Show(
                    "Express item button saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadLayoutsAsync();
                await PrepareBlankLayoutAsync();

                StatusMessage = "Express button saved.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Express button validation failed.";

                MessageBox.Show(
                    ex.Message,
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to save express button.";

                MessageBox.Show(
                    $"Failed to save express button:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // DELETE
        // =========================================================

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedLayout == null || SelectedLayout.LayoutId <= 0)
            {
                MessageBox.Show(
                    "Please select an existing express button to delete.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete express button '{SelectedLayout.DisplayLabel}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = "Deleting express button...";

            try
            {
                await _repository.DeleteLayoutAsync(SelectedLayout.LayoutId);

                await LoadLayoutsAsync();
                await PrepareBlankLayoutAsync();

                StatusMessage = "Express button deleted.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to delete express button.";

                MessageBox.Show(
                    $"Failed to delete express button:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // CLEAR / RELOAD
        // =========================================================

        [RelayCommand]
        private async Task ClearAsync()
        {
            await PrepareBlankLayoutAsync();

            SelectedLayout = null;
            SelectedSearchItem = null;

            StatusMessage = "Form cleared.";
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLayoutsAsync();
            StatusMessage = "Express button list refreshed.";
        }

        private async Task PrepareBlankLayoutAsync()
        {
            var position = await _repository.GetNextAvailablePositionAsync();

            CurrentLayout = new ExpressItemLayoutDto
            {
                ButtonColorHex = "#005555",
                TextColorHex = "#FFFFFF",
                GridRow = position.Row,
                GridColumn = position.Column,
                IsActive = true
            };

            OnPropertyChanged(nameof(IsEditMode));
        }

        private async Task LoadLayoutsAsync()
        {
            Layouts.Clear();

            var rows = await _repository.GetAdminLayoutsAsync();

            foreach (var row in rows)
                Layouts.Add(row);

            LayoutCount = Layouts.Count;
            ActiveButtonCount = Layouts.Count(x => x.IsActive);
        }
    }
}