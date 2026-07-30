using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // This is needed for [RelayCommand]
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockAdjustmentHistoryViewModel : ObservableObject
    {
        private readonly StockAdjustmentRepository _repository;

        public StockAdjustmentHistoryViewModel(
            StockAdjustmentRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private StockAdjustmentHistoryRowDto? _selectedAdjustment;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<StockAdjustmentHistoryRowDto> Adjustments { get; } = new();
        public ObservableCollection<StockAdjustmentHistoryLineDto> Lines { get; } = new();

        partial void OnSelectedAdjustmentChanged(StockAdjustmentHistoryRowDto? value)
        {
            _ = LoadSelectedDetailAsync(value);
        }

        public async Task InitializeAsync()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var rows = await _repository.SearchHistoryAsync(
                    FromDate,
                    ToDate,
                    SearchText);

                Adjustments.Clear();
                foreach (StockAdjustmentHistoryRowDto row in rows)
                    Adjustments.Add(row);

                SelectedAdjustment = Adjustments.FirstOrDefault();
                StatusMessage = $"Loaded {Adjustments.Count} stock adjustment(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "History load failed.";
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment History",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedDetailAsync(StockAdjustmentHistoryRowDto? selected)
        {
            Lines.Clear();

            if (selected == null)
                return;

            try
            {
                StockAdjustmentHistoryDetailDto? detail =
                    await _repository.GetHistoryDetailAsync(selected.Id);

                if (detail == null || SelectedAdjustment?.Id != selected.Id)
                    return;

                foreach (StockAdjustmentHistoryLineDto line in detail.Lines)
                    Lines.Add(line);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
