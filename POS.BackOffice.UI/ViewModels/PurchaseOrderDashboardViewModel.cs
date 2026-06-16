using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderDashboardViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private PoSummaryDto? _selectedPo;

        public ObservableCollection<PoSummaryDto> PurchaseOrders { get; set; } = new();

        public PurchaseOrderDashboardViewModel(PoRepository poRepository)
        {
            _poRepository = poRepository;
            _ = LoadDataAsync();
        }

        // Live Search Trigger
        partial void OnSearchTextChanged(string value)
        {
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            PurchaseOrders.Clear();
            var data = await _poRepository.GetPoSummariesAsync(SearchText);

            foreach (var po in data)
            {
                PurchaseOrders.Add(po);
            }
        }

        [RelayCommand]
        private async Task RefreshDatabaseAsync()
        {
            SearchText = string.Empty;
            await LoadDataAsync();
        }

        [RelayCommand]
        private void PrintPo()
        {
            if (SelectedPo == null)
            {
                MessageBox.Show("Please select a Purchase Order from the list first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Wire this to your actual PDF/Printing engine (like Crystal Reports, RDLC, or QuestPDF)
            MessageBox.Show($"Connecting to Print Engine...\n\nGenerating PDF for {SelectedPo.PoNumber} to Supplier: {SelectedPo.SupplierName}.", "Print PO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}