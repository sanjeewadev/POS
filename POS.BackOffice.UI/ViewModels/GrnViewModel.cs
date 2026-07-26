using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Utilities;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GrnViewModel : ObservableObject
    {
        private readonly GrnRepository _grnRepository;
        private readonly IMessageBoxService _messageBoxService;
        private readonly AuthService _authService;
        private readonly DispatcherTimer _recalculateTimer;

        private readonly List<GrnLineEntryDto> _allMatrixVariants = new();

        private bool _isInitialized = false;
        private bool _isRecalculating = false;
        private bool _isClearing = false;
        private bool _isLoadingPo = false;

        // =========================================================
        // HEADER
        // =========================================================

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private bool _isSupplierSelectionEnabled = true;

        [ObservableProperty]
        private string _supplierInvoiceNo = string.Empty;

        [ObservableProperty]
        private DateTime _invoiceDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _receivedDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _dueDate = DateTime.Now.AddDays(30);

        [ObservableProperty]
        private string _documentStatus = "DRAFT";

        [ObservableProperty]
        private string _remarks = string.Empty;

        [ObservableProperty]
        private bool _isHeaderConfirmed = false;

        public bool IsHeaderInputEnabled => !IsHeaderConfirmed && !IsBusy;

        public bool IsEntryEnabled => IsHeaderConfirmed && !IsPoLinked && !IsBusy;

        public bool IsDirectEntryEnabled => IsEntryEnabled;

        public bool IsPoLinked => SelectedPO != null;

        public bool IsMatrixExpiryEnabled =>
            IsEntryEnabled && ActiveMatrixVariants.Any(v => v.RequiresExpiry);

        // =========================================================
        // PO LINKING
        // =========================================================

        public ObservableCollection<GrnPoLookupDto> OpenPurchaseOrders { get; } = new();

        [ObservableProperty]
        private GrnPoLookupDto? _selectedPO;

        // =========================================================
        // ENTRY
        // =========================================================

        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        // Kept for backward compatibility with old XAML.
        [ObservableProperty]
        private string _matrixBatchNo = string.Empty;

        [ObservableProperty]
        private DateTime? _matrixExpiryDate = null;

        [ObservableProperty]
        private string _matrixFilterText = string.Empty;

        [ObservableProperty]
        private decimal _bulkMatrixQuantity = 0m;

        [ObservableProperty]
        private decimal _bulkMatrixUnitCost = 0m;

        [ObservableProperty]
        private decimal _bulkMatrixSellingPrice = 0m;

        [ObservableProperty]
        private bool _bulkMatrixVatIncluded = false;

        [ObservableProperty]
        private GrnLineEntryDto? _selectedMatrixVariant;

        // =========================================================
        // DISCOUNT BULK CONTROLS
        // =========================================================

        public ObservableCollection<string> DiscountModes { get; } = new(new[]
        {
            "Amount",
            "Percent"
        });

        [ObservableProperty]
        private string _bulkDiscountMode = "Amount";

        [ObservableProperty]
        private decimal _bulkDiscountValue = 0m;

        // =========================================================
        // TOTALS
        // =========================================================

        [ObservableProperty]
        private decimal _subtotal = 0m;

        [ObservableProperty]
        private decimal _totalDiscountAmount = 0m;

        [ObservableProperty]
        private decimal _totalVatAmount = 0m;

        [ObservableProperty]
        private decimal _globalBillDiscount = 0m;

        [ObservableProperty]
        private decimal _freightAmount = 0m;

        [ObservableProperty]
        private decimal _netPayable = 0m;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public ObservableCollection<GrnLineEntryDto> GrnLines { get; } = new();

        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; } = new();

        public ObservableCollection<GrnLineEntryDto> ActiveMatrixVariants { get; } = new();

        [ObservableProperty]
        private GrnLineEntryDto? _selectedLine;

        [ObservableProperty]
        private ItemMasterSummaryDto? _selectedItem;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public GrnViewModel(
            GrnRepository grnRepository,
            ItemMasterRepository itemMasterRepository,
            PoRepository poRepository,
            IMessageBoxService messageBoxService,
            AuthService authService)
        {
            _grnRepository = grnRepository ?? throw new ArgumentNullException(nameof(grnRepository));
            _ = itemMasterRepository ?? throw new ArgumentNullException(nameof(itemMasterRepository));
            _ = poRepository ?? throw new ArgumentNullException(nameof(poRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _recalculateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };

            _recalculateTimer.Tick += async (_, _) =>
            {
                _recalculateTimer.Stop();
                await RecalculateTotalsAuthoritativelyAsync();
            };
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanInitialize))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;
            StatusMessage = "Loading GRN page...";

            try
            {
                Suppliers.Clear();
                AvailableItems.Clear();
                OpenPurchaseOrders.Clear();

                var suppliers = await _grnRepository.GetActiveSuppliersAsync();

                foreach (var supplier in suppliers)
                    Suppliers.Add(supplier);

                await LoadOpenPurchaseOrdersAsync();

                BulkDiscountMode = DiscountModes.FirstOrDefault() ?? "Amount";

                _isInitialized = true;
                StatusMessage = "GRN page loaded. Select supplier, enter invoice number, then confirm header.";
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                StatusMessage = "Failed to initialize GRN page.";

                _messageBoxService.ShowError(
                    $"Failed to initialize GRN page:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadOpenPurchaseOrdersAsync()
        {
            OpenPurchaseOrders.Clear();

            var openPos = await _grnRepository.GetOpenPurchaseOrderLookupsAsync();

            foreach (var po in openPos)
                OpenPurchaseOrders.Add(po);
        }

        // =========================================================
        // HEADER CONFIRMATION
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanConfirmHeader))]
        private async Task ConfirmHeaderAsync()
        {
            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Supplier Required");

                return;
            }

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                _messageBoxService.ShowWarning(
                    "Supplier invoice number is required.",
                    "Invoice Required");

                return;
            }

            if (GrnLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "Header cannot be changed after GRN lines are added. Clear the form first.",
                    "Header Locked");

                return;
            }

            IsBusy = true;
            StatusMessage = "Checking supplier invoice...";

            try
            {
                bool duplicateInvoice = await _grnRepository.SupplierInvoiceExistsAsync(
                    SelectedSupplier.Id,
                    SupplierInvoiceNo);

                if (duplicateInvoice)
                {
                    _messageBoxService.ShowWarning(
                        "This supplier invoice number has already been posted for the selected supplier.",
                        "Duplicate Supplier Invoice");

                    StatusMessage = "Duplicate supplier invoice number.";
                    return;
                }

                IsHeaderConfirmed = true;
                IsSupplierSelectionEnabled = false;

                if (SelectedPO != null)
                {
                    StatusMessage = "Header confirmed for linked PO. Click LOAD to receive the selected PO.";
                    return;
                }

                await LoadAvailableItemsForSelectedSupplierAsync();

                StatusMessage = "Header confirmed. Item search and matrix receiving are now enabled.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to confirm GRN header.";

                _messageBoxService.ShowError(
                    $"Failed to confirm GRN header:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ResetHeader()
        {
            if (GrnLines.Any())
            {
                bool confirmed = _messageBoxService.ShowConfirmation(
                    "Resetting the header will clear all GRN lines.\n\nContinue?",
                    "Reset Header",
                    MessageBoxImage.Warning);

                if (!confirmed)
                    return;

                UnsubscribeGrnLineEvents();
                GrnLines.Clear();
                RecalculateTotals();
            }

            IsHeaderConfirmed = false;
            IsSupplierSelectionEnabled = true;
            AvailableItems.Clear();
            ClearLoadedMatrixOnly();

            StatusMessage = "Header reset. Select supplier and invoice again.";

            NotifyCommandStates();
        }

        private async Task LoadAvailableItemsForSelectedSupplierAsync()
        {
            AvailableItems.Clear();

            if (SelectedSupplier == null)
                return;

            var items = await _grnRepository.GetReceivableItemParentsForSupplierAsync(
                SelectedSupplier.Id);

            foreach (var item in items)
                AvailableItems.Add(item);

            if (!AvailableItems.Any())
            {
                _messageBoxService.ShowInformation(
                    "No supplier-approved purchasable items were found for this supplier.",
                    "No Items");
            }
        }

        // =========================================================
        // HEADER EVENTS
        // =========================================================

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            OnPropertyChanged(nameof(SupplierVatStatusText));
            OnPropertyChanged(nameof(CanUseSupplierVatPriceMode));
            OnPropertyChanged(nameof(SupplierPriceModeText));

            if (_isClearing)
                return;

            if (value != null)
            {
                int creditDays = value.DefaultCreditDays > 0
                    ? value.DefaultCreditDays
                    : 30;

                DueDate = InvoiceDate.Date.AddDays(creditDays);

                if (!value.HasVat)
                    SupplierPricesIncludeVat = false;

                StatusMessage = value.HasVat
                    ? $"Selected supplier: {value.SupplierName}"
                    : $"Selected non-VAT supplier: {value.SupplierName}. Supplier input VAT will be zero.";
            }
            else
            {
                StatusMessage = "Select a supplier before adding items.";
            }

            if (!_isLoadingPo)
            {
                if (IsHeaderConfirmed && !GrnLines.Any())
                {
                    IsHeaderConfirmed = false;
                    IsSupplierSelectionEnabled = true;
                }

                AvailableItems.Clear();
                ClearLoadedMatrixOnly();
            }

            NotifyCommandStates();
        }

        partial void OnSupplierInvoiceNoChanged(string value)
        {
            if (_isClearing)
                return;

            if (IsHeaderConfirmed && !GrnLines.Any())
            {
                IsHeaderConfirmed = false;
                AvailableItems.Clear();
                ClearLoadedMatrixOnly();
                StatusMessage = "Invoice number changed. Confirm header again.";
            }

            NotifyCommandStates();
        }

        partial void OnInvoiceDateChanged(DateTime value)
        {
            if (SelectedSupplier != null)
            {
                int creditDays = SelectedSupplier.DefaultCreditDays > 0
                    ? SelectedSupplier.DefaultCreditDays
                    : 30;

                DueDate = value.Date.AddDays(creditDays);
            }

            if (GrnLines.Any())
                QueueRecalculate();
        }

        partial void OnGlobalBillDiscountChanged(decimal value)
        {
            QueueRecalculate();
        }

        partial void OnFreightAmountChanged(decimal value)
        {
            QueueRecalculate();
        }

        partial void OnSelectedPOChanged(GrnPoLookupDto? value)
        {
            if (_isClearing)
                return;

            OnPropertyChanged(nameof(IsPoLinked));
            OnPropertyChanged(nameof(IsEntryEnabled));
            OnPropertyChanged(nameof(IsDirectEntryEnabled));

            if (value == null)
            {
                if (!IsHeaderConfirmed)
                    IsSupplierSelectionEnabled = true;

                StatusMessage = "Direct GRN mode.";
                NotifyCommandStates();
                return;
            }

            if (GrnLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "Clear the current GRN before selecting a Purchase Order.",
                    "GRN Lines Exist");

                _isClearing = true;
                SelectedPO = null;
                _isClearing = false;
                return;
            }

            _isLoadingPo = true;

            try
            {
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == value.SupplierId);
                IsSupplierSelectionEnabled = false;
                IsHeaderConfirmed = false;

                AvailableItems.Clear();
                ClearLoadedMatrixOnly();

                StatusMessage = "PO selected. Enter supplier invoice number, then click LOAD.";
            }
            finally
            {
                _isLoadingPo = false;
            }

            NotifyCommandStates();
        }

        // =========================================================
        // PO TO GRN
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanLoadPo))]
        private async Task LoadPoAsync()
        {
            if (SelectedPO == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a Purchase Order to load.",
                    "Selection Required");

                return;
            }

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                _messageBoxService.ShowWarning(
                    "Enter supplier invoice number before loading the Purchase Order.",
                    "Supplier Invoice Required");

                return;
            }

            if (GrnLines.Any())
            {
                bool confirmed = _messageBoxService.ShowConfirmation(
                    "Loading a Purchase Order will clear the current GRN lines.\n\nContinue?",
                    "Replace Current GRN",
                    MessageBoxImage.Warning);

                if (!confirmed)
                    return;
            }

            IsBusy = true;
            StatusMessage = $"Loading PO {SelectedPO.PoNumber}...";

            try
            {
                _isLoadingPo = true;

                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == SelectedPO.SupplierId);

                if (SelectedSupplier == null)
                {
                    _messageBoxService.ShowWarning(
                        "The supplier linked to this Purchase Order is inactive or missing.",
                        "Supplier Missing");

                    return;
                }

                bool duplicateInvoice = await _grnRepository.SupplierInvoiceExistsAsync(
                    SelectedSupplier.Id,
                    SupplierInvoiceNo);

                if (duplicateInvoice)
                {
                    _messageBoxService.ShowWarning(
                        "This supplier invoice number has already been posted for the selected supplier.",
                        "Duplicate Supplier Invoice");

                    return;
                }

                var poLines = await _grnRepository.GetOutstandingPoLinesAsync(
                    SelectedPO.PoHeaderId);

                if (!poLines.Any())
                {
                    _messageBoxService.ShowInformation(
                        "This Purchase Order has no outstanding lines to receive.",
                        "No Outstanding Quantity");

                    return;
                }

                SupplierPricesIncludeVat = poLines[0].IsVatIncluded;

                UnsubscribeGrnLineEvents();

                GrnLines.Clear();
                ClearLoadedMatrixOnly();
                AvailableItems.Clear();

                IsSupplierSelectionEnabled = false;
                IsHeaderConfirmed = true;

                DueDate = InvoiceDate.Date.AddDays(
                    SelectedSupplier.DefaultCreditDays > 0
                        ? SelectedSupplier.DefaultCreditDays
                        : 30);

                foreach (var poLine in poLines)
                {
                    var grnLine = new GrnLineEntryDto
                    {
                        PoLineId = poLine.PoLineId,
                        ItemVariantId = poLine.ItemVariantId,
                        ItemCode = poLine.ItemCode,
                        SkuCode = poLine.SkuCode,
                        Barcode = poLine.Barcode,
                        Description = poLine.Description,
                        PrintName = poLine.PrintName,
                        VariantDescription = string.IsNullOrWhiteSpace(poLine.VariantDescription)
                            ? "Standard"
                            : poLine.VariantDescription,
                        Uom = string.IsNullOrWhiteSpace(poLine.Uom) ? "PCS" : poLine.Uom,
                        OrderedQty = poLine.OrderedQty,
                        OutstandingPoQty = poLine.OutstandingQty,
                        ReceivedQty = poLine.OutstandingQty,
                        UnitCost = poLine.ExpectedCost,

                        LineDiscountMode = string.IsNullOrWhiteSpace(poLine.LineDiscountMode)
                            ? "Amount"
                            : poLine.LineDiscountMode,
                        LineDiscountValue = poLine.LineDiscountValue,
                        LineDiscount = poLine.LineDiscount,

                        TaxCategoryCode = poLine.TaxCategoryCode,
                        TaxCategoryName = poLine.TaxCategoryName,
                        VatRatePercent = poLine.VatRatePercent,
                        IsVatIncluded = SupplierPricesIncludeVat,
                        VatAmount = poLine.VatAmount,

                        BatchNo = string.Empty,
                        ExpiryDate = null,

                        HasBatchTracking = poLine.HasBatchTracking,
                        HasExpiryTracking = poLine.HasExpiryTracking,
                        IsScaleItem = poLine.IsScaleItem,
                        AllowDecimalQuantity = poLine.AllowDecimalQuantity,
                        RequiresExpiry = poLine.RequiresExpiry,

                        CurrentRetailPrice = poLine.CurrentRetailPrice,
                        NewRetailPrice = poLine.CurrentRetailPrice,
                        CurrentWholesalePrice = poLine.CurrentWholesalePrice,
                        NewWholesalePrice = poLine.CurrentWholesalePrice,
                        CurrentMinimumPrice = poLine.CurrentMinimumPrice,
                        NewMinimumPrice = poLine.CurrentMinimumPrice,
                        CurrentMaximumPrice = poLine.CurrentMaximumPrice,
                        NewMaximumPrice = poLine.CurrentMaximumPrice,
                        UpdateSellingPrices = false
                    };

                    grnLine.RecalculateLineAmounts();

                    SubscribeGrnLineEvents(grnLine);
                    GrnLines.Add(grnLine);
                }

                RecalculateTotals();

                StatusMessage = $"{GrnLines.Count} PO line(s) loaded for receiving.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load PO.";

                _messageBoxService.ShowError(
                    $"Failed to load Purchase Order:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                _isLoadingPo = false;
                IsBusy = false;
            }
        }

        // =========================================================
        // MATRIX LOADING
        // =========================================================

        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (_isClearing)
                return;

            if (value == null)
                return;

            if (!IsEntryEnabled)
            {
                _messageBoxService.ShowWarning(
                    "Confirm supplier and supplier invoice number before loading items.",
                    "Header Not Confirmed");

                SelectedItem = null;
                return;
            }

            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnMatrixFilterTextChanged(string value)
        {
            ApplyMatrixFilter();
        }

        partial void OnMatrixExpiryDateChanged(DateTime? value)
        {
            foreach (var line in _allMatrixVariants.Where(v => v.RequiresExpiry))
                line.ExpiryDate = value?.Date;
        }

        partial void OnBulkMatrixQuantityChanged(decimal value)
        {
            ApplyBulkMatrixQuantityCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkMatrixUnitCostChanged(decimal value)
        {
            ApplyBulkMatrixUnitCostCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkMatrixSellingPriceChanged(decimal value)
        {
            ApplyBulkMatrixSellingPriceCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkMatrixVatIncludedChanged(bool value)
        {
            foreach (var item in ActiveMatrixVariants)
                item.IsVatIncluded = value;
        }

        partial void OnBulkDiscountModeChanged(string value)
        {
            ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
        }

        partial void OnBulkDiscountValueChanged(decimal value)
        {
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            if (SelectedSupplier == null)
                return;

            IsBusy = true;
            StatusMessage = "Loading item variants...";

            try
            {
                _allMatrixVariants.Clear();
                ActiveMatrixVariants.Clear();
                SelectedMatrixVariant = null;

                var variants = await _grnRepository.GetReceivableVariantsByParentForSupplierAsync(
                    parentId,
                    SelectedSupplier.Id,
                    InvoiceDate.Date);

                foreach (var variant in variants)
                {
                    var newLine = BuildLineFromLookup(variant);
                    newLine.IsVatIncluded = SupplierPricesIncludeVat;
                    newLine.RecalculateLineAmounts();

                    _allMatrixVariants.Add(newLine);
                }

                if (!_allMatrixVariants.Any())
                {
                    _messageBoxService.ShowInformation(
                        "None of the variants for this item are approved for the selected supplier.",
                        "No Supplier-Approved Variants");
                }

                ApplyMatrixFilter();

                StatusMessage = $"{ActiveMatrixVariants.Count} variant(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load item variants.";

                _messageBoxService.ShowError(
                    $"Failed to load item variants:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();

            IEnumerable<GrnLineEntryDto> query = _allMatrixVariants;

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                string search = MatrixFilterText.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    SafeLower(v.DisplayName).Contains(search) ||
                    SafeLower(v.VariantDescription).Contains(search) ||
                    SafeLower(v.Description).Contains(search) ||
                    SafeLower(v.ItemCode).Contains(search) ||
                    SafeLower(v.SkuCode).Contains(search) ||
                    SafeLower(v.Barcode).Contains(search));
            }

            foreach (var item in query)
                ActiveMatrixVariants.Add(item);

            SelectedMatrixVariant = ActiveMatrixVariants.FirstOrDefault();

            OnPropertyChanged(nameof(IsMatrixExpiryEnabled));
            NotifyCommandStates();
        }

        [RelayCommand(CanExecute = nameof(CanApplyBulkMatrixQuantity))]
        private void ApplyBulkMatrixQuantity()
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "Load an item matrix first.",
                    "No Matrix");

                return;
            }

            if (BulkMatrixQuantity < 0)
            {
                _messageBoxService.ShowWarning(
                    "Bulk quantity cannot be negative.",
                    "Validation");

                return;
            }

            var decimalBlocked = ActiveMatrixVariants
                .FirstOrDefault(v =>
                    !v.AllowDecimalQuantity &&
                    HasDecimalPart(BulkMatrixQuantity));

            if (decimalBlocked != null)
            {
                _messageBoxService.ShowWarning(
                    $"Decimal quantity is not allowed for '{decimalBlocked.DisplayName}' with UOM '{decimalBlocked.Uom}'.",
                    "Validation");

                return;
            }

            foreach (var item in ActiveMatrixVariants)
                item.ReceivedQty = BulkMatrixQuantity;

            StatusMessage = $"Bulk quantity {QuantityDisplayFormatter.Format(BulkMatrixQuantity)} applied to visible matrix variants.";
        }

        [RelayCommand(CanExecute = nameof(CanApplyBulkMatrixUnitCost))]
        private void ApplyBulkMatrixUnitCost()
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "Load an item matrix first.",
                    "No Matrix");

                return;
            }

            if (BulkMatrixUnitCost <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Bulk received cost must be greater than zero.",
                    "Validation");

                return;
            }

            foreach (var item in ActiveMatrixVariants)
                item.UnitCost = BulkMatrixUnitCost;

            StatusMessage = $"Bulk received cost Rs. {BulkMatrixUnitCost:N2} applied to visible matrix variants.";
        }

        [RelayCommand(CanExecute = nameof(CanApplyBulkMatrixSellingPrice))]
        private void ApplyBulkMatrixSellingPrice()
        {
            if (!ActiveMatrixVariants.Any())
            {
                _messageBoxService.ShowWarning(
                    "Load an item matrix first.",
                    "No Matrix");

                return;
            }

            if (BulkMatrixSellingPrice <= 0)
            {
                _messageBoxService.ShowWarning(
                    "Bulk selling price must be greater than zero.",
                    "Validation");

                return;
            }

            foreach (var item in ActiveMatrixVariants)
            {
                item.NewRetailPrice = BulkMatrixSellingPrice;
                item.UpdateSellingPrices = true;
            }

            StatusMessage = $"Bulk selling price Rs. {BulkMatrixSellingPrice:N2} applied to visible matrix variants.";
        }

        // =========================================================
        // REUSABLE VARIANT ENTRY DIALOG
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanOpenPurchasingVariantEntryDialog))]
        private void OpenPurchasingVariantEntryDialog()
        {
            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Select and confirm a supplier before opening variant entry.",
                    "Supplier Required");
                return;
            }

            if (AvailableItems.Count == 0)
            {
                _messageBoxService.ShowInformation(
                    "No supplier-approved purchasable Stock Items are available for this supplier.",
                    "No Items");
                return;
            }

            try
            {
                int supplierId = SelectedSupplier.Id;

                IReadOnlyList<PurchasingItemOption> itemOptions = AvailableItems
                    .Select(item => new PurchasingItemOption
                    {
                        ParentId = item.ParentId,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        TrackingText = item.TrackingText
                    })
                    .ToList();

                async Task<IReadOnlyList<PurchasingVariantSource>> LoadVariantsAsync(int parentId)
                {
                    IReadOnlyList<GrnVariantLookupDto> variants =
                        await _grnRepository.GetReceivableVariantsByParentForSupplierAsync(
                            parentId,
                            supplierId,
                            InvoiceDate.Date);

                    return variants
                        .Select(variant => new PurchasingVariantSource
                        {
                            ItemVariantId = variant.ItemVariantId,
                            ItemCode = variant.ItemCode,
                            SkuCode = variant.SkuCode,
                            Barcode = variant.Barcode,
                            Description = variant.Description,
                            PrintName = variant.PrintName,
                            VariantDescription = variant.VariantDescription,
                            Uom = variant.Uom,
                            SuggestedUnitCost = variant.LastSupplierCost > 0m
                                ? variant.LastSupplierCost
                                : variant.CurrentCost,
                            HasBatchTracking = variant.HasBatchTracking,
                            RequiresExpiry = variant.RequiresExpiry,
                            IsScaleItem = variant.IsScaleItem,
                            AllowDecimalQuantity = variant.AllowDecimalQuantity,
                            LineDiscountMode = variant.LineDiscountMode,
                            LineDiscountValue = variant.LineDiscountValue,
                            LineDiscount = variant.LineDiscount,
                            VatRatePercent = variant.VatRatePercent,
                            VatAmount = variant.VatAmount,
                            TaxCategoryCode = variant.TaxCategoryCode,
                            TaxCategoryName = variant.TaxCategoryName,
                            CurrentRetailPrice = variant.CurrentRetailPrice,
                            CurrentWholesalePrice = variant.CurrentWholesalePrice,
                            CurrentMinimumPrice = variant.CurrentMinimumPrice,
                            CurrentMaximumPrice = variant.CurrentMaximumPrice
                        })
                        .ToList();
                }

                var dialogViewModel = new PurchasingVariantEntryDialogViewModel(
                    itemOptions,
                    LoadVariantsAsync,
                    ReceivedDate.Date,
                    expiryEntryEnabled: true,
                    quantityLabel: "Received Qty",
                    unitCostLabel: "Unit Cost");

                var dialog = new PurchasingVariantEntryDialog(dialogViewModel)
                {
                    Owner = GetDialogOwner()
                };

                if (dialog.ShowDialog() != true)
                    return;

                foreach (PurchasingVariantEntryRow acceptedRow in dialog.AcceptedRows)
                {
                    GrnLineEntryDto newLine = BuildLineFromPurchasingEntry(acceptedRow);
                    newLine.IsVatIncluded = SupplierPricesIncludeVat;
                    newLine.RecalculateLineAmounts();
                    MergeOrAddLine(newLine);
                }

                RecalculateTotals();
                QueueRecalculate();
                StatusMessage = $"{dialog.AcceptedRows.Count} variant row(s) added to the GRN.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Open GRN purchasing variant-entry dialog",
                    ex);

                _messageBoxService.ShowError(
                    "The variant-entry window could not be opened or completed. " +
                    "The current GRN remains available and no partial dialog changes were applied. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "GRN Variant Entry Error");
            }
        }

        private bool CanOpenPurchasingVariantEntryDialog()
        {
            return !IsBusy &&
                   IsEntryEnabled &&
                   SelectedSupplier != null &&
                   AvailableItems.Count > 0;
        }

        private GrnLineEntryDto BuildLineFromPurchasingEntry(
            PurchasingVariantEntryRow row)
        {
            PurchasingVariantSource source = row.Source;

            return new GrnLineEntryDto
            {
                ItemVariantId = source.ItemVariantId,
                ItemCode = source.ItemCode,
                SkuCode = source.SkuCode,
                Barcode = source.Barcode,
                Description = source.Description,
                PrintName = source.PrintName,
                VariantDescription = string.IsNullOrWhiteSpace(source.VariantDescription)
                    ? "Standard"
                    : source.VariantDescription,
                Uom = string.IsNullOrWhiteSpace(source.Uom)
                    ? "PCS"
                    : source.Uom,
                UnitCost = row.UnitCost,
                OrderedQty = 0m,
                OutstandingPoQty = 0m,
                ReceivedQty = row.Quantity,
                LineDiscountMode = string.IsNullOrWhiteSpace(source.LineDiscountMode)
                    ? "Amount"
                    : source.LineDiscountMode,
                LineDiscountValue = source.LineDiscountValue,
                LineDiscount = source.LineDiscount,
                TaxCategoryCode = source.TaxCategoryCode,
                TaxCategoryName = source.TaxCategoryName,
                VatRatePercent = source.VatRatePercent,
                IsVatIncluded = SupplierPricesIncludeVat,
                VatAmount = source.VatAmount,
                BatchNo = string.Empty,
                ExpiryDate = source.RequiresExpiry ? row.ExpiryDate?.Date : null,
                HasBatchTracking = source.HasBatchTracking,
                HasExpiryTracking = source.RequiresExpiry,
                IsScaleItem = source.IsScaleItem,
                AllowDecimalQuantity = source.AllowDecimalQuantity,
                RequiresExpiry = source.RequiresExpiry,
                CurrentRetailPrice = source.CurrentRetailPrice,
                NewRetailPrice = source.CurrentRetailPrice,
                CurrentWholesalePrice = source.CurrentWholesalePrice,
                NewWholesalePrice = source.CurrentWholesalePrice,
                CurrentMinimumPrice = source.CurrentMinimumPrice,
                NewMinimumPrice = source.CurrentMinimumPrice,
                CurrentMaximumPrice = source.CurrentMaximumPrice,
                NewMaximumPrice = source.CurrentMaximumPrice,
                UpdateSellingPrices = false
            };
        }

        // =========================================================
        // BARCODE / SKU ADD
        // =========================================================

        [RelayCommand]
        private async Task AddItemAsync()
        {
            string term = (ScanBarcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            if (!IsEntryEnabled)
            {
                _messageBoxService.ShowWarning(
                    "Confirm supplier and supplier invoice number before scanning items.",
                    "Header Not Confirmed");

                ScanBarcode = string.Empty;
                return;
            }

            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier before scanning items.",
                    "Supplier Required");

                ScanBarcode = string.Empty;
                return;
            }

            try
            {
                var variant = await _grnRepository.GetReceivableVariantByBarcodeOrSkuAsync(
                    term,
                    SelectedSupplier.Id,
                    InvoiceDate.Date);

                if (variant == null)
                {
                    _messageBoxService.ShowWarning(
                        $"Barcode/SKU '{term}' was not found or is not approved for this supplier.",
                        "Item Not Found");

                    ScanBarcode = string.Empty;
                    return;
                }

                var newLine = BuildLineFromLookup(variant);
                newLine.ReceivedQty = 1m;

                if (newLine.RequiresExpiry && MatrixExpiryDate.HasValue)
                    newLine.ExpiryDate = MatrixExpiryDate.Value.Date;

                newLine.RecalculateLineAmounts();

                MergeOrAddLine(newLine);

                ScanBarcode = string.Empty;
                RecalculateTotals();

                StatusMessage = $"Item added: {newLine.DisplayName}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to add item.";

                _messageBoxService.ShowError(
                    $"Failed to add item:\n\n{ex.Message}",
                    "Error");
            }
        }

        private GrnLineEntryDto BuildLineFromLookup(GrnVariantLookupDto variant)
        {
            decimal cost = variant.LastSupplierCost > 0
                ? variant.LastSupplierCost
                : variant.CurrentCost;

            return new GrnLineEntryDto
            {
                ItemVariantId = variant.ItemVariantId,
                ItemCode = variant.ItemCode,
                SkuCode = variant.SkuCode,
                Barcode = variant.Barcode,
                Description = variant.Description,
                PrintName = variant.PrintName,
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,
                Uom = string.IsNullOrWhiteSpace(variant.Uom)
                    ? "PCS"
                    : variant.Uom,
                UnitCost = cost,
                OrderedQty = 0m,
                OutstandingPoQty = 0m,
                ReceivedQty = 0m,

                LineDiscountMode = string.IsNullOrWhiteSpace(variant.LineDiscountMode)
                    ? "Amount"
                    : variant.LineDiscountMode,
                LineDiscountValue = variant.LineDiscountValue,
                LineDiscount = variant.LineDiscount,

                TaxCategoryCode = variant.TaxCategoryCode,
                TaxCategoryName = variant.TaxCategoryName,
                VatRatePercent = variant.VatRatePercent,
                IsVatIncluded = SupplierPricesIncludeVat,
                VatAmount = variant.VatAmount,

                BatchNo = string.Empty,
                ExpiryDate = null,

                HasBatchTracking = variant.HasBatchTracking,
                HasExpiryTracking = variant.HasExpiryTracking,
                IsScaleItem = variant.IsScaleItem,
                AllowDecimalQuantity = variant.AllowDecimalQuantity,
                RequiresExpiry = variant.RequiresExpiry,

                CurrentRetailPrice = variant.CurrentRetailPrice,
                NewRetailPrice = variant.CurrentRetailPrice,
                CurrentWholesalePrice = variant.CurrentWholesalePrice,
                NewWholesalePrice = variant.CurrentWholesalePrice,
                CurrentMinimumPrice = variant.CurrentMinimumPrice,
                NewMinimumPrice = variant.CurrentMinimumPrice,
                CurrentMaximumPrice = variant.CurrentMaximumPrice,
                NewMaximumPrice = variant.CurrentMaximumPrice,
                UpdateSellingPrices = false
            };
        }

        // =========================================================
        // MATRIX ADD
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            if (!IsEntryEnabled)
            {
                _messageBoxService.ShowWarning(
                    "Confirm supplier and supplier invoice number before adding matrix items.",
                    "Header Not Confirmed");

                return;
            }

            var itemsToAdd = _allMatrixVariants
                .Where(v => v.ReceivedQty > 0)
                .ToList();

            if (!itemsToAdd.Any())
            {
                _messageBoxService.ShowWarning(
                    "Please enter a received quantity for at least one matrix variant.",
                    "No Quantity");

                return;
            }

            if (MatrixExpiryDate is DateTime matrixExpiryDate)
            {
                foreach (var item in itemsToAdd.Where(i => i.RequiresExpiry && !i.ExpiryDate.HasValue))
                    item.ExpiryDate = matrixExpiryDate.Date;
            }

            var errors = new List<string>();

            foreach (var item in itemsToAdd)
            {
                item.IsVatIncluded = SupplierPricesIncludeVat;
                item.RecalculateLineAmounts();

                errors.AddRange(item.ValidateForPost(isPoLinked: false));

                if (item.ExpiryDate.HasValue &&
                    item.ExpiryDate.Value.Date < ReceivedDate.Date)
                {
                    errors.Add($"{item.DisplayName}: expiry date cannot be before received date.");
                }
            }

            if (errors.Any())
            {
                _messageBoxService.ShowWarning(
                    "Cannot add matrix items because validation failed:\n\n" +
                    string.Join("\n", errors),
                    "Validation Error");

                return;
            }

            foreach (var item in itemsToAdd)
            {
                var newLine = item.CloneForGrnEntry();

                newLine.BatchNo = string.Empty;
                newLine.ExpiryDate = newLine.RequiresExpiry
                    ? newLine.ExpiryDate?.Date
                    : null;

                newLine.RecalculateLineAmounts();

                MergeOrAddLine(newLine);
            }

            ClearLoadedMatrixOnly();
            RecalculateTotals();

            StatusMessage = "Matrix items added to GRN.";
        }

        private void MergeOrAddLine(GrnLineEntryDto newLine)
        {
            DateTime? newExpiry = newLine.RequiresExpiry
                ? newLine.ExpiryDate?.Date
                : null;

            var existing = GrnLines.FirstOrDefault(l =>
                l.ItemVariantId == newLine.ItemVariantId &&
                l.ExpiryDate?.Date == newExpiry &&
                (l.PoLineId ?? 0) == (newLine.PoLineId ?? 0));

            if (existing == null)
            {
                SubscribeGrnLineEvents(newLine);
                GrnLines.Add(newLine);
                NotifyPriceUpdateSummary();
                PostGrnCommand.NotifyCanExecuteChanged();
                return;
            }

            existing.ReceivedQty += newLine.ReceivedQty;

            if (newLine.UnitCost > 0)
                existing.UnitCost = newLine.UnitCost;

            existing.LineDiscountMode = newLine.LineDiscountMode;
            existing.LineDiscountValue += newLine.LineDiscountValue;

            existing.TaxCategoryCode = newLine.TaxCategoryCode;
            existing.TaxCategoryName = newLine.TaxCategoryName;
            existing.VatRatePercent = newLine.VatRatePercent;
            existing.IsVatIncluded = SupplierPricesIncludeVat;

            if (!string.IsNullOrWhiteSpace(newLine.Uom))
                existing.Uom = newLine.Uom;

            if (newLine.RequiresExpiry)
                existing.ExpiryDate = newExpiry;

            if (newLine.HasAnySellingPriceChange)
            {
                existing.SellingPriceAction = GrnSellingPriceActionCodes.Normalize(newLine.SellingPriceAction);
                existing.UpdateSellingPrices = existing.SellingPriceAction ==
                    GrnSellingPriceActionCodes.UpdateMasterPrice;
                existing.NewRetailPrice = newLine.NewRetailPrice;
                existing.NewWholesalePrice = newLine.NewWholesalePrice;
                existing.NewMinimumPrice = newLine.NewMinimumPrice;
                existing.NewMaximumPrice = newLine.NewMaximumPrice;
                existing.RetailMarkupPercent = newLine.RetailMarkupPercent;
                existing.WholesaleMarkupPercent = newLine.WholesaleMarkupPercent;
            }

            existing.RecalculateLineAmounts();

            NotifyPriceUpdateSummary();
            PostGrnCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void RemoveLine(GrnLineEntryDto? line)
        {
            if (line == null)
                return;

            line.PropertyChanged -= GrnLine_PropertyChanged;

            GrnLines.Remove(line);
            RecalculateTotals();
            NotifyPriceUpdateSummary();

            StatusMessage = "Line removed.";

            PostGrnCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // DISCOUNT BULK APPLY
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanApplyBulkDiscountModeToLines))]
        private void ApplyBulkDiscountModeToLines()
        {
            if (!GrnLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "Add GRN lines before applying discount type.",
                    "No GRN Lines");

                return;
            }

            string mode = NormalizeDiscountMode(BulkDiscountMode);

            foreach (var line in GrnLines)
                line.LineDiscountMode = mode;

            RecalculateTotals();

            StatusMessage = $"Discount type '{mode}' applied to all GRN lines.";
        }

        [RelayCommand(CanExecute = nameof(CanApplyBulkDiscountValueToLines))]
        private void ApplyBulkDiscountValueToLines()
        {
            if (!GrnLines.Any())
            {
                _messageBoxService.ShowWarning(
                    "Add GRN lines before applying discount value.",
                    "No GRN Lines");

                return;
            }

            if (BulkDiscountValue < 0)
            {
                _messageBoxService.ShowWarning(
                    "Discount value cannot be negative.",
                    "Validation");

                return;
            }

            string mode = NormalizeDiscountMode(BulkDiscountMode);

            if (mode == "Percent" && BulkDiscountValue > 100)
            {
                _messageBoxService.ShowWarning(
                    "Discount percentage cannot be greater than 100.",
                    "Validation");

                return;
            }

            foreach (var line in GrnLines)
            {
                line.LineDiscountMode = mode;
                line.LineDiscountValue = BulkDiscountValue;
            }

            RecalculateTotals();

            StatusMessage = $"Discount value {BulkDiscountValue:N2} applied to all GRN lines.";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        [RelayCommand]
        public void RecalculateTotals()
        {
            if (_isRecalculating)
                return;

            _isRecalculating = true;

            try
            {
                decimal subtotalGross = 0m;
                decimal lineDiscountTotal = 0m;
                decimal vatTotal = 0m;
                decimal linePayableTotal = 0m;

                foreach (var line in GrnLines)
                {
                    if (line.ReceivedQty <= 0 || line.UnitCost <= 0)
                    {
                        line.LineDiscount = 0m;
                        line.VatAmount = 0m;
                        line.LineTotal = 0m;
                        line.LandedCost = 0m;
                        continue;
                    }

                    line.RecalculateLineAmounts();

                    subtotalGross += line.GrossAmount;
                    lineDiscountTotal += line.LineDiscount;
                    vatTotal += line.VatAmount;
                    linePayableTotal += line.LineTotal;
                }

                Subtotal = Math.Round(subtotalGross, 2);
                TotalDiscountAmount = Math.Round(lineDiscountTotal + GlobalBillDiscount, 2);
                TotalVatAmount = Math.Round(vatTotal, 2);

                decimal net = linePayableTotal - GlobalBillDiscount + FreightAmount;
                NetPayable = Math.Round(net < 0 ? 0 : net, 2);

                AllocateLandedCost();
            }
            finally
            {
                _isRecalculating = false;
            }

            PostGrnCommand.NotifyCanExecuteChanged();

            if (!_isApplyingAuthoritativePreview)
                QueueRecalculate();
        }

        private void AllocateLandedCost()
        {
            decimal totalCostBase = GrnLines.Sum(GetLineCostBaseForLandedCost);

            if (totalCostBase <= 0)
            {
                foreach (var line in GrnLines)
                    line.LandedCost = 0m;

                return;
            }

            foreach (var line in GrnLines)
            {
                if (line.ReceivedQty <= 0)
                {
                    line.LandedCost = 0m;
                    continue;
                }

                decimal lineCostBase = GetLineCostBaseForLandedCost(line);
                decimal weight = lineCostBase / totalCostBase;

                decimal allocatedFreight = FreightAmount * weight;
                decimal allocatedGlobalDiscount = GlobalBillDiscount * weight;

                decimal landedLineTotal = lineCostBase + allocatedFreight - allocatedGlobalDiscount;

                if (landedLineTotal < 0)
                    landedLineTotal = 0m;

                line.LandedCost = Math.Round(landedLineTotal / line.ReceivedQty, 2);
            }
        }

        private static decimal GetLineCostBaseForLandedCost(GrnLineEntryDto line)
        {
            decimal gross = line.ReceivedQty * line.UnitCost;
            decimal afterLineDiscount = gross - line.LineDiscount;

            if (afterLineDiscount < 0)
                afterLineDiscount = 0m;

            if (line.VatRatePercent <= 0)
                return afterLineDiscount;

            if (!line.IsVatIncluded)
                return afterLineDiscount;

            decimal vatRate = line.VatRatePercent / 100m;

            if (vatRate <= 0)
                return afterLineDiscount;

            return afterLineDiscount / (1 + vatRate);
        }

        private void QueueRecalculate()
        {
            _recalculateTimer.Stop();
            _recalculateTimer.Start();
        }

        // =========================================================
        // POST
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanPostGrn))]
        private async Task PostGrnAsync()
        {
            bool taxPreviewReady = await RecalculateTotalsAuthoritativelyAsync(showErrors: true);

            if (!taxPreviewReady || !ValidateBeforePost())
                return;

            int masterUpdates = GrnLines.Count(line =>
                GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
                GrnSellingPriceActionCodes.UpdateMasterPrice);
            int batchOverrides = GrnLines.Count(line =>
                GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
                GrnSellingPriceActionCodes.SetBatchPriceOverride);
            int keepCurrent = GrnLines.Count(line =>
                GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
                GrnSellingPriceActionCodes.UseCurrentMasterPrice);

            string priceChangeText = masterUpdates == 0 && batchOverrides == 0
                ? "All received rows will keep their current pricing authority."
                : $"{masterUpdates} master price update(s), {batchOverrides} batch override(s), " +
                  $"and {keepCurrent} row(s) keeping current pricing will be posted and audited.";

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Post GRN for Rs. {NetPayable:N2}?\n\n" +
                "This will update inventory, item batches/stock buckets, PO received quantities, and supplier ledger.\n\n" +
                priceChangeText,
                "Confirm GRN Posting",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;
            StatusMessage = "Posting GRN...";

            try
            {
                if (!await RecalculateTotalsAuthoritativelyAsync(showErrors: true))
                    return;

                var sourceLinesForPosting = GrnLines
                    .Where(l => l.ReceivedQty > 0)
                    .ToList();

                bool hasPrintableBatchLabels = sourceLinesForPosting.Any(l => l.HasBatchTracking);
                int printableBatchLineCount = sourceLinesForPosting.Count(l => l.HasBatchTracking);

                var validLines = sourceLinesForPosting
                    .Select(ToPostingLine)
                    .ToList();

                string authenticatedUsername = GetAuthenticatedUsername();

                var header = new GrnHeader
                {
                    PurchaseOrderId = SelectedPO?.PoHeaderId,
                    SupplierId = SelectedSupplier!.Id,
                    SupplierInvoiceNo = SupplierInvoiceNo.Trim(),
                    InvoiceDate = InvoiceDate.Date,
                    ReceivedDate = ReceivedDate.Date,
                    DueDate = DueDate.Date,
                    CreditDays = Math.Max(0, (DueDate.Date - InvoiceDate.Date).Days),
                    Remarks = Remarks.Trim(),
                    IsTaxInclusive =
                        SelectedSupplier?.HasVat == true &&
                        SupplierPricesIncludeVat,
                    Subtotal = Subtotal,
                    GlobalBillDiscount = GlobalBillDiscount,
                    FreightAmount = FreightAmount,
                    TotalDiscountAmount = TotalDiscountAmount,
                    TotalVatAmount = TotalVatAmount,
                    NetPayable = NetPayable,
                    CreatedBy = authenticatedUsername,
                    PostedBy = authenticatedUsername
                };

                await _grnRepository.PostGrnAsync(header, validLines);

                string postedGrnNumber = string.IsNullOrWhiteSpace(header.GrnNumber)
                    ? "posted GRN"
                    : header.GrnNumber.Trim();

                if (hasPrintableBatchLabels)
                {
                    bool printNow = _messageBoxService.ShowConfirmation(
                        $"GRN {postedGrnNumber} posted successfully.\n\n" +
                        $"This GRN has {printableBatchLineCount} batch-tracked line(s) that need GRN batch barcode labels.\n\n" +
                        "Open Barcode Printer page now and print the batch labels?",
                        "Print GRN Batch Barcodes",
                        MessageBoxImage.Question);

                    if (printNow)
                    {
                        _messageBoxService.ShowInformation(
                            $"Open Inventory Operations > Barcode Printer.\n\n" +
                            $"1. Select GRN: {postedGrnNumber}\n" +
                            "2. Click Load GRN Batches\n" +
                            "3. Check Print Qty\n" +
                            "4. Click Print Selected\n\n" +
                            "Average-cost GENERAL stock lines will not appear in the GRN barcode queue.",
                            "Barcode Printing");
                    }
                }
                else
                {
                    _messageBoxService.ShowInformation(
                        $"GRN {postedGrnNumber} posted successfully.\n\n" +
                        "No GRN batch barcode labels are required because this GRN has no batch-tracked lines.",
                        "Success");
                }

                Clear();
                await LoadOpenPurchaseOrdersAsync();

                StatusMessage = hasPrintableBatchLabels
                    ? $"GRN {postedGrnNumber} posted successfully. Print batch labels from Barcode Printer."
                    : $"GRN {postedGrnNumber} posted successfully.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Posting blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Posting Blocked");
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Posting failed.";

                _messageBoxService.ShowError(
                    $"Transaction rolled back.\n\n{message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateBeforePost()
        {
            if (!IsHeaderConfirmed)
            {
                _messageBoxService.ShowWarning(
                    "Confirm supplier and supplier invoice number before posting.",
                    "Header Not Confirmed");

                return false;
            }

            if (SelectedSupplier == null)
            {
                _messageBoxService.ShowWarning(
                    "Please select a supplier.",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                _messageBoxService.ShowWarning(
                    "Supplier invoice number is required.",
                    "Validation Error");

                return false;
            }

            if (DueDate.Date < InvoiceDate.Date)
            {
                _messageBoxService.ShowWarning(
                    "Due date cannot be before invoice date.",
                    "Validation Error");

                return false;
            }

            if (!GrnLines.Any(l => l.ReceivedQty > 0))
            {
                _messageBoxService.ShowWarning(
                    "Cannot post an empty GRN.",
                    "Validation Error");

                return false;
            }

            if (GlobalBillDiscount < 0 || FreightAmount < 0)
            {
                _messageBoxService.ShowWarning(
                    "Global discount and freight cannot be negative.",
                    "Validation Error");

                return false;
            }

            decimal payableBeforeGlobalDiscount = GrnLines.Sum(l => l.LineTotal);

            if (GlobalBillDiscount > payableBeforeGlobalDiscount)
            {
                _messageBoxService.ShowWarning(
                    "Global bill discount cannot be greater than GRN value.",
                    "Validation Error");

                return false;
            }

            var errors = new List<string>();

            bool isPoLinked = SelectedPO != null;

            foreach (var line in GrnLines.Where(l => l.ReceivedQty > 0))
            {
                line.RecalculateLineAmounts();

                errors.AddRange(line.ValidateForPost(isPoLinked));

                if (line.ExpiryDate.HasValue &&
                    line.ExpiryDate.Value.Date < ReceivedDate.Date)
                {
                    errors.Add($"{line.DisplayName}: expiry date cannot be before received date.");
                }
            }

            if (errors.Any())
            {
                _messageBoxService.ShowWarning(
                    "Cannot post GRN because validation failed:\n\n" +
                    string.Join("\n", errors),
                    "Validation Error");

                return false;
            }

            return true;
        }

        private string GetAuthenticatedUsername()
        {
            string username = _authService.CurrentUser?.Username?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException(
                    "The authenticated BackOffice user is unavailable. Sign in again before posting the GRN.");
            }

            return username;
        }

        private static GrnLine ToPostingLine(GrnLineEntryDto line)
        {
            return new GrnLine
            {
                PoLineId = line.PoLineId,
                ItemBatchId = line.ItemBatchId,
                ItemVariantId = line.ItemVariantId,

                BatchNo = string.Empty,

                ExpiryDate = line.RequiresExpiry
                    ? line.ExpiryDate?.Date
                    : null,

                Uom = line.Uom?.Trim() ?? string.Empty,
                OrderedQty = line.OrderedQty,
                ReceivedQty = line.ReceivedQty,
                UnitCost = line.UnitCost,

                LineDiscountMode = NormalizeDiscountMode(line.LineDiscountMode),
                LineDiscountValue = line.LineDiscountValue,
                LineDiscount = line.LineDiscount,

                VatRatePercent = line.VatRatePercent,
                IsVatIncluded = line.IsVatIncluded,
                VatAmount = line.VatAmount,

                LandedCost = line.LandedCost,
                LineTotal = line.LineTotal,

                SellingPriceAction = GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction),
                UpdateSellingPrices = GrnSellingPriceActionCodes.Normalize(line.SellingPriceAction) ==
                    GrnSellingPriceActionCodes.UpdateMasterPrice,
                CurrentRetailPrice = line.CurrentRetailPrice,
                NewRetailPrice = line.NewRetailPrice,
                CurrentWholesalePrice = line.CurrentWholesalePrice,
                NewWholesalePrice = line.NewWholesalePrice,
                CurrentMinimumPrice = line.CurrentMinimumPrice,
                NewMinimumPrice = line.NewMinimumPrice,
                CurrentMaximumPrice = line.CurrentMaximumPrice,
                NewMaximumPrice = line.NewMaximumPrice,
                RetailMarkupPercent = line.RetailMarkupPercent,
                WholesaleMarkupPercent = line.WholesaleMarkupPercent
            };
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand]
        private void Clear()
        {
            _isClearing = true;

            try
            {
                SelectedPO = null;
                IsSupplierSelectionEnabled = true;
                IsHeaderConfirmed = false;

                SelectedSupplier = null;
                SupplierInvoiceNo = string.Empty;

                InvoiceDate = DateTime.Now;
                ReceivedDate = DateTime.Now;
                DueDate = DateTime.Now.AddDays(30);

                Remarks = string.Empty;
                ScanBarcode = string.Empty;
                MatrixBatchNo = string.Empty;
                MatrixExpiryDate = null;
                MatrixFilterText = string.Empty;

                BulkMatrixQuantity = 0m;
                BulkMatrixUnitCost = 0m;
                BulkMatrixSellingPrice = 0m;
                BulkMatrixVatIncluded = false;
                SupplierPricesIncludeVat = false;

                BulkDiscountMode = DiscountModes.FirstOrDefault() ?? "Amount";
                BulkDiscountValue = 0m;

                GlobalBillDiscount = 0m;
                FreightAmount = 0m;
                SelectedItem = null;
                SelectedLine = null;
                SelectedMatrixVariant = null;

                AvailableItems.Clear();

                UnsubscribeGrnLineEvents();
                GrnLines.Clear();
                NotifyPriceUpdateSummary();

                ClearLoadedMatrixOnly();

                Subtotal = 0m;
                TotalDiscountAmount = 0m;
                TotalVatAmount = 0m;
                NetPayable = 0m;
                TaxableAmountTotal = 0m;
                StandardRatedAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                TaxPreviewStatus = "Add GRN rows to calculate authoritative VAT.";
                IsTaxPreviewRetryVisible = false;

                DocumentStatus = "DRAFT";
                StatusMessage = "Ready for new GRN.";

                OnPropertyChanged(nameof(IsPoLinked));
                OnPropertyChanged(nameof(IsEntryEnabled));
                OnPropertyChanged(nameof(IsDirectEntryEnabled));
                OnPropertyChanged(nameof(IsHeaderInputEnabled));
                OnPropertyChanged(nameof(IsMatrixExpiryEnabled));
            }
            finally
            {
                _isClearing = false;
            }

            NotifyCommandStates();
        }

        private void ClearLoadedMatrixOnly()
        {
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            SelectedMatrixVariant = null;
            MatrixFilterText = string.Empty;
            BulkMatrixQuantity = 0m;

            if (!_isClearing)
            {
                _isClearing = true;
                SelectedItem = null;
                _isClearing = false;
            }
            else
            {
                SelectedItem = null;
            }

            OnPropertyChanged(nameof(IsMatrixExpiryEnabled));
            NotifyCommandStates();
        }

        // =========================================================
        // EVENTS
        // =========================================================

        private void SubscribeGrnLineEvents(GrnLineEntryDto line)
        {
            line.PropertyChanged -= GrnLine_PropertyChanged;
            line.PropertyChanged += GrnLine_PropertyChanged;
        }

        private void UnsubscribeGrnLineEvents()
        {
            foreach (var line in GrnLines)
                line.PropertyChanged -= GrnLine_PropertyChanged;
        }

        private void GrnLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRecalculating || _isApplyingAuthoritativePreview)
                return;

            if (e.PropertyName == nameof(GrnLineEntryDto.ReceivedQty) ||
                e.PropertyName == nameof(GrnLineEntryDto.UnitCost) ||
                e.PropertyName == nameof(GrnLineEntryDto.LineDiscountMode) ||
                e.PropertyName == nameof(GrnLineEntryDto.LineDiscountValue) ||
                e.PropertyName == nameof(GrnLineEntryDto.LineDiscount) ||
                e.PropertyName == nameof(GrnLineEntryDto.VatRatePercent) ||
                e.PropertyName == nameof(GrnLineEntryDto.IsVatIncluded))
            {
                QueueRecalculate();
            }

            if (e.PropertyName == nameof(GrnLineEntryDto.RequiresExpiry))
            {
                OnPropertyChanged(nameof(IsMatrixExpiryEnabled));
            }

            if (e.PropertyName == nameof(GrnLineEntryDto.SellingPriceAction) ||
                e.PropertyName == nameof(GrnLineEntryDto.UpdateSellingPrices) ||
                e.PropertyName == nameof(GrnLineEntryDto.NewRetailPrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.NewWholesalePrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.NewMinimumPrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.NewMaximumPrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.CurrentRetailPrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.CurrentWholesalePrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.CurrentMinimumPrice) ||
                e.PropertyName == nameof(GrnLineEntryDto.CurrentMaximumPrice))
            {
                NotifyPriceUpdateSummary();
            }

            PostGrnCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // COMMAND STATES
        // =========================================================

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsHeaderInputEnabled));
            OnPropertyChanged(nameof(CanUseSupplierVatPriceMode));
            OnPropertyChanged(nameof(IsEntryEnabled));
            OnPropertyChanged(nameof(IsDirectEntryEnabled));
            OnPropertyChanged(nameof(IsMatrixExpiryEnabled));

            NotifyCommandStates();
        }

        partial void OnIsHeaderConfirmedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsHeaderInputEnabled));
            OnPropertyChanged(nameof(CanUseSupplierVatPriceMode));
            OnPropertyChanged(nameof(IsEntryEnabled));
            OnPropertyChanged(nameof(IsDirectEntryEnabled));
            OnPropertyChanged(nameof(IsMatrixExpiryEnabled));

            NotifyCommandStates();
        }

        private void NotifyCommandStates()
        {
            InitializeCommand.NotifyCanExecuteChanged();
            ConfirmHeaderCommand.NotifyCanExecuteChanged();
            LoadPoCommand.NotifyCanExecuteChanged();

            ApplyBulkMatrixQuantityCommand.NotifyCanExecuteChanged();
            ApplyBulkMatrixUnitCostCommand.NotifyCanExecuteChanged();
            ApplyBulkMatrixSellingPriceCommand.NotifyCanExecuteChanged();
            OpenPurchasingVariantEntryDialogCommand.NotifyCanExecuteChanged();

            ApplyBulkDiscountModeToLinesCommand.NotifyCanExecuteChanged();
            ApplyBulkDiscountValueToLinesCommand.NotifyCanExecuteChanged();

            OpenBulkSellingPriceDialogCommand.NotifyCanExecuteChanged();
            ResetProposedSellingPricesCommand.NotifyCanExecuteChanged();
            PostGrnCommand.NotifyCanExecuteChanged();
        }

        private bool CanInitialize()
        {
            return !IsBusy && !_isInitialized;
        }

        private bool CanConfirmHeader()
        {
            return !IsBusy &&
                   !IsHeaderConfirmed &&
                   SelectedSupplier != null &&
                   !string.IsNullOrWhiteSpace(SupplierInvoiceNo) &&
                   !GrnLines.Any();
        }

        private bool CanLoadPo()
        {
            return !IsBusy && SelectedPO != null;
        }

        private bool CanApplyBulkMatrixQuantity()
        {
            return !IsBusy &&
                   IsEntryEnabled &&
                   ActiveMatrixVariants.Any() &&
                   BulkMatrixQuantity >= 0;
        }

        private bool CanApplyBulkMatrixUnitCost()
        {
            return !IsBusy &&
                   IsEntryEnabled &&
                   ActiveMatrixVariants.Any() &&
                   BulkMatrixUnitCost > 0;
        }

        private bool CanApplyBulkMatrixSellingPrice()
        {
            return !IsBusy &&
                   IsEntryEnabled &&
                   ActiveMatrixVariants.Any() &&
                   BulkMatrixSellingPrice > 0;
        }

        private bool CanApplyBulkDiscountModeToLines()
        {
            return !IsBusy &&
                   GrnLines.Any() &&
                   !string.IsNullOrWhiteSpace(BulkDiscountMode);
        }

        private bool CanApplyBulkDiscountValueToLines()
        {
            return !IsBusy &&
                   GrnLines.Any() &&
                   BulkDiscountValue >= 0;
        }

        private bool CanPostGrn()
        {
            return !IsBusy &&
                   IsHeaderConfirmed &&
                   SelectedSupplier != null &&
                   GrnLines.Any(l => l.ReceivedQty > 0);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static bool HasDecimalPart(decimal value)
        {
            return value != Math.Truncate(value);
        }

        private static string SafeLower(string? value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }
    }

    internal static class GrnLineEntryDtoExtensions
    {
        public static GrnLineEntryDto CloneForGrnEntry(this GrnLineEntryDto source)
        {
            return new GrnLineEntryDto
            {
                GrnLineId = 0,
                PoLineId = source.PoLineId,
                ItemBatchId = source.ItemBatchId,
                ItemVariantId = source.ItemVariantId,

                ItemCode = source.ItemCode,
                SkuCode = source.SkuCode,
                Barcode = source.Barcode,
                Description = source.Description,
                PrintName = source.PrintName,
                VariantDescription = source.VariantDescription,
                Uom = source.Uom,

                OrderedQty = source.OrderedQty,
                OutstandingPoQty = source.OutstandingPoQty,

                HasBatchTracking = source.HasBatchTracking,
                HasExpiryTracking = source.HasExpiryTracking,
                IsScaleItem = source.IsScaleItem,
                AllowDecimalQuantity = source.AllowDecimalQuantity,
                RequiresExpiry = source.RequiresExpiry,

                BatchNo = source.BatchNo,
                ExpiryDate = source.ExpiryDate,

                ReceivedQty = source.ReceivedQty,
                UnitCost = source.UnitCost,

                LineDiscountMode = source.LineDiscountMode,
                LineDiscountValue = source.LineDiscountValue,
                LineDiscount = source.LineDiscount,

                TaxCategoryCode = source.TaxCategoryCode,
                TaxCategoryName = source.TaxCategoryName,
                VatRatePercent = source.VatRatePercent,
                IsVatIncluded = source.IsVatIncluded,
                VatAmount = source.VatAmount,
                TaxableAmount = source.TaxableAmount,
                GlobalDiscountAllocation = source.GlobalDiscountAllocation,

                LandedCost = source.LandedCost,
                LineTotal = source.LineTotal,

                CurrentRetailPrice = source.CurrentRetailPrice,
                NewRetailPrice = source.NewRetailPrice,
                CurrentWholesalePrice = source.CurrentWholesalePrice,
                NewWholesalePrice = source.NewWholesalePrice,
                CurrentMinimumPrice = source.CurrentMinimumPrice,
                NewMinimumPrice = source.NewMinimumPrice,
                CurrentMaximumPrice = source.CurrentMaximumPrice,
                NewMaximumPrice = source.NewMaximumPrice,
                RetailMarkupPercent = source.RetailMarkupPercent,
                WholesaleMarkupPercent = source.WholesaleMarkupPercent,
                SellingPriceAction = GrnSellingPriceActionCodes.Normalize(source.SellingPriceAction),
                UpdateSellingPrices = GrnSellingPriceActionCodes.Normalize(source.SellingPriceAction) ==
                    GrnSellingPriceActionCodes.UpdateMasterPrice
            };
        }
    }
}