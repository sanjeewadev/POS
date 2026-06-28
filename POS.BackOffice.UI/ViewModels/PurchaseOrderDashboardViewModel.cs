using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderDashboardViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly SupplierRepository _supplierRepository;

        // =========================================================
        // FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Supplier? _selectedSupplierFilter;

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        [ObservableProperty]
        private DateTime? _filterStartDate;

        [ObservableProperty]
        private DateTime? _filterEndDate;

        // =========================================================
        // SELECTION / DETAILS
        // =========================================================

        [ObservableProperty]
        private PoSummaryDto? _selectedPo;

        [ObservableProperty]
        private PoHeader? _viewingPoDetails;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<PoSummaryDto> PurchaseOrders { get; } = new();

        public ObservableCollection<Supplier> FilterSuppliers { get; } = new();

        public ObservableCollection<string> FilterStatuses { get; } = new(new[]
        {
            "All",
            "Draft",
            "Approved",
            "Partially Received",
            "Closed",
            "Cancelled"
        });

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public PurchaseOrderDashboardViewModel(
            PoRepository poRepository,
            SupplierRepository supplierRepository)
        {
            _poRepository = poRepository;
            _supplierRepository = supplierRepository;

            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                await LoadFilterSuppliersAsync();
                await LoadDataInternalAsync();

                StatusMessage = $"{PurchaseOrders.Count} purchase order(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize PO dashboard.";

                MessageBox.Show(
                    $"Failed to initialize PO dashboard:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadFilterSuppliersAsync()
        {
            FilterSuppliers.Clear();

            var suppliers = await _supplierRepository.GetAllAsync();

            foreach (var supplier in suppliers
                         .Where(s => !s.IsDeactivated)
                         .OrderBy(s => s.SupplierName))
            {
                FilterSuppliers.Add(supplier);
            }
        }

        // =========================================================
        // FILTER CHANGE EVENTS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            StatusMessage = "Search text changed. Press Enter or click SEARCH.";
        }

        partial void OnSelectedSupplierFilterChanged(Supplier? value)
        {
            StatusMessage = "Supplier filter changed. Click SEARCH.";
        }

        partial void OnSelectedStatusFilterChanged(string value)
        {
            StatusMessage = "Status filter changed. Click SEARCH.";
        }

        partial void OnFilterStartDateChanged(DateTime? value)
        {
            StatusMessage = "Date filter changed. Click SEARCH.";
        }

        partial void OnFilterEndDateChanged(DateTime? value)
        {
            StatusMessage = "Date filter changed. Click SEARCH.";
        }

        partial void OnViewingPoDetailsChanged(PoHeader? value)
        {
            CancelPoCommand.NotifyCanExecuteChanged();
            ClonePoCommand.NotifyCanExecuteChanged();
            PrintPoCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            SearchCommand.NotifyCanExecuteChanged();
            RefreshDatabaseCommand.NotifyCanExecuteChanged();
            ClearFiltersCommand.NotifyCanExecuteChanged();
            ViewDetailsCommand.NotifyCanExecuteChanged();
            CancelPoCommand.NotifyCanExecuteChanged();
            ClonePoCommand.NotifyCanExecuteChanged();
            PrintPoCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // LOAD / SEARCH
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshDatabaseAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                await LoadDataInternalAsync();
                StatusMessage = $"{PurchaseOrders.Count} purchase order(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load purchase orders.";

                MessageBox.Show(
                    $"Failed to load purchase orders:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDataInternalAsync()
        {
            PurchaseOrders.Clear();

            if (FilterStartDate.HasValue &&
                FilterEndDate.HasValue &&
                FilterEndDate.Value.Date < FilterStartDate.Value.Date)
            {
                throw new InvalidOperationException("Filter end date cannot be before start date.");
            }

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

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedSupplierFilter = null;
            SelectedStatusFilter = "All";
            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            await LoadDataAsync();
        }

        // =========================================================
        // DETAILS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanViewDetails))]
        private async Task ViewDetailsAsync(PoSummaryDto? summary)
        {
            if (summary == null)
                return;

            IsBusy = true;

            try
            {
                var fullPo = await _poRepository.GetPurchaseOrderDetailsAsync(summary.PoHeaderId);

                if (fullPo == null)
                {
                    MessageBox.Show(
                        "Selected Purchase Order was not found.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                ViewingPoDetails = fullPo;
                StatusMessage = $"Viewing {fullPo.PoNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load PO details.";

                MessageBox.Show(
                    $"Failed to load PO details:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CloseDetails()
        {
            ViewingPoDetails = null;
            StatusMessage = "Returned to dashboard.";
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanCancelPo))]
        private async Task CancelPoAsync()
        {
            if (ViewingPoDetails == null)
                return;

            var result = MessageBox.Show(
                $"Cancel Purchase Order '{ViewingPoDetails.PoNumber}'?\n\n" +
                "This is only allowed if no quantities have been received.",
                "Confirm PO Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _poRepository.CancelPurchaseOrderAsync(
                    ViewingPoDetails.Id,
                    cancelledBy: "Admin",
                    reason: "Cancelled from PO dashboard.");

                MessageBox.Show(
                    "Purchase Order cancelled successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ViewingPoDetails = null;
                await LoadDataInternalAsync();

                StatusMessage = "Purchase Order cancelled.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Cancellation blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Action Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to cancel PO.";

                MessageBox.Show(
                    $"Failed to cancel PO:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanClonePo))]
        private async Task ClonePoAsync()
        {
            if (ViewingPoDetails == null)
                return;

            var result = MessageBox.Show(
                $"Clone '{ViewingPoDetails.PoNumber}' into a new Purchase Order?",
                "Clone PO",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (App.Services == null)
                {
                    MessageBox.Show(
                        "Application service container is not available.",
                        "Clone Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var createPoViewModel = App.Services.GetRequiredService<PurchaseOrderViewModel>();

                // Give the target ViewModel a short chance to finish async lookup loading.
                for (int i = 0; i < 10 && !createPoViewModel.Suppliers.Any(); i++)
                {
                    await Task.Delay(100);
                }

                createPoViewModel.ClearCommand.Execute(null);

                var supplier = createPoViewModel.Suppliers
                    .FirstOrDefault(s => s.Id == ViewingPoDetails.SupplierId);

                if (supplier == null)
                {
                    MessageBox.Show(
                        "Cannot clone because the supplier is inactive or not loaded.",
                        "Clone Blocked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                createPoViewModel.SelectedSupplier = supplier;
                createPoViewModel.SelectedTerms = ViewingPoDetails.Terms;
                createPoViewModel.CreditDaysInput = ViewingPoDetails.CreditDays;
                createPoViewModel.OrderDate = DateTime.Now;
                createPoViewModel.ExpectedDate = DateTime.Now.AddDays(7);
                createPoViewModel.Remarks =
                    $"[Cloned from {ViewingPoDetails.PoNumber}]\n{ViewingPoDetails.Remarks}".Trim();

                createPoViewModel.PoLines.Clear();

                foreach (var line in ViewingPoDetails.PoLines)
                {
                    createPoViewModel.PoLines.Add(new PoLine
                    {
                        ItemVariantId = line.ItemVariantId,
                        ItemCode = !string.IsNullOrWhiteSpace(line.ItemCode)
                            ? line.ItemCode
                            : line.ItemVariant?.ItemParent?.ItemCode ?? string.Empty,
                        VariantDescription = !string.IsNullOrWhiteSpace(line.VariantDescription)
                            ? line.VariantDescription
                            : line.ItemVariant?.VariantDescription ?? "Standard",
                        Description = !string.IsNullOrWhiteSpace(line.Description)
                            ? line.Description
                            : line.ItemVariant?.ItemParent?.ItemName ?? string.Empty,
                        Barcode = line.ItemVariant?.Barcode ?? string.Empty,
                        Uom = line.Uom,
                        OrderQty = line.OrderQty,
                        ExpectedCost = line.ExpectedCost,
                        LineDiscount = line.LineDiscount,
                        TaxCode = line.TaxCode,
                        SupplierItemCode = line.SupplierItemCode,
                        Moq = 1
                    });
                }

                createPoViewModel.RecalculateTotals();

                var mainViewModel = App.Services.GetRequiredService<MainViewModel>();
                mainViewModel.CurrentPage = createPoViewModel;

                ViewingPoDetails = null;
                StatusMessage = "PO cloned into new Purchase Order form.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to clone PO:\n\n{ex.Message}",
                    "Clone Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanPrintPo))]
        private void PrintPo()
        {
            if (ViewingPoDetails == null)
                return;

            MessageBox.Show(
                $"PDF export for {ViewingPoDetails.PoNumber} will be connected after the PO and GRN workflow is stable.",
                "PDF Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanViewDetails(PoSummaryDto? summary)
        {
            return !IsBusy && summary != null;
        }

        private bool CanCancelPo()
        {
            if (IsBusy || ViewingPoDetails == null)
                return false;

            return ViewingPoDetails.Status != "Closed" &&
                   ViewingPoDetails.Status != "Cancelled" &&
                   !ViewingPoDetails.PoLines.Any(l => l.ReceivedQty > 0);
        }

        private bool CanClonePo()
        {
            return !IsBusy && ViewingPoDetails != null;
        }

        private bool CanPrintPo()
        {
            return !IsBusy && ViewingPoDetails != null;
        }
    }
}