using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SalesExplorerViewModel : ViewModelBase
    {
        private readonly MasterSalesAnalyticsRepository _repository;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _terminalFilter = string.Empty;
        [ObservableProperty] private string _selectedReturnStatus = "All";
        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 50;
        [ObservableProperty] private int _totalPages = 1;
        [ObservableProperty] private int _totalRecords;
        [ObservableProperty] private bool _canGoPrevious;
        [ObservableProperty] private bool _canGoNext;
        [ObservableProperty] private decimal _summaryNetSales;
        [ObservableProperty] private decimal _summaryReturns;
        [ObservableProperty] private decimal _summaryNetAfterReturns;
        [ObservableProperty] private decimal _summaryGrossProfit;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private SalesExplorerRecordDto? _selectedSale;
        [ObservableProperty] private SaleReceiptDetailsDto? _selectedDetails;

        public ObservableCollection<string> ReturnStatuses { get; } = new()
        {
            "All", "Not Returned", "Partially Returned", "Fully Returned"
        };

        public ObservableCollection<SalesExplorerRecordDto> Sales { get; } = new();
        public bool IsEmpty => !IsBusy && Sales.Count == 0;

        public SalesExplorerViewModel(MasterSalesAnalyticsRepository repository)
        {
            _repository = repository;
            _ = LoadPageAsync();
        }

        partial void OnSelectedSaleChanged(SalesExplorerRecordDto? value)
        {
            _ = LoadDetailsAsync(value?.SaleId);
        }

        [RelayCommand]
        private async Task LoadPageAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Sales Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading completed sales...";
            try
            {
                PagedSalesResult result = await _repository.GetPagedSalesAsync(
                    StartDate,
                    EndDate,
                    SearchText,
                    TerminalFilter,
                    SelectedReturnStatus,
                    CurrentPage,
                    PageSize);

                Sales.Clear();
                foreach (var row in result.Records)
                    Sales.Add(row);

                TotalRecords = result.TotalCount;
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalRecords / PageSize));
                if (CurrentPage > TotalPages)
                {
                    CurrentPage = TotalPages;
                    await LoadPageAsync();
                    return;
                }

                CanGoPrevious = CurrentPage > 1;
                CanGoNext = CurrentPage < TotalPages;
                SummaryNetSales = result.SummaryNetSales;
                SummaryReturns = result.SummaryReturns;
                SummaryNetAfterReturns = result.SummaryNetAfterReturns;
                SummaryGrossProfit = result.SummaryGrossProfit;
                SelectedSale = Sales.Count > 0 ? Sales[0] : null;
                StatusMessage = $"{TotalRecords:N0} completed sale(s). Page {CurrentPage:N0} of {TotalPages:N0}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Sales Explorer could not be loaded.";
                MessageBox.Show(ex.Message, "Sales Explorer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private async Task LoadDetailsAsync(int? saleId)
        {
            if (!saleId.HasValue)
            {
                SelectedDetails = null;
                return;
            }

            try
            {
                SelectedDetails = await _repository.GetSaleReceiptDetailsAsync(saleId.Value);
            }
            catch (Exception ex)
            {
                SelectedDetails = null;
                MessageBox.Show(ex.Message, "Sale Details", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            CurrentPage = 1;
            await LoadPageAsync();
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            TerminalFilter = string.Empty;
            SelectedReturnStatus = "All";
            CurrentPage = 1;
            await LoadPageAsync();
        }

        [RelayCommand]
        private async Task NextPageAsync()
        {
            if (!CanGoNext)
                return;
            CurrentPage++;
            await LoadPageAsync();
        }

        [RelayCommand]
        private async Task PreviousPageAsync()
        {
            if (!CanGoPrevious)
                return;
            CurrentPage--;
            await LoadPageAsync();
        }
    }
}
