using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SalesExplorerViewModel : ViewModelBase
    {
        private readonly MasterSalesAnalyticsRepository _repository;

        // ==========================================
        // 1. FILTER PROPERTIES
        // ==========================================
        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-7); // Default to last 7 days
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "All";

        public ObservableCollection<string> AvailableStatuses { get; } = new()
        {
            "All", "Completed", "Returned", "Voided", "Suspended"
        };

        // ==========================================
        // 2. PAGINATION STATE
        // ==========================================
        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 50; // Standard ERP grid size
        [ObservableProperty] private int _totalPages = 1;
        [ObservableProperty] private int _totalRecords = 0;

        [ObservableProperty] private bool _canGoPrevious;
        [ObservableProperty] private bool _canGoNext;

        // ==========================================
        // 3. MACRO SUMMARY MATH (Header Cards)
        // ==========================================
        [ObservableProperty] private decimal _summaryTotalRevenue;
        [ObservableProperty] private decimal _summaryTotalProfit;

        // ==========================================
        // 4. COLLECTIONS & DRILL-DOWN STATE
        // ==========================================
        public ObservableCollection<SalesExplorerRecordDto> PagedSales { get; set; } = new();

        [ObservableProperty] private bool _isReceiptModalOpen;
        [ObservableProperty] private SaleReceiptDetailsDto? _selectedReceipt;

        public SalesExplorerViewModel(MasterSalesAnalyticsRepository repository)
        {
            _repository = repository;
            _ = LoadPageAsync(); // Initial load
        }

        // ==========================================
        // 5. THE ENGINE COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task LoadPageAsync()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = await _repository.GetPagedSalesAsync(
                    StartDate,
                    EndDate,
                    SearchText,
                    SelectedStatus,
                    CurrentPage,
                    PageSize);

                // Update Grid
                PagedSales.Clear();
                foreach (var record in result.Records)
                {
                    PagedSales.Add(record);
                }

                // Update Pagination Math
                TotalRecords = result.TotalCount;
                TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);
                if (TotalPages == 0) TotalPages = 1; // Prevent "Page 1 of 0"

                CanGoPrevious = CurrentPage > 1;
                CanGoNext = CurrentPage < TotalPages;

                // Update Macro Summaries
                SummaryTotalRevenue = result.SummaryTotalRevenue;
                SummaryTotalProfit = result.SummaryTotalProfit;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sales data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            // Whenever a user searches or changes a date, we MUST reset to Page 1
            CurrentPage = 1;
            _ = LoadPageAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            SelectedStatus = "All";
            CurrentPage = 1;

            _ = LoadPageAsync();
        }

        // --- PAGINATION CONTROLS ---

        [RelayCommand]
        private void NextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
                _ = LoadPageAsync();
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                _ = LoadPageAsync();
            }
        }

        // ==========================================
        // 6. DRILL-DOWN RECEIPT COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task OpenReceiptAsync(SalesExplorerRecordDto clickedRow)
        {
            if (clickedRow == null) return;

            try
            {
                var fullReceipt = await _repository.GetSaleReceiptDetailsAsync(clickedRow.SaleId);

                if (fullReceipt != null)
                {
                    SelectedReceipt = fullReceipt;
                    IsReceiptModalOpen = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load receipt details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseReceipt()
        {
            IsReceiptModalOpen = false;
            SelectedReceipt = null;
        }
    }
}