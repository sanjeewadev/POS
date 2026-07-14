using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.Cashier.UI.ViewModels
{
    public partial class StockInquiryViewModel : ObservableObject
    {
        private readonly StockInquiryRepository _repository;

        public ObservableCollection<StockInquiryResultDto> StockResults { get; } = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CheckStockCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "Enter an item code, SKU, barcode or description.";

        public StockInquiryViewModel(StockInquiryRepository repository)
        {
            _repository = repository;
        }

        private bool CanCheckStock() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanCheckStock))]
        private async Task CheckStockAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                IReadOnlyList<StockInquiryResultDto> results =
                    await _repository.SearchAsync(SearchText);

                StockResults.Clear();
                foreach (StockInquiryResultDto result in results)
                    StockResults.Add(result);

                StatusText = results.Count == 0
                    ? "No active Stock Items were found."
                    : $"{results.Count} Stock Item(s) found.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Stock inquiry",
                    ex);

                StatusText = "Stock could not be loaded. See the local POS log for details.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
