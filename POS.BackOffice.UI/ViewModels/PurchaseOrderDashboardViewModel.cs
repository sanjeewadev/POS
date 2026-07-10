using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class PurchaseOrderDashboardViewModel : ObservableObject
    {
        private readonly PoRepository _poRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;

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

        [ObservableProperty]
        private bool _showCancelledPurchaseOrders = false;

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

        public ObservableCollection<string> FilterStatuses { get; } = new();

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsDetailsOpen => ViewingPoDetails != null;

        public PurchaseOrderDashboardViewModel(
            PoRepository poRepository,
            SupplierRepository supplierRepository,
            IMessageBoxService messageBoxService)
        {
            _poRepository = poRepository ?? throw new ArgumentNullException(nameof(poRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            RefreshStatusFilters();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;

            try
            {
                RefreshStatusFilters();

                await LoadFilterSuppliersAsync();
                await LoadDataInternalAsync();

                _isInitialized = true;
                StatusMessage = $"{PurchaseOrders.Count} purchase order(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize PO dashboard.";

                _messageBoxService.ShowError(
                    $"Failed to initialize PO dashboard:\n\n{ex.Message}",
                    "Database Error");
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
                         .OrderBy(s => s.SupplierName)
                         .ThenBy(s => s.SupplierCode))
            {
                FilterSuppliers.Add(supplier);
            }
        }

        private void RefreshStatusFilters()
        {
            string current = SelectedStatusFilter;

            FilterStatuses.Clear();

            FilterStatuses.Add("All");
            FilterStatuses.Add("Approved");
            FilterStatuses.Add("Closed");

            if (ShowCancelledPurchaseOrders)
                FilterStatuses.Add("Cancelled");

            if (!FilterStatuses.Contains(current))
                SelectedStatusFilter = "All";
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

        partial void OnShowCancelledPurchaseOrdersChanged(bool value)
        {
            RefreshStatusFilters();

            StatusMessage = value
                ? "Cancelled POs will be included after SEARCH."
                : "Cancelled POs are hidden. Click SEARCH to refresh.";
        }

        partial void OnViewingPoDetailsChanged(PoHeader? value)
        {
            OnPropertyChanged(nameof(IsDetailsOpen));

            CloseDetailsCommand.NotifyCanExecuteChanged();
            CancelPoCommand.NotifyCanExecuteChanged();
            ClonePoCommand.NotifyCanExecuteChanged();
            PrintPoCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshDatabaseCommand.NotifyCanExecuteChanged();
            ClearFiltersCommand.NotifyCanExecuteChanged();
            ViewDetailsCommand.NotifyCanExecuteChanged();
            CloseDetailsCommand.NotifyCanExecuteChanged();
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
            await LoadFilterSuppliersAsync();
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
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Search blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Filter Error");
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load purchase orders.";

                _messageBoxService.ShowError(
                    $"Failed to load purchase orders:\n\n{ex.Message}",
                    "Database Error");
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
                FilterEndDate,
                ShowCancelledPurchaseOrders);

            foreach (var po in data)
                PurchaseOrders.Add(po);
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedSupplierFilter = null;
            ShowCancelledPurchaseOrders = false;
            SelectedStatusFilter = "All";
            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            RefreshStatusFilters();

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
                    _messageBoxService.ShowWarning(
                        "Selected Purchase Order was not found.",
                        "Not Found");
                    return;
                }

                ViewingPoDetails = fullPo;
                SelectedPo = summary;

                StatusMessage = $"Viewing {fullPo.PoNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load PO details.";

                _messageBoxService.ShowError(
                    $"Failed to load PO details:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCloseDetails))]
        private void CloseDetails()
        {
            ViewingPoDetails = null;
            StatusMessage = "Returned to dashboard.";
        }

        private bool CanCloseDetails()
        {
            return !IsBusy && ViewingPoDetails != null;
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanCancelPo))]
        private async Task CancelPoAsync()
        {
            if (ViewingPoDetails == null)
                return;

            bool confirm = _messageBoxService.ShowConfirmation(
                $"Cancel Purchase Order '{ViewingPoDetails.PoNumber}'?\n\n" +
                "This is only allowed if no quantities have been received.",
                "Confirm PO Cancellation");

            if (!confirm)
                return;

            IsBusy = true;

            try
            {
                await _poRepository.CancelPurchaseOrderAsync(
                    ViewingPoDetails.Id,
                    cancelledBy: "Admin",
                    reason: "Cancelled from PO dashboard.");

                _messageBoxService.ShowInformation(
                    "Purchase Order cancelled successfully.",
                    "Success");

                ViewingPoDetails = null;
                await LoadDataInternalAsync();

                StatusMessage = "Purchase Order cancelled.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Cancellation blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Action Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to cancel PO.";

                _messageBoxService.ShowError(
                    $"Failed to cancel PO:\n\n{ex.Message}",
                    "Database Error");
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

            bool confirm = _messageBoxService.ShowConfirmation(
                $"Clone '{ViewingPoDetails.PoNumber}' into a new Purchase Order?",
                "Clone PO");

            if (!confirm)
                return;

            IsBusy = true;

            try
            {
                if (App.Services == null)
                {
                    _messageBoxService.ShowWarning(
                        "Application service container is not available.",
                        "Clone Error");
                    return;
                }

                var createPoViewModel = App.Services.GetRequiredService<PurchaseOrderViewModel>();

                if (createPoViewModel.InitializeCommand.CanExecute(null))
                    await createPoViewModel.InitializeCommand.ExecuteAsync(null);

                createPoViewModel.ClearCommand.Execute(null);

                var supplier = createPoViewModel.Suppliers
                    .FirstOrDefault(s => s.Id == ViewingPoDetails.SupplierId);

                if (supplier == null)
                {
                    _messageBoxService.ShowWarning(
                        "Cannot clone because the supplier is inactive or not loaded.",
                        "Clone Blocked");
                    return;
                }

                createPoViewModel.SelectedSupplier = supplier;
                createPoViewModel.SelectedTerms = string.IsNullOrWhiteSpace(ViewingPoDetails.Terms)
                    ? "Credit"
                    : ViewingPoDetails.Terms;
                createPoViewModel.CreditDaysInput = ViewingPoDetails.CreditDays;
                createPoViewModel.OrderDate = DateTime.Now;
                createPoViewModel.ExpectedDate = DateTime.Now.AddDays(7);
                createPoViewModel.Remarks =
                    $"[Cloned from {ViewingPoDetails.PoNumber}]\n{ViewingPoDetails.Remarks}".Trim();

                createPoViewModel.PoLines.Clear();

                foreach (var line in ViewingPoDetails.PoLines)
                {
                    var newLine = new PoLine
                    {
                        ItemVariantId = line.ItemVariantId,
                        ItemCode = !string.IsNullOrWhiteSpace(line.ItemCode)
                            ? line.ItemCode
                            : line.ItemVariant?.ItemParent?.ItemCode ?? string.Empty,
                        SkuCode = line.ItemVariant?.SkuCode ?? string.Empty,
                        VariantDescription = !string.IsNullOrWhiteSpace(line.VariantDescription)
                            ? line.VariantDescription
                            : string.IsNullOrWhiteSpace(line.ItemVariant?.VariantDescription)
                                ? "Standard"
                                : line.ItemVariant.VariantDescription,
                        Description = !string.IsNullOrWhiteSpace(line.Description)
                            ? line.Description
                            : line.ItemVariant?.ItemParent?.ItemName ?? string.Empty,
                        PrintName = line.ItemVariant?.ItemParent?.PrintName ?? string.Empty,
                        Barcode = line.ItemVariant?.Barcode ?? string.Empty,
                        Uom = string.IsNullOrWhiteSpace(line.Uom) ? "PCS" : line.Uom,
                        OrderQty = line.OrderQty,
                        ExpectedCost = line.ExpectedCost,
                        LineDiscountMode = string.IsNullOrWhiteSpace(line.LineDiscountMode)
                            ? "Amount"
                            : line.LineDiscountMode,
                        LineDiscountValue = line.LineDiscountValue,
                        LineDiscount = line.LineDiscount,
                        TaxCode = string.IsNullOrWhiteSpace(line.TaxCode)
                            ? "VAT"
                            : line.TaxCode,
                        VatRatePercent = line.VatRatePercent,
                        IsVatIncluded = line.IsVatIncluded,
                        TaxAmount = line.TaxAmount,
                        LineTotal = line.LineTotal,
                        SupplierItemCode = line.SupplierItemCode,
                        Moq = line.Moq <= 0 ? 1 : line.Moq,
                        HasBatchTracking = line.HasBatchTracking,
                        HasExpiryTracking = line.HasExpiryTracking,
                        LineStatus = "Open"
                    };

                    createPoViewModel.PoLines.Add(newLine);
                }

                createPoViewModel.RecalculateTotals();

                var mainViewModel = App.Services.GetRequiredService<MainViewModel>();
                mainViewModel.CurrentPage = createPoViewModel;

                ViewingPoDetails = null;

                StatusMessage = "PO cloned into new Purchase Order form.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to clone PO.";

                _messageBoxService.ShowError(
                    $"Failed to clone PO:\n\n{ex.Message}",
                    "Clone Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanPrintPo))]
        private void PrintPo()
        {
            if (ViewingPoDetails == null)
                return;

            _messageBoxService.ShowInformation(
                $"PDF export for {ViewingPoDetails.PoNumber} is not connected yet.\n\n" +
                "We will connect PDF export after PO save, dashboard, GRN posting, and GRN history are stable.",
                "PDF Export");
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

            if (ViewingPoDetails.Status == "Closed" ||
                ViewingPoDetails.Status == "Cancelled")
            {
                return false;
            }

            return !ViewingPoDetails.PoLines.Any(l => l.ReceivedQty > 0);
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