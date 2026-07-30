using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ShopCostFreeIssueReportViewModel : ObservableObject
    {
        private readonly ShopCostFreeIssueReportRepository _repository;

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private decimal _totalQuantity;

        [ObservableProperty]
        private decimal _totalCost;

        public ObservableCollection<ShopCostFreeIssueDto> ReportItems { get; } = new();

        public ShopCostFreeIssueReportViewModel(ShopCostFreeIssueReportRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        [RelayCommand]
        private async Task LoadReportAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = "Loading report...";
            ReportItems.Clear();
            RecalculateTotals();

            try
            {
                if (ToDate < FromDate)
                {
                    MessageBox.Show("The 'To' date cannot be earlier than the 'From' date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "Invalid date range.";
                    return;
                }

                var items = await _repository.GetReportAsync(FromDate, ToDate);

                foreach (var item in items)
                {
                    ReportItems.Add(item);
                }

                RecalculateTotals();

                StatusMessage = $"Loaded {ReportItems.Count} item(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load report.";
                MessageBox.Show($"An error occurred while loading the report:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RecalculateTotals()
        {
            if (!ReportItems.Any())
            {
                TotalQuantity = 0;
                TotalCost = 0;
                return;
            }

            TotalQuantity = ReportItems.Sum(i => i.Quantity);
            TotalCost = ReportItems.Sum(i => i.TotalCost);
        }

        [RelayCommand]
        private void Clear()
        {
            FromDate = DateTime.Today;
            ToDate = DateTime.Today;
            ReportItems.Clear();
            RecalculateTotals();
            StatusMessage = "Ready.";
        }
    }
}