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
    public partial class PriceChangeHistoryViewModel : ObservableObject
    {
        private readonly PriceChangeHistoryRepository _repository;

        // =========================================================
        // FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedPriceLevelFilter = "All";

        [ObservableProperty]
        private string _selectedChangeSourceFilter = "All";

        [ObservableProperty]
        private string _selectedChangedByFilter = "All";

        [ObservableProperty]
        private DateTime? _dateFrom = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime? _dateTo = DateTime.Today;

        [ObservableProperty]
        private int _selectedRowLimit = 500;

        public ObservableCollection<string> PriceLevelFilters { get; } = new(new[]
        {
            "All",
            "Master",
            "Batch"
        });

        public ObservableCollection<string> ChangeSourceFilters { get; } = new(new[]
        {
            "All"
        });

        public ObservableCollection<string> ChangedByFilters { get; } = new(new[]
        {
            "All"
        });

        public ObservableCollection<int> RowLimitOptions { get; } = new(new[]
        {
            100,
            250,
            500,
            1000,
            5000
        });

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<PriceChangeHistory> PriceChanges { get; } = new();

        // =========================================================
        // SELECTION
        // =========================================================

        [ObservableProperty]
        private PriceChangeHistory? _selectedPriceChange;

        public bool HasSelectedPriceChange => SelectedPriceChange != null;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _totalRows = 0;

        [ObservableProperty]
        private int _masterChangeCount = 0;

        [ObservableProperty]
        private int _batchChangeCount = 0;

        [ObservableProperty]
        private string _latestChangedText = "-";

        public PriceChangeHistoryViewModel(PriceChangeHistoryRepository repository)
        {
            _repository = repository;
        }

        partial void OnSelectedPriceChangeChanged(PriceChangeHistory? value)
        {
            OnPropertyChanged(nameof(HasSelectedPriceChange));
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        [RelayCommand]
        private async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;

            await LoadLookupFiltersAsync();
            await LoadHistoryAsync();
        }

        private async Task LoadLookupFiltersAsync()
        {
            try
            {
                var sources = await _repository.GetChangeSourcesAsync();
                var users = await _repository.GetChangedByUsersAsync();

                ChangeSourceFilters.Clear();
                ChangeSourceFilters.Add("All");

                foreach (string source in sources.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
                    ChangeSourceFilters.Add(source);

                ChangedByFilters.Clear();
                ChangedByFilters.Add("All");

                foreach (string user in users.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
                    ChangedByFilters.Add(user);

                if (!ChangeSourceFilters.Contains(SelectedChangeSourceFilter))
                    SelectedChangeSourceFilter = "All";

                if (!ChangedByFilters.Contains(SelectedChangedByFilter))
                    SelectedChangedByFilter = "All";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load price history filters.";

                MessageBox.Show(
                    $"Failed to load price history filter lists:\n\n{ex.Message}",
                    "Price History Filter Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // =========================================================
        // LOAD HISTORY
        // =========================================================

        [RelayCommand]
        private async Task SearchAsync()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLookupFiltersAsync();
            await LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            if (IsBusy)
                return;

            if (!ValidateFilters())
                return;

            IsBusy = true;
            StatusMessage = "Loading price change history...";

            try
            {
                var rows = await _repository.GetPriceChangeHistoryAsync(
                    searchText: SearchText,
                    priceLevel: SelectedPriceLevelFilter,
                    changeSource: SelectedChangeSourceFilter,
                    changedBy: SelectedChangedByFilter,
                    dateFrom: DateFrom,
                    dateTo: DateTo,
                    maxRows: SelectedRowLimit);

                PriceChanges.Clear();

                foreach (var row in rows)
                    PriceChanges.Add(row);

                SelectedPriceChange = null;

                RefreshSummaryCounters();

                StatusMessage = $"Loaded {TotalRows} price change record(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load price change history.";

                MessageBox.Show(
                    $"Failed to load price change history:\n\n{ex.Message}",
                    "Price History Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateFilters()
        {
            if (DateFrom.HasValue &&
                DateTo.HasValue &&
                DateFrom.Value.Date > DateTo.Value.Date)
            {
                MessageBox.Show(
                    "Date From cannot be later than Date To.",
                    "Filter Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            if (SelectedRowLimit <= 0)
                SelectedRowLimit = 500;

            return true;
        }

        // =========================================================
        // CLEAR / DETAIL
        // =========================================================

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedPriceLevelFilter = "All";
            SelectedChangeSourceFilter = "All";
            SelectedChangedByFilter = "All";
            DateFrom = DateTime.Today.AddDays(-30);
            DateTo = DateTime.Today;
            SelectedRowLimit = 500;
            SelectedPriceChange = null;

            await LoadHistoryAsync();
        }

        [RelayCommand]
        private void CloseDetails()
        {
            SelectedPriceChange = null;
            StatusMessage = "Details closed.";
        }

        // =========================================================
        // SUMMARY
        // =========================================================

        private void RefreshSummaryCounters()
        {
            TotalRows = PriceChanges.Count;

            MasterChangeCount = PriceChanges.Count(p =>
                p.PriceLevel.Equals("Master", StringComparison.OrdinalIgnoreCase));

            BatchChangeCount = PriceChanges.Count(p =>
                p.PriceLevel.Equals("Batch", StringComparison.OrdinalIgnoreCase));

            var latest = PriceChanges
                .OrderByDescending(p => p.ChangedAt)
                .FirstOrDefault();

            LatestChangedText = latest == null
                ? "-"
                : $"{latest.ChangedAt:yyyy-MM-dd HH:mm} by {latest.ChangedBy}";
        }
    }
}