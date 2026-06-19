using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static POS.Core.Models.DTOs.ItemPerformanceDto;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ItemSalesAnalyticsViewModel : ViewModelBase
    {
        private readonly SalesAnalyticsRepository _repository;

        // ==========================================
        // 1. FILTER PARAMETERS
        // ==========================================
        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddDays(-30); // Default to last 30 days

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // ==========================================
        // 2. MACRO KPI CARDS
        // ==========================================
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty] private decimal _totalCost;
        [ObservableProperty] private decimal _grossProfit;
        [ObservableProperty] private double _averageMargin;
        [ObservableProperty] private decimal _totalUnitsSold;
        [ObservableProperty] private int _activeSellingItems;
        [ObservableProperty] private int _deadStockItems;

        // ==========================================
        // 3. MASTER GRID COLLECTION
        // ==========================================
        public ObservableCollection<ItemPerformanceDto> ItemPerformances { get; set; } = new();

        public ObservableCollection<ItemPerformanceDto> TopSellingItems { get; set; } = new();
        public ObservableCollection<ItemPerformanceDto> WorstSellingItems { get; set; } = new();
        public ObservableCollection<ItemPerformanceDto> DeadStockList { get; set; } = new();

        public ObservableCollection<ItemPerformanceDto> HighestProfitItems { get; set; } = new();
        public ObservableCollection<ItemPerformanceDto> NegativeMarginItems { get; set; } = new();
        public ObservableCollection<TrendPointDto> SalesTrendData { get; set; } = new();

        public class ItemDrillDownDto
        {
            public DateTime TransactionDate { get; set; }
            public string TransactionType { get; set; } = string.Empty; // "SALE" or "GRN"
            public string DocumentNo { get; set; } = string.Empty;
            public string PartyName { get; set; } = string.Empty; // Supplier or Customer Name
            public decimal QtyIn { get; set; }
            public decimal QtyOut { get; set; }
        }

        public ItemSalesAnalyticsViewModel(SalesAnalyticsRepository repository)
        {
            _repository = repository;

            // Auto-fire the analytics engine when the page opens
            _ = GenerateAnalyticsAsync();
        }

        // ==========================================
        // 4. THE EXECUTION ENGINE
        // ==========================================
        [RelayCommand]
        private async Task GenerateAnalyticsAsync()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Fire the KPI Engine
                var kpis = await _repository.GetKpisAsync(StartDate, EndDate, 90); // 90 days = Dead Stock threshold
                TotalRevenue = kpis.TotalRevenue;
                TotalCost = kpis.TotalCost;
                GrossProfit = kpis.GrossProfit;
                AverageMargin = kpis.AverageMargin;
                TotalUnitsSold = kpis.TotalUnitsSold;
                ActiveSellingItems = kpis.ActiveSellingItems;
                DeadStockItems = kpis.DeadStockItems;

                // 2. Fire the Master Grid Engine
                var rawGridData = await _repository.GetItemPerformanceAsync(StartDate, EndDate);

                // 3. Apply Local UI Search Filters (Fast in-memory filtering)
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var lowerSearch = SearchText.ToLower();
                    rawGridData = rawGridData.Where(x =>
                        x.ItemName.ToLower().Contains(lowerSearch) ||
                        x.ItemCode.ToLower().Contains(lowerSearch) ||
                        x.CategoryName.ToLower().Contains(lowerSearch)).ToList();
                }

                // 4. Bind to UI
                // 4. Bind to UI & Pivot Intelligence Data
                ItemPerformances.Clear();
                TopSellingItems.Clear();
                WorstSellingItems.Clear();
                DeadStockList.Clear();

                // Master Grid
                foreach (var item in rawGridData) ItemPerformances.Add(item);

                // Intelligence: Winners (Top 20 by Volume)
                var topSellers = rawGridData.OrderByDescending(x => x.QtySold).Take(20);
                foreach (var item in topSellers) TopSellingItems.Add(item);

                // Intelligence: Losers (Bottom 20 by Volume, excluding zero-sellers)
                var worstSellers = rawGridData.Where(x => x.QtySold > 0).OrderBy(x => x.QtySold).Take(20);
                foreach (var item in worstSellers) WorstSellingItems.Add(item);

                // Intelligence: Dead Stock (Items with SOH that haven't sold in 90 days)
                var deadStockData = await _repository.GetDeadStockAsync(90);
                foreach (var item in deadStockData) DeadStockList.Add(item);

                // Intelligence: Profitability (Top 20 Cash Cows)
                HighestProfitItems.Clear();
                var mostProfitable = rawGridData.Where(x => x.Profit > 0).OrderByDescending(x => x.Profit).Take(20);
                foreach (var item in mostProfitable) HighestProfitItems.Add(item);

                // Intelligence: Profitability (Items losing money)
                NegativeMarginItems.Clear();
                var negativeMargin = rawGridData.Where(x => x.Margin < 0).OrderBy(x => x.Margin); // Lowest negative first
                foreach (var item in negativeMargin) NegativeMarginItems.Add(item);

                // Intelligence: Time-Series Trends
                SalesTrendData.Clear();
                var trendData = await _repository.GetSalesTrendsAsync(StartDate, EndDate);
                foreach (var item in trendData) SalesTrendData.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Analytics Engine Error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            _ = GenerateAnalyticsAsync();
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            MessageBox.Show("Excel Export module initializing...\n\n(Will connect to ClosedXML in the next phase).", "Export to Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}