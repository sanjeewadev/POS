using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FinancialSummaryViewModel : ViewModelBase
    {
        private readonly FinancialAnalyticsRepository _repository;

        // ==========================================
        // 1. FILTER PROPERTIES
        // ==========================================
        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30); // Default to last 30 days for good charts
        [ObservableProperty] private DateTime _endDate = DateTime.Today;

        // ==========================================
        // 2. MACRO KPI CARDS (Financial Summary)
        // ==========================================
        [ObservableProperty] private decimal _grossSales;
        [ObservableProperty] private decimal _totalDiscounts;
        [ObservableProperty] private decimal _totalReturns;
        [ObservableProperty] private decimal _netSales;
        [ObservableProperty] private decimal _totalCostOfGoods;
        [ObservableProperty] private decimal _grossProfit;
        [ObservableProperty] private decimal _operatingExpenses;
        [ObservableProperty] private decimal _operatingMargin;
        [ObservableProperty] private int _totalSalesCount;
        [ObservableProperty] private decimal _averageSaleValue;

        // ==========================================
        // 3. LIVECHARTS PROPERTIES
        // ==========================================
        public SeriesCollection TrendSeries { get; set; } = new SeriesCollection();
        public ChartValues<string> TrendLabels { get; set; } = new ChartValues<string>();
        public Func<double, string> CurrencyFormatter { get; set; } = value => "Rs. " + value.ToString("N0");

        public FinancialSummaryViewModel(FinancialAnalyticsRepository repository)
        {
            _repository = repository;
            _ = LoadDashboardAsync();
        }

        // ==========================================
        // 4. THE ENGINE COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task LoadDashboardAsync()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Load the KPI Math
                var summary = await _repository.GetFinancialSummaryAsync(StartDate, EndDate);

                GrossSales = summary.GrossSales;
                TotalDiscounts = summary.TotalDiscounts;
                TotalReturns = summary.TotalReturns;
                NetSales = summary.NetSales;
                TotalCostOfGoods = summary.TotalCostOfGoods;
                GrossProfit = summary.GrossProfit;
                OperatingExpenses = summary.OperatingExpenses;
                OperatingMargin = summary.OperatingMargin;
                TotalSalesCount = summary.TotalSalesCount;
                AverageSaleValue = summary.AverageSaleValue;

                // 2. Load the Chart Data
                var trends = await _repository.GetFinancialTrendsAsync(StartDate, EndDate);

                // Clear previous chart data
                TrendLabels.Clear();
                var revenueValues = new ChartValues<decimal>();
                var profitValues = new ChartValues<decimal>();

                foreach (var point in trends)
                {
                    TrendLabels.Add(point.DateLabel);
                    revenueValues.Add(point.Revenue);
                    profitValues.Add(point.Profit);
                }

                // Rebuild the series for the UI
                TrendSeries.Clear();
                TrendSeries.Add(new LineSeries
                {
                    Title = "Revenue",
                    Values = revenueValues,
                    Stroke = System.Windows.Media.Brushes.DarkBlue,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    PointGeometrySize = 10
                });
                TrendSeries.Add(new LineSeries
                {
                    Title = "Gross Profit",
                    Values = profitValues,
                    Stroke = System.Windows.Media.Brushes.ForestGreen,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    PointGeometrySize = 10
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading financial dashboard: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            _ = LoadDashboardAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            _ = LoadDashboardAsync();
        }
    }
}