using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using Microsoft.Extensions.DependencyInjection; // Used for the Clone example

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderDashboardViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly SupplierRepository _supplierRepository;

        // --- FILTERS ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private Supplier? _selectedSupplierFilter;
        [ObservableProperty] private string _selectedStatusFilter = "All";
        [ObservableProperty] private DateTime? _filterStartDate;
        [ObservableProperty] private DateTime? _filterEndDate;

        // --- STATE & SELECTIONS ---
        [ObservableProperty] private PoSummaryDto? _selectedPo;

        // The state object for our slide-out Detail Panel
        [ObservableProperty] private PoHeader? _viewingPoDetails;

        // --- COLLECTIONS ---
        public ObservableCollection<PoSummaryDto> PurchaseOrders { get; set; } = new();
        public ObservableCollection<Supplier> FilterSuppliers { get; set; } = new();
        public ObservableCollection<string> FilterStatuses { get; set; } = new(new[] { "All", "Draft", "Approved", "Partially Received", "Closed", "Canceled" });

        public PurchaseOrderDashboardViewModel(PoRepository poRepository, SupplierRepository supplierRepository)
        {
            _poRepository = poRepository;
            _supplierRepository = supplierRepository;

            // Set default date range to the last 30 days
            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            FilterSuppliers.Clear();
            var suppliers = await _supplierRepository.GetAllAsync();
            foreach (var sup in suppliers)
            {
                FilterSuppliers.Add(sup);
            }

            await LoadDataAsync();
        }

        // ==========================================
        // FILTER TRIGGERS
        // ==========================================
        partial void OnSearchTextChanged(string value) => _ = LoadDataAsync();
        partial void OnSelectedSupplierFilterChanged(Supplier? value) => _ = LoadDataAsync();
        partial void OnSelectedStatusFilterChanged(string value) => _ = LoadDataAsync();
        partial void OnFilterStartDateChanged(DateTime? value) => _ = LoadDataAsync();
        partial void OnFilterEndDateChanged(DateTime? value) => _ = LoadDataAsync();

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            PurchaseOrders.Clear();

            var data = await _poRepository.GetPoSummariesAsync(
                SearchText,
                SelectedSupplierFilter?.Id,
                SelectedStatusFilter,
                FilterStartDate,
                FilterEndDate);

            foreach (var po in data)
            {
                PurchaseOrders.Add(po);
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedSupplierFilter = null;
            SelectedStatusFilter = "All";
            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task RefreshDatabaseAsync()
        {
            await LoadDataAsync();
        }

        // ==========================================
        // MASTER-DETAIL UI COMMANDS
        // ==========================================

        [RelayCommand]
        private async Task ViewDetailsAsync(PoSummaryDto summary)
        {
            if (summary == null) return;

            try
            {
                var fullPo = await _poRepository.GetPurchaseOrderDetailsAsync(summary.PoHeaderId);

                if (fullPo != null)
                {
                    ViewingPoDetails = fullPo;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load PO details: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseDetails()
        {
            ViewingPoDetails = null;
        }

        // ==========================================
        // ✅ NEW: ACTION COMMANDS
        // ==========================================

        [RelayCommand]
        private async Task CancelPoAsync()
        {
            if (ViewingPoDetails == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to permanently cancel PO '{ViewingPoDetails.PoNumber}'?\n\nThis will release expected stock reservations. This action cannot be undone.",
                "Confirm Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _poRepository.CancelPurchaseOrderAsync(ViewingPoDetails.Id);

                    MessageBox.Show("Purchase Order has been canceled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Close the overlay and refresh the master grid to show the new "Canceled" status
                    CloseDetails();
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to cancel PO: {ex.Message}", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ClonePo()
        {
            if (ViewingPoDetails == null) return;

            var result = MessageBox.Show(
                $"Do you want to clone '{ViewingPoDetails.PoNumber}' into a new Purchase Order draft?",
                "Clone PO",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 1. Resolve both the Target ViewModel and your Main Navigation ViewModel
                var createPoViewModel = App.Services!.GetRequiredService<PurchaseOrderViewModel>();
                var mainViewModel = App.Services.GetRequiredService<MainViewModel>();

                // 2. Pre-fill the header data
                createPoViewModel.SelectedSupplier = createPoViewModel.Suppliers.FirstOrDefault(s => s.Id == ViewingPoDetails.SupplierId);
                createPoViewModel.SelectedTerms = ViewingPoDetails.Terms;
                createPoViewModel.CreditDaysInput = ViewingPoDetails.CreditDays;
                createPoViewModel.Remarks = $"[Cloned from {ViewingPoDetails.PoNumber}]\n" + ViewingPoDetails.Remarks;

                // 3. Pre-fill the line items
                createPoViewModel.PoLines.Clear();
                foreach (var line in ViewingPoDetails.PoLines)
                {
                    createPoViewModel.PoLines.Add(new PoLine
                    {
                        ItemVariantId = line.ItemVariantId,
                        ItemCode = line.ItemVariant.ItemParent.ItemCode,
                        VariantDescription = line.ItemVariant.VariantDescription,
                        Description = line.ItemVariant.ItemParent.ItemName,
                        OrderQty = line.OrderQty,
                        ExpectedCost = line.ExpectedCost,
                        LineDiscount = line.LineDiscount,
                        TaxCode = line.TaxCode,
                        SupplierItemCode = line.SupplierItemCode
                    });
                }

                // 4. Force it to recalculate the totals
                createPoViewModel.RecalculateTotals();

                // 5. Trigger the screen switch using your exact property name!
                mainViewModel.CurrentPage = createPoViewModel;

                // Close the overlay on the dashboard so it's clean if the user comes back
                CloseDetails();
            }
        }

        [RelayCommand]
        private void PrintPo()
        {
            if (ViewingPoDetails != null)
            {
                // Passes the fully-loaded object (including all Line Items and Supplier info) to the future PDF Engine
                MessageBox.Show($"Generating PDF for {ViewingPoDetails.PoNumber} to Supplier: {ViewingPoDetails.Supplier.SupplierName}...", "Print Engine", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}