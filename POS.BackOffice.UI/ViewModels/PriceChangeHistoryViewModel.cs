using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PriceChangeHistoryViewModel : ObservableObject
    {
        private readonly PriceChangeHistoryRepository _repository;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedPriceLevelFilter = "All";
        [ObservableProperty] private string _selectedChangeSourceFilter = "All";
        [ObservableProperty] private string _selectedChangedByFilter = "All";
        [ObservableProperty] private DateTime? _dateFrom = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime? _dateTo = DateTime.Today;
        [ObservableProperty] private int _selectedRowLimit = 500;
        [ObservableProperty] private PriceChangeOperationSummaryDto? _selectedOperation;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isInitialized;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private int _totalOperations;
        [ObservableProperty] private int _masterOperationCount;
        [ObservableProperty] private int _batchOperationCount;
        [ObservableProperty] private string _latestChangedText = "-";

        public ObservableCollection<string> PriceLevelFilters { get; } = new(new[] { "All", "Master", "Batch" });
        public ObservableCollection<string> ChangeSourceFilters { get; } = new(new[] { "All" });
        public ObservableCollection<string> ChangedByFilters { get; } = new(new[] { "All" });
        public ObservableCollection<int> RowLimitOptions { get; } = new(new[] { 100, 250, 500, 1000, 5000 });
        public ObservableCollection<PriceChangeOperationSummaryDto> Operations { get; } = new();
        public ObservableCollection<PriceChangeDetailDto> OperationDetails { get; } = new();

        public bool HasSelectedOperation => SelectedOperation != null;
        public bool IsHistoryEmpty => !IsBusy && Operations.Count == 0;
        public int TotalRows => TotalOperations;
        public int MasterChangeCount => MasterOperationCount;
        public int BatchChangeCount => BatchOperationCount;

        public PriceChangeHistoryViewModel(PriceChangeHistoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsHistoryEmpty));

        partial void OnSelectedOperationChanged(PriceChangeOperationSummaryDto? value)
        {
            OnPropertyChanged(nameof(HasSelectedOperation));
            OperationDetails.Clear();
            if (value != null)
                _ = LoadSelectedOperationDetailsAsync(value.OperationKey);
        }

        [RelayCommand]
        private async Task InitializeAsync()
        {
            if (IsInitialized)
                return;
            bool filters = await LoadLookupFiltersAsync();
            bool rows = await LoadHistoryAsync();
            IsInitialized = filters && rows;
        }

        [RelayCommand]
        private async Task SearchAsync() => await LoadHistoryAsync();

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLookupFiltersAsync();
            await LoadHistoryAsync();
        }

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
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private void CloseDetails()
        {
            SelectedOperation = null;
            OperationDetails.Clear();
            StatusMessage = "Details closed.";
        }

        private async Task<bool> LoadLookupFiltersAsync()
        {
            try
            {
                var sources = await _repository.GetChangeSourcesAsync();
                var users = await _repository.GetChangedByUsersAsync();

                ChangeSourceFilters.Clear();
                ChangeSourceFilters.Add("All");
                foreach (string source in sources.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
                    ChangeSourceFilters.Add(source);

                ChangedByFilters.Clear();
                ChangedByFilters.Add("All");
                foreach (string user in users.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
                    ChangedByFilters.Add(user);

                if (!ChangeSourceFilters.Contains(SelectedChangeSourceFilter))
                    SelectedChangeSourceFilter = "All";
                if (!ChangedByFilters.Contains(SelectedChangedByFilter))
                    SelectedChangedByFilter = "All";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load price history filters.";
                MessageBox.Show($"Failed to load filter lists:\n\n{ex.Message}", "Price Change History", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private async Task<bool> LoadHistoryAsync()
        {
            if (IsBusy)
                return false;
            if (DateFrom.HasValue && DateTo.HasValue && DateFrom.Value.Date > DateTo.Value.Date)
            {
                MessageBox.Show("Date From cannot be later than Date To.", "Price Change History", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (SelectedRowLimit <= 0)
                SelectedRowLimit = 500;

            IsBusy = true;
            StatusMessage = "Loading grouped price changes...";
            try
            {
                var rows = await _repository.GetOperationsAsync(
                    SearchText,
                    SelectedPriceLevelFilter,
                    SelectedChangeSourceFilter,
                    SelectedChangedByFilter,
                    DateFrom,
                    DateTo,
                    SelectedRowLimit);
                Operations.Clear();
                foreach (PriceChangeOperationSummaryDto row in rows)
                    Operations.Add(row);
                SelectedOperation = null;
                OperationDetails.Clear();
                RefreshSummaryCounters();
                OnPropertyChanged(nameof(IsHistoryEmpty));
                StatusMessage = $"Loaded {TotalOperations} price change operation(s).";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load grouped price changes.";
                MessageBox.Show($"Failed to load price change history:\n\n{ex.Message}", "Price Change History", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedOperationDetailsAsync(string operationKey)
        {
            try
            {
                var details = await _repository.GetOperationDetailsAsync(operationKey);
                if (SelectedOperation?.OperationKey != operationKey)
                    return;
                OperationDetails.Clear();
                foreach (PriceChangeDetailDto detail in details)
                    OperationDetails.Add(detail);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load operation details:\n\n{ex.Message}", "Price Change History", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshSummaryCounters()
        {
            TotalOperations = Operations.Count;
            MasterOperationCount = Operations.Count(row => !row.MasterChangeSummary.Equals("No master change", StringComparison.OrdinalIgnoreCase));
            BatchOperationCount = Operations.Count(row => row.BatchOverrideCount > 0);
            PriceChangeOperationSummaryDto? latest = Operations.OrderByDescending(row => row.ChangedAt).FirstOrDefault();
            LatestChangedText = latest == null ? "-" : $"{latest.ChangedAt:yyyy-MM-dd HH:mm} by {latest.ChangedBy}";
            OnPropertyChanged(nameof(TotalRows));
            OnPropertyChanged(nameof(MasterChangeCount));
            OnPropertyChanged(nameof(BatchChangeCount));
        }
    }
}
