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
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly DashboardRepository _repository;

        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Loading operational dashboard...";
        [ObservableProperty] private decimal _merchandiseSales;
        [ObservableProperty] private decimal _customerReturns;
        [ObservableProperty] private decimal _netSales;
        [ObservableProperty] private decimal _grossProfit;
        [ObservableProperty] private decimal _customerCreditOutstanding;
        [ObservableProperty] private decimal _supplierOutstanding;
        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private int _negativeStockCount;
        [ObservableProperty] private int _openShiftCount;
        [ObservableProperty] private int _heldCartCount;
        [ObservableProperty] private int _draftSupplierClaimCount;
        [ObservableProperty] private int _submittedSupplierClaimCount;

        public ObservableCollection<FinancialTenderTotalDto> TenderTotals { get; } = new();
        public ObservableCollection<DashboardTopItemDto> TopItems { get; } = new();
        public ObservableCollection<DashboardAttentionDto> AttentionItems { get; } = new();

        public DashboardViewModel(DashboardRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _ = RefreshAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show(
                    "Start date cannot be later than end date.",
                    "Dashboard",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = "Reading operational totals...";
                DashboardSummaryDto summary = await _repository.GetSummaryAsync(StartDate, EndDate);

                MerchandiseSales = summary.MerchandiseSales;
                CustomerReturns = summary.CustomerReturns;
                NetSales = summary.NetSales;
                GrossProfit = summary.GrossProfit;
                CustomerCreditOutstanding = summary.CustomerCreditOutstanding;
                SupplierOutstanding = summary.SupplierOutstanding;
                LowStockCount = summary.LowStockCount;
                NegativeStockCount = summary.NegativeStockCount;
                OpenShiftCount = summary.OpenShiftCount;
                HeldCartCount = summary.HeldCartCount;
                DraftSupplierClaimCount = summary.DraftSupplierClaimCount;
                SubmittedSupplierClaimCount = summary.SubmittedSupplierClaimCount;

                Replace(TenderTotals, summary.TenderTotals);
                Replace(TopItems, summary.TopItems);
                Replace(AttentionItems, summary.AttentionItems);

                StatusMessage = $"Dashboard refreshed for {summary.StartDate:yyyy-MM-dd} to {summary.EndDate:yyyy-MM-dd}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Dashboard could not be loaded.";
                MessageBox.Show(ex.Message, "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task TodayAsync()
        {
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task ThisMonthAsync()
        {
            StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            EndDate = DateTime.Today;
            await RefreshAsync();
        }

        private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }
    }
}
